using System.Collections;
using System.Collections.Generic;
using Kesselwerk.Core;
using UnityEngine;

namespace Kesselwerk
{
    /// <summary>Choreografie der Attacken: jede Optik (MoveFx) hat einen eigenen Ablauf.</summary>
    public partial class AutoBattleManager
    {
        static readonly string[] RankNames = { "", "I", "II", "III", "IV", "V" };

        IEnumerator PlayMove(BattleEvent e, List<BattleEvent> group)
        {
            var a = views[e.actor];
            var def = a.def;
            energy[e.actor] = 0f;
            UpdateBar(e.actor);
            Color c = Look.ElementColor(def.element);
            int rank = Mathf.Clamp(e.rank, 1, 5);
            float big = 1f + 0.12f * (rank - 1);
            string rk = rank > 1 ? $"  <size=70%><color=#ffd23f>Rang {RankNames[rank]}</color></size>" : "";
            hud.Banner($"<color={Look.Hex(c)}>{def.StageName(a.stage)}</color>: {e.text}{rk}", 0.9f);
            Sound.Voice(def, 0.7f);

            // Ziele aus den Folgeereignissen
            var targets = new List<Creature>();
            foreach (var g in group)
                if ((g.type == EvType.Damage || g.type == EvType.Heal || g.type == EvType.Shield || g.type == EvType.Buff || g.type == EvType.Debuff) &&
                    g.actor == e.actor && views.TryGetValue(g.target, out var tv) && !targets.Contains(tv))
                    targets.Add(tv);
            Creature main = e.target >= 0 && views.ContainsKey(e.target) ? views[e.target] : (targets.Count > 0 ? targets[0] : null);
            bool offensive = !(e.skill == SkillKind.HealLowest || e.skill == SkillKind.HealAll || e.skill == SkillKind.ShieldAll ||
                               e.skill == SkillKind.Taunt || e.skill == SkillKind.BuffAttack);

            // Ereignisse einzeln je Ziel anwenden (für nacheinander einschlagende Effekte)
            var done = new bool[group.Count];
            System.Action<Creature> hitOne = t =>
            {
                for (int i = 0; i < group.Count; i++)
                    if (!done[i] && views.TryGetValue(group[i].target, out var v) && v == t) { done[i] = true; Apply(group[i], true); }
            };
            System.Action hitRest = () => { for (int i = 0; i < group.Count; i++) if (!done[i]) { done[i] = true; Apply(group[i], true); } };
            System.Action punch = () =>
            {
                if (!offensive) return;
                bool wide = targets.Count > 2 || def.isBoss;
                Shake(wide ? 0.4f : 0.22f, (def.rarity >= Rarity.Legendaer || def.isBoss ? 0.3f : 0.18f) * big);
                Color fc = c; fc.a = wide ? 0.4f : 0.22f;
                hud.Flash(fc);
                Sound.Play("krit", 0.8f, wide ? 0.7f : 0.9f);
                if (!skipping) StartCoroutine(SlowMo(wide ? 0.3f : 0.16f));
            };

            Vector3 enemySide = targets.Count > 0 ? Avg(targets) : a.transform.position + a.transform.forward * 3f;
            bool canLunge = main != null && main != a && !main.IsDead;

            switch (e.fx)
            {
                // ---------------- Nahkampf ----------------
                case MoveFx.Charge:
                    if (!canLunge) goto default;
                    Sound.Play("zauber", 0.6f, 1.2f);
                    Zoom(a.Center, 0.3f);
                    yield return a.Channel(MonsterAnimator.CastKeys, 0.35f, c, 0.12f);
                    yield return a.Release(0.06f);
                    Zoom(Vector3.zero, 0f);
                    yield return a.Lunge(main, MonsterAnimator.SlamKeys, () =>
                    {
                        Vector3 p = main.transform.position;
                        FxPlus.Shockwave(p, c, 2.4f * big, 0.45f);
                        FxPlus.Dust(p, 1.5f);
                        Fx.Burst(main.Center, c * 3f, 50, 6f, 0.3f, 0.5f, 0.6f);
                        main.Knock(a.transform.position, 0.6f);
                        hitRest(); punch();
                    }, true, 0.22f);
                    break;

                case MoveFx.Slash:
                    if (canLunge && !def.ranged && targets.Count <= 3 && e.skill != SkillKind.Chain)
                    {
                        Sound.Play("zisch", 0.7f, 1.3f);
                        yield return a.Lunge(main, MonsterAnimator.AttackKeys, () =>
                        {
                            foreach (var t in targets)
                                FxPlus.Slash(t.Center, t.transform.position - a.transform.position, c, t == main ? 3 + rank / 2 : 2, t.Height * 0.8f * big);
                            hitRest(); punch();
                        }, false, 0.16f);
                    }
                    else
                    {
                        // Klingen fliegen nacheinander zu den Zielen
                        yield return a.Face(enemySide);
                        a.PlayClip(MonsterAnimator.AttackKeys);
                        foreach (var t in targets)
                        {
                            if (t == a) continue;
                            Sound.Play("zisch", 0.7f, 1.4f);
                            yield return FxPlus.Shot(a.Center, t.Center, c, 0.16f, 0.18f, false);
                            FxPlus.Slash(t.Center, t.transform.position - a.transform.position, c, 2, t.Height * 0.8f * big);
                            hitOne(t);
                        }
                        hitRest(); punch();
                        yield return new WaitForSeconds(0.2f);
                        a.CastEnd();
                    }
                    break;

                case MoveFx.Bite:
                    if (!canLunge) goto default;
                    Sound.Play("zisch", 0.6f, 0.8f);
                    yield return a.Lunge(main, MonsterAnimator.AttackKeys, () =>
                    {
                        FxPlus.Bite(main.Center, c, main.Height * 0.9f * big);
                        Fx.Burst(main.Center, new Color(2.5f, 0.3f, 0.3f), 30, 4f, 0.2f, 0.4f, 1f);
                        if (e.skill == SkillKind.Drain) FxPlus.Drain(main.Center, a.Center, new Color(2.5f, 0.3f, 0.6f));
                        hitRest(); punch();
                    }, false, 0.14f);
                    break;

                // ---------------- Fernkampf ----------------
                case MoveFx.Projectile:
                    yield return a.Face(enemySide);
                    Zoom(a.Center, 0.25f);
                    Sound.Play("zauber", 0.7f, 1.1f);
                    FxPlus.Orb(a.Center + a.transform.forward * 0.5f + Vector3.up * 0.2f, c, 0.45f * big, 0.45f);
                    yield return a.Channel(MonsterAnimator.CastKeys, 0.45f, c, 0.1f);
                    Zoom(Vector3.zero, 0f);
                    a.PlayClip(MonsterAnimator.AttackKeys);
                    {
                        var shots = new List<Coroutine>();
                        foreach (var t in targets)
                            if (t != a) shots.Add(StartCoroutine(FxPlus.Shot(a.Center + a.transform.forward * 0.5f, t.Center, c, 0.32f, 0.32f * big)));
                        foreach (var s in shots) yield return s;
                    }
                    foreach (var t in targets) { FxPlus.Shockwave(t.transform.position, c, 1.6f, 0.35f); t.Knock(a.transform.position, 0.35f); }
                    hitRest(); punch();
                    yield return a.Release(0.15f);
                    a.CastEnd();
                    break;

                case MoveFx.Beam:
                    if (main == null) goto default;
                    yield return a.Face(main.transform.position);
                    Zoom(a.Center, 0.3f);
                    Sound.Play("zauber", 0.8f, 0.9f);
                    yield return a.Channel(MonsterAnimator.CastKeys, 0.45f, c, 0.15f);
                    Zoom((a.Center + main.Center) * 0.5f, 0.15f);
                    Sound.Play("zisch", 1f, 0.6f);
                    {
                        var beam = StartCoroutine(FxPlus.Beam(a.Center + a.transform.forward * 0.4f, main.Center, c, 0.55f, 0.55f * big));
                        yield return new WaitForSeconds(0.22f);
                        main.Knock(a.transform.position, 0.5f);
                        Fx.Burst(main.Center, c * 3f, 50, 6f, 0.3f, 0.5f, 0f);
                        hitRest(); punch();
                        yield return beam;
                    }
                    Zoom(Vector3.zero, 0f);
                    yield return a.Release(0.15f);
                    a.CastEnd();
                    break;

                case MoveFx.Nova:
                    yield return a.Face(enemySide);
                    Sound.Play("zauber", 0.8f, 0.8f);
                    yield return a.Channel(MonsterAnimator.CastKeys, 0.35f, c, 0.12f);
                    foreach (var t in targets) FxPlus.Orb(t.Center + Vector3.up * 0.2f, c, 0.7f * big, 0.45f);
                    yield return new WaitForSeconds(0.45f);
                    foreach (var t in targets)
                    {
                        Fx.Burst(t.Center, c * 3f, 70, 7f, 0.3f, 0.55f, 0f);
                        FxPlus.Shockwave(t.transform.position, c, 2.2f * big, 0.4f);
                        t.Knock(t.transform.position + Vector3.forward * 0.01f, 0.25f);
                    }
                    hitRest(); punch();
                    yield return a.Release(0.15f);
                    a.CastEnd();
                    break;

                case MoveFx.Rain:
                    yield return a.Face(enemySide);
                    Zoom(a.Center, 0.25f);
                    Sound.Play("zauber", 0.9f, 0.7f);
                    yield return a.Channel(MonsterAnimator.CastKeys, 0.5f, c, 0.3f);
                    Zoom(enemySide, 0.12f);
                    for (int w = 0; w < 1 + rank / 2; w++)
                    {
                        foreach (var t in targets)
                        {
                            StartCoroutine(FxPlus.Meteor(t.transform.position + new Vector3(Random.Range(-0.3f, 0.3f), 0.3f, Random.Range(-0.3f, 0.3f)), c, 0.35f * big));
                            yield return new WaitForSeconds(0.05f);
                        }
                        yield return new WaitForSeconds(0.12f);
                    }
                    yield return new WaitForSeconds(0.2f);
                    foreach (var t in targets) t.Knock(t.transform.position + Vector3.forward * 0.01f, 0.3f);
                    hitRest(); punch();
                    Zoom(Vector3.zero, 0f);
                    yield return a.Release(0.2f);
                    a.CastEnd();
                    break;

                case MoveFx.Lightning:
                {
                    yield return a.Face(enemySide);
                    Sound.Play("zauber", 0.7f, 1.4f);
                    yield return a.Channel(MonsterAnimator.CastKeys, 0.35f, c, 0.2f);
                    bool chain = e.skill == SkillKind.Chain;
                    Vector3 from = a.Center;
                    foreach (var t in targets)
                    {
                        if (t == a) continue;
                        Vector3 src = chain ? from : t.Center + Vector3.up * 9f;
                        Fx.Lightning(src, t.Center, c * 3f);
                        if (!chain) Fx.Lightning(t.Center + Vector3.up * 9f + Vector3.right * 0.6f, t.Center, Color.white * 2f);
                        FxPlus.Shockwave(t.transform.position, c, 1.5f, 0.3f);
                        Color fl = Color.white; fl.a = 0.25f; hud.Flash(fl);
                        Sound.Play("krit", 0.6f, 1.5f);
                        hitOne(t);
                        from = t.Center;
                        yield return new WaitForSeconds(0.13f);
                    }
                    hitRest(); punch();
                    yield return a.Release(0.15f);
                    a.CastEnd();
                    break;
                }

                // ---------------- Flächen ----------------
                case MoveFx.Quake:
                {
                    Sound.Play("zauber", 0.6f, 0.6f);
                    yield return a.Face(enemySide);
                    float len = a.PlayClip(MonsterAnimator.SlamKeys);
                    yield return new WaitForSeconds(Mathf.Clamp(len * 0.5f, 0.25f, 0.55f));
                    Vector3 p0 = a.transform.position;
                    FxPlus.Shockwave(p0, new Color(0.9f, 0.7f, 0.4f), 6f * big, 0.6f);
                    FxPlus.Dust(p0, 2f);
                    Shake(0.5f, 0.35f * big);
                    Sound.Play("treffer", 1f, 0.5f);
                    yield return new WaitForSeconds(0.18f);
                    foreach (var t in targets)
                    {
                        FxPlus.Spikes(t.transform.position, new Color(0.75f, 0.55f, 0.35f), 5, 0.8f * big);
                        t.Knock(t.transform.position + Vector3.forward * 0.01f, 0.4f);
                        hitOne(t);
                        yield return new WaitForSeconds(0.04f);
                    }
                    hitRest(); punch();
                    yield return new WaitForSeconds(0.25f);
                    a.CastEnd();
                    break;
                }

                case MoveFx.Spikes:
                    yield return a.Face(enemySide);
                    Sound.Play("zauber", 0.6f, 0.9f);
                    yield return a.Channel(MonsterAnimator.SlamKeys, 0.4f, c, 0.05f);
                    foreach (var t in targets)
                    {
                        FxPlus.Spikes(t.transform.position, c, t == main ? 9 : 6, 1.2f * big);
                        t.Knock(t.transform.position + Vector3.forward * 0.01f, 0.45f);
                        hitOne(t);
                        yield return new WaitForSeconds(0.06f);
                    }
                    hitRest(); punch();
                    yield return a.Release(0.2f);
                    a.CastEnd();
                    break;

                case MoveFx.Vortex:
                {
                    yield return a.Face(enemySide);
                    Sound.Play("zisch", 1f, 0.5f);
                    yield return a.Channel(MonsterAnimator.CastKeys, 0.35f, c, 0.2f);
                    Vector3 start = a.transform.position + a.transform.forward * 0.8f;
                    var path = new List<Vector3>();
                    foreach (var t in targets) path.Add(t.transform.position);
                    path.Sort((x, y) => x.x.CompareTo(y.x));
                    Vector3 prev = start;
                    foreach (var p in path)
                    {
                        yield return FxPlus.Tornado(prev, p, c * 0.8f + Color.white * 0.4f, 0.22f);
                        foreach (var t in targets) if (t.transform.position == p) { t.Knock(prev, 0.5f); hitOne(t); }
                        prev = p;
                    }
                    hitRest(); punch();
                    yield return a.Release(0.15f);
                    a.CastEnd();
                    break;
                }

                case MoveFx.Wave:
                {
                    yield return a.Face(enemySide);
                    Sound.Play("zauber", 0.7f, 0.8f);
                    yield return a.Channel(MonsterAnimator.CastKeys, 0.35f, c, 0.2f);
                    Sound.Play("zisch", 1f, 0.6f);
                    Vector3 from = a.transform.position, to = enemySide;
                    for (float t = 0f; t < 1f; t += 0.2f)
                    {
                        Fx.Wave(Vector3.Lerp(from, to, t) + Vector3.up * 0.2f, 7f, c * 2.2f);
                        yield return new WaitForSeconds(0.06f);
                    }
                    foreach (var t in targets) { t.Knock(a.transform.position, 0.55f); FxPlus.Shockwave(t.transform.position, c, 1.5f, 0.35f); }
                    hitRest(); punch();
                    yield return a.Release(0.15f);
                    a.CastEnd();
                    break;
                }

                case MoveFx.Blizzard:
                    yield return a.Face(enemySide);
                    Sound.Play("zauber", 0.8f, 1.3f);
                    yield return a.Channel(MonsterAnimator.CastKeys, 0.45f, c, 0.25f);
                    foreach (var t in targets) FxPlus.Blizzard(t.transform.position, new Color(0.7f, 0.9f, 1f));
                    yield return new WaitForSeconds(0.3f);
                    foreach (var t in targets)
                    {
                        FxPlus.Spikes(t.transform.position, new Color(0.6f, 0.85f, 1.2f), 5, 0.7f * big);
                        hitOne(t);
                        yield return new WaitForSeconds(0.04f);
                    }
                    hitRest(); punch();
                    yield return a.Release(0.2f);
                    a.CastEnd();
                    break;

                case MoveFx.Cloud:
                {
                    yield return a.Face(enemySide);
                    Sound.Play("zisch", 0.8f, 0.4f);
                    a.PlayClip(MonsterAnimator.RoarKeys);
                    Shake(0.3f, 0.08f);
                    yield return new WaitForSeconds(0.3f);
                    Color cc = e.skill == SkillKind.Poison ? new Color(0.5f, 1.2f, 0.3f) : new Color(0.8f, 0.3f, 1.2f);
                    foreach (var t in targets) { FxPlus.Cloud(t.transform.position, cc, 0.7f); hitOne(t); }
                    yield return new WaitForSeconds(0.35f);
                    hitRest(); punch();
                    yield return new WaitForSeconds(0.2f);
                    a.CastEnd();
                    break;
                }

                // ---------------- Unterstützung ----------------
                case MoveFx.HealAura:
                    Zoom(a.Center, 0.25f);
                    Sound.Play("heilung");
                    yield return a.Channel(MonsterAnimator.CastKeys, 0.45f, new Color(0.5f, 1f, 0.6f), 0.3f);
                    Zoom(Vector3.zero, 0f);
                    foreach (var t in targets)
                    {
                        FxPlus.Heal(t.transform.position, new Color(0.45f, 1.1f, 0.6f), t.Height);
                        hitOne(t);
                        yield return new WaitForSeconds(0.05f);
                    }
                    hitRest();
                    yield return a.Release(0.25f);
                    a.CastEnd();
                    break;

                case MoveFx.ShieldDome:
                    Sound.Play("schild");
                    yield return a.Channel(MonsterAnimator.CastKeys, 0.35f, new Color(0.5f, 0.8f, 1.3f), 0.2f);
                    if (e.skill == SkillKind.Taunt)
                    {
                        a.PlayClip(MonsterAnimator.RoarKeys);
                        FxPlus.Shockwave(a.transform.position, new Color(1.5f, 0.5f, 0.3f), 4f, 0.5f);
                        Shake(0.3f, 0.12f);
                    }
                    foreach (var t in targets) { FxPlus.Dome(t.transform.position, new Color(0.4f, 0.75f, 1.4f), Mathf.Max(0.6f, t.Height * 0.55f)); hitOne(t); }
                    hitRest();
                    yield return new WaitForSeconds(0.25f);
                    yield return a.Release(0.2f);
                    a.CastEnd();
                    break;

                case MoveFx.Roar:
                {
                    Zoom(a.Center, 0.35f);
                    float len = a.PlayClip(MonsterAnimator.RoarKeys);
                    yield return new WaitForSeconds(Mathf.Clamp(len * 0.4f, 0.2f, 0.45f));
                    Sound.Play("zauber", 1f, 0.5f);
                    Shake(0.45f, 0.2f);
                    FxPlus.Shockwave(a.transform.position, new Color(2f, 1f, 0.3f), 7f, 0.55f);
                    Color fl = new Color(1f, 0.6f, 0.2f, 0.18f); hud.Flash(fl);
                    Zoom(Vector3.zero, 0f);
                    yield return new WaitForSeconds(0.15f);
                    foreach (var t in targets) { Fx.Pillar(t.transform.position, new Color(3f, 1.4f, 0.4f), t.Height * 1.4f, 45); t.Glow(new Color(1f, 0.6f, 0.2f)); hitOne(t); }
                    hitRest();
                    yield return new WaitForSeconds(0.3f);
                    a.CastEnd();
                    break;
                }

                default:
                    for (int i = 0; i < done.Length; i++) done[i] = true; // PlaySkill wendet selbst an
                    yield return PlaySkill(e, group);
                    break;
            }
            hitRest();
        }

        static Vector3 Avg(List<Creature> list)
        {
            Vector3 s = Vector3.zero;
            foreach (var c in list) s += c.transform.position;
            return s / Mathf.Max(1, list.Count);
        }
    }
}
