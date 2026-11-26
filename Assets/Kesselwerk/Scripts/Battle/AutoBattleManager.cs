using System.Collections;
using System.Collections.Generic;
using Kesselwerk.Core;
using UnityEngine;

namespace Kesselwerk
{
    /// <summary>
    /// Vollautomatischer 3D-Kampf. Der Kampf wird zuerst deterministisch berechnet (BattleSim)
    /// und danach als Ereignisfolge in 3D abgespielt. So kann ein Server später dasselbe Ergebnis nachrechnen.
    /// </summary>
    public class AutoBattleManager : MonoBehaviour
    {
        public static int Speed = 1;
        public static AutoBattleManager Instance { get; private set; }
        public bool Finished => finished;

        Camera cam;
        BattleHud hud;
        StageDef stage;
        ArenaFight arena;
        TeamSpec enemyTeam;
        string title;
        BattleResult result;
        TeamSpec playerTeam;
        readonly Dictionary<int, Creature> views = new Dictionary<int, Creature>();
        float[] hp, maxHp, shield, energy;
        bool skipping, finished;
        int cursor;

        // Kamera
        float camDist = 16f, camFov = 40f;
        Vector3 camLook = new Vector3(0.3f, 0.5f, -0.9f);
        const float CamPitch = 40f;
        const float CamYaw = 28f;
        Vector3 zoomTarget;
        float zoomAmount, zoomGoal;
        float shakeUntil, shakePower;
        float introYaw;

        void Start()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.AutoRotation;
            Speed = Mathf.Clamp(Game.Data.battleSpeed, 1, 3);

            cam = ArenaBuilder.SetupCameraAndLight();
            ArenaBuilder.BuildArena(true);

            arena = Game.PendingArena;
            Game.PendingArena = null;
            playerTeam = Game.Data.BuildTeam();
            int seed;
            if (arena != null)
            {
                // Arena: Gegner und Startwert kommen vom Server, der den Kampf schon berechnet hat
                stage = new StageDef { region = 0, index = 0, name = "Arena" };
                enemyTeam = arena.enemy;
                seed = arena.seed;
                title = $"Arena  ·  gegen {arena.opponent}";
            }
            else
            {
                stage = Game.PendingStage ?? Campaign.Get(0, 0);
                enemyTeam = stage.enemy;
                seed = Random.Range(1, int.MaxValue);
                title = $"{Campaign.RegionNames[stage.region]}  ·  {stage.name}";
            }
            ArenaBuilder.ApplyRegionMood(stage.region);
            WeatherFx.DressRegion(stage.region);
            GrassField.Create(stage.region, true);
            weather = arena != null ? Weather.Klar : Campaign.WeatherFor(stage.region, stage.index);
            result = BattleSim.Run(playerTeam, enemyTeam, seed, weather);
            Debug.Log($"Kesselwerk: Kampf {title} berechnet – Startwert {seed}, {result.events.Count} Ereignisse, Sieger {(result.winner == 0 ? "Spieler" : "Gegner")}");
            if (arena != null && arena.serverWinner != result.winner)
                Debug.LogWarning("Kesselwerk: Arena-Ergebnis weicht vom Server ab – es zählt das Server-Ergebnis.");

            int n = result.units.Count;
            hp = new float[n]; maxHp = new float[n]; shield = new float[n]; energy = new float[n];
            foreach (var u in result.units)
            {
                maxHp[u.uid] = u.maxHp;
                hp[u.uid] = u.maxHp;
                shield[u.uid] = u.startShield;
            }

            hud = gameObject.AddComponent<BattleHud>();
            hud.Init(this, cam, title);
            WeatherFx.Create(weather, c => hud.Flash(c));

            foreach (var u in result.units)
            {
                bool player = u.team == 0;
                var pos = ArenaBuilder.SlotPos(player, u.slot);
                var v = Creature.Spawn(u.def, u.spec.stage, pos, player ? Vector3.forward : Vector3.back, player, u.slot);
                views[u.uid] = v;
                hud.AddUnit(u, v);
                hud.SetBar(u.uid, hp[u.uid], maxHp[u.uid], shield[u.uid], 0f, u.def.isBoss);
            }

            Sound.Music(true);
            StartCoroutine(Run());
        }

        Weather weather;

        void OnDestroy() => Time.timeScale = 1f;

        public void CycleSpeed()
        {
            Speed = Speed >= 3 ? 1 : Speed + 1;
            Game.Data.battleSpeed = Speed;
            if (!finished) Time.timeScale = Speed;
        }

        public void SkipToEnd()
        {
            if (finished) return;
            skipping = true;
        }

        // ------------------------------------------------------------------
        // Ablauf
        // ------------------------------------------------------------------

        IEnumerator Run()
        {
            Time.timeScale = 1f;
            // Auftritt: Kamera schwenkt, Kreaturen erscheinen nacheinander
            introYaw = 35f;
            float delay = 0f;
            foreach (var kv in views)
            {
                StartCoroutine(kv.Value.SpawnAnim(delay));
                delay += 0.08f;
            }
            Sound.Play("beschwoerung", 0.6f);
            hud.Banner(title, 1.0f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 1.4f)
            {
                introYaw = Mathf.Lerp(35f, 0f, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            introYaw = 0f;

            // Bossauftritt: kurze Kamerafahrt
            foreach (var u in result.units)
            {
                if (!u.def.isBoss) continue;
                var v = views[u.uid];
                hud.Banner($"<color=#ff7a50>BOSS</color>  {u.Name}", 1.3f);
                Sound.Voice(u.def, 1f);
                Shake(0.6f, 0.25f);
                Zoom(v.Center, 0.55f);
                Fx.Ring(v.transform.position + Vector3.up * 0.1f, new Color(3f, 0.8f, 0.3f));
                yield return new WaitForSeconds(1.5f);
                Zoom(Vector3.zero, 0f);
            }

            hud.ShowSynergies(Synergies.For(playerTeam), Synergies.For(enemyTeam));
            if (weather != Weather.Klar)
            {
                hud.Banner($"Wetter: {WeatherInfo.Name(weather)}\n<size=60%>{WeatherInfo.Effect(weather)}</size>", 1.4f);
                yield return new WaitForSeconds(1.6f);
            }
            hud.Banner("KAMPF!", 0.6f);
            yield return new WaitForSeconds(0.9f);

            Time.timeScale = Speed;
            cursor = 0;
            var events = result.events;
            while (cursor < events.Count && !skipping)
            {
                var e = events[cursor];
                if (e.type == EvType.Attack || e.type == EvType.Skill)
                {
                    int end = GroupEnd(cursor + 1);
                    var group = events.GetRange(cursor + 1, end - cursor - 1);
                    if (e.type == EvType.Attack) yield return PlayAttack(e, group);
                    else yield return PlaySkill(e, group);
                    cursor = end;
                    yield return new WaitForSeconds(0.05f);
                }
                else
                {
                    Apply(e, true);
                    cursor++;
                    if (e.type == EvType.BurnTick || e.type == EvType.PoisonTick || e.type == EvType.Stunned) yield return new WaitForSeconds(0.35f);
                }
            }

            if (skipping)
            {
                // Rest sofort anwenden
                for (; cursor < events.Count; cursor++) Apply(events[cursor], false);
                foreach (var u in result.units) if (!u.Alive && !views[u.uid].IsDead) views[u.uid].Die();
            }

            yield return Finish();
        }

        /// <summary>Folgeereignisse eines Angriffs reichen bis zur nächsten Aktion.</summary>
        int GroupEnd(int from)
        {
            var ev = result.events;
            int i = from;
            while (i < ev.Count)
            {
                var t = ev[i].type;
                if (t == EvType.Attack || t == EvType.Skill || t == EvType.Stunned || t == EvType.BurnTick || t == EvType.PoisonTick || t == EvType.End) break;
                // Regeneration zu Beginn der nächsten Aktion gehört nicht mehr dazu
                if (t == EvType.Heal && ev[i].actor == ev[i].target && i + 1 < ev.Count &&
                    (ev[i + 1].type == EvType.Attack || ev[i + 1].type == EvType.Skill || ev[i + 1].type == EvType.Stunned) && ev[i + 1].actor == ev[i].actor)
                    break;
                i++;
            }
            return i;
        }

        IEnumerator PlayAttack(BattleEvent e, List<BattleEvent> group)
        {
            var a = views[e.actor];
            var t = views[e.target];
            AddEnergy(e.actor, BattleSim.EnergyPerAttack);
            Sound.Voice(a.def, 0.35f);
            bool done = false;
            System.Action impact = () => { foreach (var g in group) Apply(g, true); done = true; };
            if (a.def.ranged) yield return a.RangedAttack(t, impact);
            else yield return a.MeleeAttack(t, impact);
            if (!done) impact();
        }

        IEnumerator PlaySkill(BattleEvent e, List<BattleEvent> group)
        {
            var a = views[e.actor];
            var def = a.def;
            energy[e.actor] = 0f;
            UpdateBar(e.actor);
            Color c = Look.ElementColor(def.element);
            hud.Banner($"<color={Look.Hex(c)}>{a.def.StageName(a.stage)}</color>: {e.text}", 0.8f);
            Sound.Play("zauber");
            Sound.Voice(def, 0.7f);
            Zoom(a.Center, 0.35f);

            // Ziele aus den Folgeereignissen
            var targets = new List<Creature>();
            foreach (var g in group)
                if ((g.type == EvType.Damage || g.type == EvType.Heal || g.type == EvType.Shield || g.type == EvType.Buff || g.type == EvType.Debuff) &&
                    views.TryGetValue(g.target, out var tv) && !targets.Contains(tv) && g.actor == e.actor)
                    targets.Add(tv);
            Creature main = e.target >= 0 && views.ContainsKey(e.target) ? views[e.target] : (targets.Count > 0 ? targets[0] : null);

            bool single = e.skill == SkillKind.Strike || e.skill == SkillKind.Burn || e.skill == SkillKind.Stun || e.skill == SkillKind.Freeze ||
                          e.skill == SkillKind.Execute || e.skill == SkillKind.Drain || e.skill == SkillKind.Cleave;
            bool applied = false;
            System.Action impact = () =>
            {
                if (applied) return;
                applied = true;
                SkillImpactFx(e, def, a, targets, c);
                foreach (var g in group) Apply(g, true);
            };

            if (single && !def.ranged && main != null && main != a)
            {
                yield return a.CastStart();
                Zoom(Vector3.zero, 0f);
                yield return a.MeleeAttack(main, impact, true);
            }
            else
            {
                yield return a.CastStart();
                Zoom(Vector3.zero, 0f);
                yield return SkillTravelFx(e, def, a, targets, c);
                impact();
                yield return new WaitForSeconds(0.35f);
                a.CastEnd();
            }
            impact();
        }

        /// <summary>Flug- und Vorbereitungseffekte je nach Fähigkeit.</summary>
        IEnumerator SkillTravelFx(BattleEvent e, CreatureDef def, Creature a, List<Creature> targets, Color c)
        {
            switch (e.skill)
            {
                case SkillKind.Chain:
                {
                    Vector3 from = a.Center;
                    foreach (var t in targets)
                    {
                        if (t == a) continue;
                        Fx.Lightning(from, t.Center, c * 3f);
                        Sound.Play("krit", 0.5f, 1.4f);
                        from = t.Center;
                        yield return new WaitForSeconds(0.12f);
                    }
                    break;
                }
                case SkillKind.BackLine:
                case SkillKind.Strike:
                case SkillKind.Burn:
                case SkillKind.Stun:
                case SkillKind.Execute:
                case SkillKind.Drain:
                case SkillKind.Cleave:
                {
                    var list = new List<Coroutine>();
                    foreach (var t in targets)
                        if (t != a) list.Add(StartCoroutine(Creature.Projectile(a.Center, t.Center, c, 0.3f)));
                    foreach (var co in list) yield return co;
                    break;
                }
                case SkillKind.AllEnemies:
                case SkillKind.DebuffDefense:
                {
                    if (def.element == Element.Wasser || e.skill == SkillKind.DebuffDefense)
                    {
                        bool enemySide = !a.isPlayer;
                        Fx.Wave(new Vector3(0f, 0.2f, enemySide ? -3.8f : 3.8f), 9f, c * 2.5f);
                        Sound.Play("zisch", 0.8f, 0.7f);
                        yield return new WaitForSeconds(0.35f);
                    }
                    else if (def.element == Element.Blitz)
                    {
                        foreach (var t in targets)
                        {
                            Fx.Lightning(t.Center + Vector3.up * 8f, t.Center, c * 3f);
                            yield return new WaitForSeconds(0.05f);
                        }
                    }
                    else yield return new WaitForSeconds(0.15f);
                    break;
                }
                default:
                    yield return new WaitForSeconds(0.1f);
                    break;
            }
        }

        void SkillImpactFx(BattleEvent e, CreatureDef def, Creature a, List<Creature> targets, Color c)
        {
            bool offensive = e.skill != SkillKind.HealLowest && e.skill != SkillKind.HealAll && e.skill != SkillKind.ShieldAll &&
                             e.skill != SkillKind.Taunt && e.skill != SkillKind.BuffAttack;
            foreach (var t in targets)
            {
                Vector3 p = t.transform.position;
                switch (e.skill)
                {
                    case SkillKind.HealLowest:
                    case SkillKind.HealAll:
                        Fx.Pillar(p, new Color(0.4f, 2.5f, 0.8f), t.Height * 1.4f, 50);
                        break;
                    case SkillKind.ShieldAll:
                    case SkillKind.Taunt:
                        Fx.Ring(p + Vector3.up * 0.1f, new Color(0.8f, 1.6f, 3f));
                        Fx.Pillar(p, new Color(0.6f, 1.2f, 3f), t.Height, 30);
                        break;
                    case SkillKind.BuffAttack:
                        Fx.Pillar(p, new Color(3f, 1.4f, 0.4f), t.Height * 1.3f, 40);
                        break;
                    default:
                        if (def.element == Element.Feuer) Fx.Pillar(p, new Color(3f, 1.1f, 0.3f), t.Height * 1.5f, 70);
                        else if (def.element == Element.Schatten) Fx.Burst(t.Center, new Color(1.4f, 0.4f, 2.2f), 60, 5f, 0.35f, 0.7f, 0f);
                        else if (def.element == Element.Natur || def.element == Element.Erde) Fx.Burst(p + Vector3.up * 0.1f, c * 2f, 50, 4f, 0.3f, 0.7f, 3f);
                        else Fx.Burst(t.Center, c * 2.5f, 45, 5f, 0.25f, 0.6f, 0.5f);
                        Fx.Sparks(t.Center, c * 2.5f, (t.transform.position - a.transform.position).normalized + Vector3.up * 0.5f, 25);
                        break;
                }
            }
            if (e.skill == SkillKind.Taunt) Fx.Ring(a.transform.position + Vector3.up * 0.1f, new Color(3f, 0.6f, 0.4f));
            if (offensive)
            {
                bool big = e.skill == SkillKind.AllEnemies || def.rarity >= Rarity.Episch || def.isBoss;
                Shake(big ? 0.4f : 0.2f, def.rarity >= Rarity.Legendaer || def.isBoss ? 0.32f : 0.18f);
                Sound.Play("krit", 0.8f, 0.8f);
                Color fc = c; fc.a = big ? 0.45f : 0.25f;
                hud.Flash(fc);
                if (!skipping) StartCoroutine(SlowMo(big ? 0.35f : 0.18f));
            }
            else Sound.Play(e.skill == SkillKind.ShieldAll || e.skill == SkillKind.Taunt ? "schild" : "heilung");
        }

        // ------------------------------------------------------------------
        // Ereignisse auf die Anzeige anwenden
        // ------------------------------------------------------------------

        void Apply(BattleEvent e, bool show)
        {
            switch (e.type)
            {
                case EvType.Damage:
                case EvType.BurnTick:
                case EvType.PoisonTick:
                {
                    float v = e.value;
                    float absorbed = Mathf.Min(shield[e.target], v);
                    shield[e.target] -= absorbed;
                    hp[e.target] = Mathf.Max(0f, hp[e.target] - (v - absorbed));
                    if (e.type == EvType.Damage) AddEnergy(e.target, BattleSim.EnergyPerHit);
                    UpdateBar(e.target);
                    if (!show) break;
                    var t = views[e.target];
                    if (e.type == EvType.PoisonTick)
                    {
                        Fx.Burst(t.Center, new Color(0.6f, 2.2f, 0.4f), 18, 1.5f, 0.2f, 0.6f, -0.6f);
                        hud.Float(t.Top, e.value.ToString(), new Color(0.6f, 1f, 0.35f), 36f);
                        t.Hit(t.transform.position + t.transform.forward, false);
                        break;
                    }
                    if (e.type == EvType.BurnTick)
                    {
                        Fx.Burst(t.Center, new Color(3f, 1.1f, 0.3f), 20, 2f, 0.2f, 0.5f, -1f);
                        hud.Float(t.Top, e.value.ToString(), new Color(1f, 0.6f, 0.2f), 38f);
                        t.Hit(t.transform.position + t.transform.forward, false);
                        break;
                    }
                    var from = views[e.actor];
                    string txt = e.value.ToString();
                    Color col = Color.white;
                    float size = 44f;
                    if (e.crit) { txt = "KRIT " + txt + "!"; col = new Color(1f, 0.85f, 0.2f); size = 58f; }
                    if (e.elem > 1f) { col = e.crit ? col : new Color(1f, 0.55f, 0.25f); hud.Float(t.Top + Vector3.up * 0.5f, "Effektiv!", new Color(1f, 0.55f, 0.25f), 30f); }
                    else if (e.elem < 1f) { col = new Color(0.7f, 0.75f, 0.8f); }
                    if (e.text == "Dornen") { txt = "Dornen " + e.value; col = new Color(0.6f, 1f, 0.5f); size = 34f; }
                    hud.Float(t.Top, txt, col, size);
                    t.Hit(from.transform.position, e.crit);
                    Fx.Burst(t.Center, Look.ElementColor(from.def.element) * 2f, e.crit ? 26 : 14, 3f, 0.15f, 0.35f, 0.5f);
                    Sound.Play(e.crit ? "krit" : "treffer", 0.8f, Random.Range(0.9f, 1.1f));
                    if (e.crit) Shake(0.15f, 0.12f);
                    break;
                }
                case EvType.Heal:
                    hp[e.target] = Mathf.Min(maxHp[e.target], hp[e.target] + e.value);
                    UpdateBar(e.target);
                    if (show && e.value > 0)
                    {
                        hud.Float(views[e.target].Top, "+" + e.value, new Color(0.45f, 1f, 0.55f), 36f);
                        views[e.target].Glow(new Color(0.5f, 1f, 0.6f));
                    }
                    break;
                case EvType.Shield:
                    shield[e.target] += e.value;
                    UpdateBar(e.target);
                    if (show) hud.Float(views[e.target].Top, "Schild", new Color(0.7f, 0.9f, 1f), 30f);
                    break;
                case EvType.Buff:
                    if (e.text == "Raserei")
                    {
                        if (!show) break;
                        var boss = views[e.target];
                        hud.Banner($"<color=#ff5a3c>RASEREI!</color>  {boss.def.StageName(boss.stage)} wird stärker", 1.1f);
                        hud.Flash(new Color(1f, 0.15f, 0.1f, 0.45f));
                        Fx.Ring(boss.transform.position + Vector3.up * 0.1f, new Color(3f, 0.4f, 0.2f));
                        Fx.Pillar(boss.transform.position, new Color(3f, 0.5f, 0.2f), boss.Height * 1.5f, 90);
                        Shake(0.6f, 0.3f);
                        Sound.Voice(boss.def, 1f);
                        boss.Glow(new Color(1f, 0.2f, 0.1f));
                        break;
                    }
                    if (show) hud.Float(views[e.target].Top, "Angriff +", new Color(1f, 0.6f, 0.3f), 30f);
                    break;
                case EvType.Debuff:
                    if (show) { hud.Float(views[e.target].Top, "Verteidigung −", new Color(0.8f, 0.6f, 1f), 28f); views[e.target].Glow(new Color(0.6f, 0.3f, 0.9f)); }
                    break;
                case EvType.Burn:
                    if (show) { hud.Float(views[e.target].Top, "Brennt!", new Color(1f, 0.5f, 0.2f), 32f); Fx.Pillar(views[e.target].transform.position, new Color(3f, 1f, 0.3f), 1.2f, 30); }
                    break;
                case EvType.Stun:
                    if (show) { hud.Float(views[e.target].Top, "Betäubt!", new Color(1f, 0.95f, 0.4f), 32f); Fx.Ring(views[e.target].Top - Vector3.up * 0.3f, new Color(3f, 2.6f, 0.6f)); }
                    break;
                case EvType.Stunned:
                    if (show) hud.Float(views[e.actor].Top, "setzt aus", new Color(1f, 0.95f, 0.6f), 30f);
                    break;
                case EvType.Frost:
                    if (show)
                    {
                        var ft = views[e.target];
                        hud.Float(ft.Top, "Vereist!", new Color(0.6f, 0.9f, 1f), 32f);
                        Fx.Burst(ft.Center, new Color(1.2f, 2.2f, 3f), 22, 2.5f, 0.15f, 0.6f, 0.3f);
                        Fx.Ring(ft.transform.position + Vector3.up * 0.05f, new Color(1.2f, 2.2f, 3f));
                        ft.Glow(new Color(0.5f, 0.8f, 1f));
                    }
                    break;
                case EvType.Poison:
                    if (show)
                    {
                        var pt = views[e.target];
                        hud.Float(pt.Top, e.value > 1 ? $"Gift ×{e.value}" : "Vergiftet!", new Color(0.65f, 1f, 0.35f), 30f);
                        Fx.Burst(pt.Center, new Color(0.7f, 2.4f, 0.4f), 16, 1.2f, 0.25f, 0.8f, -0.4f);
                        pt.Glow(new Color(0.5f, 1f, 0.3f));
                    }
                    break;
                case EvType.Phase:
                    if (show)
                    {
                        var boss = views[e.actor];
                        bool despair = e.text == "Verzweiflung";
                        hud.Banner(despair ? "<color=#ff3c6e>VERZWEIFLUNG!</color>  Der Boss schlägt um sich" : "<color=#8fd0ff>SCHILDWALL!</color>  Der Boss schützt sich", 1.1f);
                        hud.Flash(despair ? new Color(0.9f, 0.1f, 0.3f, 0.45f) : new Color(0.4f, 0.7f, 1f, 0.35f));
                        Fx.Ring(boss.transform.position + Vector3.up * 0.1f, despair ? new Color(3f, 0.3f, 0.5f) : new Color(0.8f, 1.6f, 3f));
                        Fx.Pillar(boss.transform.position, despair ? new Color(3f, 0.4f, 0.6f) : new Color(0.8f, 1.5f, 3f), boss.Height * 1.5f, 80);
                        Shake(despair ? 0.7f : 0.35f, 0.3f);
                        Sound.Voice(boss.def, 1f);
                    }
                    break;
                case EvType.Dodge:
                    if (show) { hud.Float(views[e.target].Top, "Ausgewichen", Color.white, 30f); Sound.Play("zisch", 0.5f, 1.5f); }
                    break;
                case EvType.Taunt:
                    if (show) hud.Float(views[e.actor].Top, "Provoziert!", new Color(1f, 0.45f, 0.35f), 32f);
                    break;
                case EvType.Death:
                    hp[e.target] = 0f;
                    UpdateBar(e.target);
                    views[e.target].Die();
                    if (show) Sound.Play("tod", 0.8f);
                    break;
            }
        }

        /// <summary>Kurze Zeitlupe beim Einschlag einer Spezialfähigkeit.</summary>
        IEnumerator SlowMo(float realSeconds)
        {
            Time.timeScale = Speed * 0.25f;
            yield return new WaitForSecondsRealtime(realSeconds);
            if (!finished) Time.timeScale = Speed;
        }

        void AddEnergy(int uid, float amount)
        {
            var u = result.units[uid];
            energy[uid] = Mathf.Min(100f, energy[uid] + amount * u.energyMul);
            UpdateBar(uid);
        }

        void UpdateBar(int uid) => hud.SetBar(uid, hp[uid], maxHp[uid], shield[uid], energy[uid], result.units[uid].def.isBoss);

        // ------------------------------------------------------------------
        // Ende
        // ------------------------------------------------------------------

        IEnumerator Finish()
        {
            finished = true;
            Time.timeScale = 1f;
            bool won = result.winner == 0;
            yield return new WaitForSeconds(0.6f);
            Zoom(new Vector3(0f, 0.5f, won ? -2.6f : 2.6f), 0.3f);
            foreach (var u in result.units)
            {
                var v = views[u.uid];
                if (v.IsDead) continue;
                if (u.team == result.winner) StartCoroutine(v.Victory());
                else StartCoroutine(v.Defeat());
            }
            Sound.Play(won ? "sieg" : "niederlage");
            hud.Banner(won ? "<color=#ffd23f>SIEG!</color>" : "NIEDERLAGE", 1.2f);
            if (won)
                foreach (var u in result.units)
                    if (u.team == 0 && u.Alive) Fx.Burst(views[u.uid].Top, new Color(3f, 2.4f, 0.8f), 30, 4f, 0.2f, 1f, 1f);

            if (arena != null)
            {
                // Arena: Wertung kommt vom Server, dazu etwas Gold
                bool serverWon = arena.serverWinner == 0;
                var rw = new Rewards { gold = serverWon ? 60 : 15 };
                Game.Data.gold += rw.gold;
                Game.Save();
                string sign = arena.delta >= 0 ? "+" : "";
                string extra = $"<color=#9fd3ff>Arena-Wertung {sign}{arena.delta} → {arena.rating} ({arena.rank})</color>";
                yield return new WaitForSeconds(1.8f);
                hud.ShowResult(serverWon, rw, result, () => GameFlow.ToMenu(MenuPage.Arena), null, extra);
            }
            else
            {
                var rw = BattleRewards.Apply(Game.Data, stage, result);
                Game.Save();
                yield return new WaitForSeconds(1.8f);
                hud.ShowResult(won, rw, result, () => GameFlow.ToMenu(MenuPage.Campaign), () => GameFlow.ToBattle());
            }
        }

        // ------------------------------------------------------------------
        // Kamera
        // ------------------------------------------------------------------

        void Zoom(Vector3 target, float amount)
        {
            if (amount > 0f) zoomTarget = target;
            zoomGoal = amount;
        }

        void Shake(float time, float power)
        {
            shakeUntil = Time.time + time;
            shakePower = power;
        }

        void LateUpdate()
        {
            if (cam == null) return;
            bool portrait = UX.Portrait;
            camFov = portrait ? 56f : 38f;
            cam.fieldOfView = camFov;

            // Abstand so wählen, dass beide Formationen ins Bild passen
            float halfV = camFov * 0.5f * Mathf.Deg2Rad;
            float halfH = Mathf.Atan(Mathf.Tan(halfV) * cam.aspect);
            // Hochkant: steiler und gerader von hinten, damit das Feld die Höhe nutzt
            float pitch = portrait ? 56f : CamPitch;
            float yaw = portrait ? 8f : CamYaw;
            float distW = (portrait ? 4.3f : 5.6f) / Mathf.Tan(halfH);
            float distD = (portrait ? 5.6f : 4.6f) / Mathf.Tan(halfV);
            camDist = Mathf.Max(10f, Mathf.Max(distW, distD));

            zoomAmount = Mathf.MoveTowards(zoomAmount, zoomGoal, Time.unscaledDeltaTime * 2.5f);
            Vector3 baseLook = portrait ? new Vector3(0f, 0.5f, -0.3f) : camLook;
            Vector3 look = Vector3.Lerp(baseLook, zoomTarget, zoomAmount);
            float dist = camDist * (1f - zoomAmount * 0.55f);

            Quaternion rot = Quaternion.Euler(pitch, yaw + introYaw, 0f);
            Vector3 pos = look - rot * Vector3.forward * dist;
            if (Time.time < shakeUntil)
                pos += Random.insideUnitSphere * shakePower;
            cam.transform.SetPositionAndRotation(pos, rot);
            ArenaBuilder.FocusAt(dist);
        }
    }
}
