using System.Collections.Generic;
using Kesselwerk.Core;
using NUnit.Framework;

namespace Kesselwerk.Tests
{
    [TestFixture]
    public class ElementTests
    {
        [Test]
        public void WasserSchlaegtFeuer()
        {
            Assert.AreEqual(1.5f, ElementChart.Multiplier(Element.Wasser, Element.Feuer));
            Assert.AreEqual(0.75f, ElementChart.Multiplier(Element.Feuer, Element.Wasser));
        }

        [Test]
        public void LichtUndSchattenSindGegenseitigStark()
        {
            Assert.AreEqual(1.5f, ElementChart.Multiplier(Element.Licht, Element.Schatten));
            Assert.AreEqual(1.5f, ElementChart.Multiplier(Element.Schatten, Element.Licht));
        }

        [Test]
        public void NeutraleKombination()
        {
            Assert.AreEqual(1f, ElementChart.Multiplier(Element.Feuer, Element.Luft));
        }

        [Test]
        public void KeineWidersprueche()
        {
            for (int a = 0; a < 10; a++)
                for (int b = 0; b < 10; b++)
                {
                    var ea = (Element)a; var eb = (Element)b;
                    bool mutual = ElementChart.Beats(ea, eb) && ElementChart.Beats(eb, ea);
                    bool lightShadow = (ea == Element.Licht && eb == Element.Schatten) || (ea == Element.Schatten && eb == Element.Licht);
                    Assert.IsFalse(mutual && !lightShadow, $"{ea} und {eb} schlagen sich gegenseitig");
                    Assert.IsFalse(a == b && ElementChart.Beats(ea, eb), $"{ea} schlägt sich selbst");
                }
        }
    }

    [TestFixture]
    public class BattleTests
    {
        static TeamSpec Starter(int level) => new TeamSpec()
            .Add(new UnitSpec("krebs", level, 0)).Add(new UnitSpec("moos", level, 1))
            .Add(new UnitSpec("biene", level, 3)).Add(new UnitSpec("knochen", level, 4)).Add(new UnitSpec("hirsch", level, 5));

        static TeamSpec Mixed(int level) => new TeamSpec()
            .Add(new UnitSpec("yeti", level, 1)).Add(new UnitSpec("glut", level, 0)).Add(new UnitSpec("panda", level, 2))
            .Add(new UnitSpec("zapf", level, 3)).Add(new UnitSpec("stern", level, 4)).Add(new UnitSpec("gift", level, 5));

        [Test]
        public void GleicherStartwertGleichesErgebnis()
        {
            var a = BattleSim.Run(Starter(5), Mixed(5), 42);
            var b = BattleSim.Run(Starter(5), Mixed(5), 42);
            Assert.AreEqual(a.winner, b.winner);
            Assert.AreEqual(a.events.Count, b.events.Count);
            for (int i = 0; i < a.events.Count; i++)
                Assert.AreEqual(a.events[i].ToString(), b.events[i].ToString(), "Ereignis " + i);
        }

        [Test]
        public void KampfEndetMitSiegerUndEndeEreignis()
        {
            for (int seed = 1; seed <= 30; seed++)
            {
                var r = BattleSim.Run(Starter(3), Mixed(3), seed);
                Assert.IsTrue(r.winner == 0 || r.winner == 1);
                Assert.AreEqual(EvType.End, r.events[r.events.Count - 1].type);
                if (!r.timeout) Assert.AreEqual(0, r.Alive(1 - r.winner), "Verlierer hat noch Kreaturen");
            }
        }

        [Test]
        public void JedeKreaturStirbtHoechstensEinmal()
        {
            for (int seed = 1; seed <= 30; seed++)
            {
                var r = BattleSim.Run(Starter(4), Mixed(4), seed);
                var dead = new HashSet<int>();
                foreach (var e in r.events)
                    if (e.type == EvType.Death) Assert.IsTrue(dead.Add(e.target), "doppelter Tod");
            }
        }

        [Test]
        public void TotesMonsterHandeltNicht()
        {
            var r = BattleSim.Run(Starter(4), Mixed(4), 7);
            var dead = new HashSet<int>();
            foreach (var e in r.events)
            {
                if (e.type == EvType.Death) dead.Add(e.target);
                if (e.type == EvType.Attack || e.type == EvType.Skill)
                    Assert.IsFalse(dead.Contains(e.actor), "Tote Kreatur greift an");
            }
        }

        [Test]
        public void StaerkeresTeamGewinntMeistens()
        {
            int wins = 0;
            for (int seed = 1; seed <= 20; seed++)
                if (BattleSim.Run(Starter(25), Starter(1), seed).winner == 0) wins++;
            Assert.AreEqual(20, wins);
        }

        [Test]
        public void NahkaempferGreiftVordereReiheAn()
        {
            var player = new TeamSpec().Add(new UnitSpec("knochen", 1, 1));
            var enemy = new TeamSpec().Add(new UnitSpec("pieksi", 30, 0)).Add(new UnitSpec("hirsch", 1, 4));
            var r = BattleSim.Run(player, enemy, 3);
            foreach (var e in r.events)
            {
                if (e.type == EvType.Attack && e.actor == 0)
                {
                    Assert.AreEqual("pieksi", r.units[e.target].def.id);
                    return;
                }
            }
            Assert.IsTrue(false, "Kein Angriff gefunden");
        }

        [Test]
        public void ProvozierenZiehtAngriffeAn()
        {
            var player = new TeamSpec().Add(new UnitSpec("biene", 1, 3)).Add(new UnitSpec("biene", 1, 4));
            var enemy = new TeamSpec().Add(new UnitSpec("krebs", 40, 0)).Add(new UnitSpec("hirsch", 1, 4));
            var r = BattleSim.Run(player, enemy, 5);
            int krebs = r.units.Find(u => u.def.id == "krebs").uid;
            bool taunting = false;
            foreach (var e in r.events)
            {
                if (e.type == EvType.Taunt && e.actor == krebs) { taunting = true; continue; }
                if (taunting && e.type == EvType.Attack && r.units[e.actor].team == 0)
                {
                    Assert.AreEqual(krebs, e.target, "Angriff trotz Provokation nicht auf den Krebs");
                    return;
                }
            }
        }

        [Test]
        public void SpezialfaehigkeitWirdAusgeloest()
        {
            var r = BattleSim.Run(Starter(5), Mixed(5), 11);
            bool skill = false;
            foreach (var e in r.events) if (e.type == EvType.Skill) skill = true;
            Assert.IsTrue(skill);
        }

        [Test]
        public void ElementvorteilErhoehtSchaden()
        {
            // gleicher Angreifer, Ziele mit identischen Werten außer Element: Vergleich über Ereignisse
            var r = BattleSim.Run(new TeamSpec().Add(new UnitSpec("krebs", 10, 0)), new TeamSpec().Add(new UnitSpec("glut", 1, 0)), 9);
            foreach (var e in r.events)
                if (e.type == EvType.Damage && e.actor == 0 && e.text == null)
                {
                    Assert.AreEqual(1.5f, e.elem);
                    return;
                }
        }

        [Test]
        public void UngueltigesTeamWirdErkannt()
        {
            var t = new TeamSpec().Add(new UnitSpec("krebs", 1, 0)).Add(new UnitSpec("moos", 1, 0));
            Assert.IsFalse(t.IsValid(out _));
            Assert.IsFalse(new TeamSpec().IsValid(out _));
            Assert.IsFalse(new TeamSpec().Add(new UnitSpec("gibtsnicht", 1, 0)).IsValid(out _));
            Assert.IsTrue(Starter(1).IsValid(out _));
        }
    }

    [TestFixture]
    public class SynergyTests
    {
        [Test]
        public void ZweiNaturErgebenNaturII()
        {
            var t = new TeamSpec().Add(new UnitSpec("moos", 1, 0)).Add(new UnitSpec("hirsch", 1, 3));
            var list = Synergies.For(t);
            Assert.IsTrue(list.Exists(s => s.name == "Natur II"));
        }

        [Test]
        public void GleicheKreaturZaehltNurEinmal()
        {
            var t = new TeamSpec().Add(new UnitSpec("moos", 1, 0)).Add(new UnitSpec("moos", 1, 1));
            Assert.IsFalse(Synergies.For(t).Exists(s => s.name.StartsWith("Natur")));
        }

        [Test]
        public void TankUndHeilerErgebenBollwerk()
        {
            var t = new TeamSpec().Add(new UnitSpec("krebs", 1, 0)).Add(new UnitSpec("hirsch", 1, 3));
            Assert.IsTrue(Synergies.For(t).Exists(s => s.name == "Bollwerk"));
        }
    }

    [TestFixture]
    public class ProgressTests
    {
        [Test]
        public void NeuesSpielHatGueltigesTeam()
        {
            var p = PlayerData.NewGame();
            Assert.IsTrue(p.BuildTeam().IsValid(out var err), err);
            Assert.AreEqual(5, p.TeamCount());
        }

        [Test]
        public void ErfahrungSteigertStufeBisZurGrenze()
        {
            var c = new OwnedCreature { id = "krebs" };
            Progress.AddXp(c, 100000);
            Assert.AreEqual(Stats.MaxLevel(1), c.level);
            Assert.AreEqual(0, c.xp);
        }

        [Test]
        public void FusionBrauchtKopien()
        {
            var p = PlayerData.NewGame();
            var c = p.Get("krebs");
            Assert.IsFalse(Progress.Fuse(p, c, out _));
            c.copies = 2;
            Assert.IsTrue(Progress.Fuse(p, c, out var err), err);
            Assert.AreEqual(2, c.stars);
            Assert.AreEqual(0, c.copies);
        }

        [Test]
        public void EntwicklungBrauchtStufe()
        {
            var p = PlayerData.NewGame();
            var c = p.Get("moos");
            p.evoStones = 99; p.gold = 99999;
            Assert.IsFalse(Progress.Evolve(p, c, out _));
            c.level = Progress.EvolveLevel;
            Assert.IsTrue(Progress.Evolve(p, c, out var err), err);
            Assert.AreEqual("Hainriese", c.Name);
        }

        [Test]
        public void TeamfeldTauschVerhindertDoppelte()
        {
            var p = PlayerData.NewGame();
            p.SetTeamSlot(2, "krebs");
            int count = 0;
            foreach (var id in p.team) if (id == "krebs") count++;
            Assert.AreEqual(1, count);
            Assert.AreEqual("krebs", p.team[2]);
        }

        [Test]
        public void LoginBelohnungNurEinmalProTag()
        {
            var p = PlayerData.NewGame();
            int before = p.crystals;
            Assert.Greater(Progress.DailyLogin(p, 100), 0);
            Assert.AreEqual(0, Progress.DailyLogin(p, 100));
            Assert.Greater(p.crystals, before);
            Progress.DailyLogin(p, 101);
            Assert.AreEqual(2, p.loginStreak);
        }
    }

    [TestFixture]
    public class SummonTests
    {
        [Test]
        public void ChancenErgeben100Prozent()
        {
            float sum = 0f;
            foreach (var r in Summon.Rates) sum += r;
            Assert.AreEqual(100.0, sum, 0.001);
        }

        [Test]
        public void ZehnerBeschwoerungGarantiertSelten()
        {
            for (int seed = 1; seed <= 50; seed++)
            {
                var p = PlayerData.NewGame();
                p.crystals = Summon.CostTen;
                Assert.IsTrue(Summon.Pull(p, 10, new Rng(seed), out var res, out var err), err);
                Assert.AreEqual(10, res.Count);
                Assert.AreEqual(0, p.crystals);
                Assert.IsTrue(res.Exists(r => r.def.rarity >= Rarity.Selten), "Seed " + seed);
            }
        }

        [Test]
        public void ZuWenigKristalle()
        {
            var p = PlayerData.NewGame();
            p.crystals = 50;
            Assert.IsFalse(Summon.Pull(p, 1, new Rng(1), out _, out _));
            Assert.AreEqual(50, p.crystals);
        }

        [Test]
        public void DoppelteWerdenZuKopien()
        {
            var p = PlayerData.NewGame();
            p.crystals = 100000;
            Summon.Pull(p, 100, new Rng(3), out _, out _);
            var ids = new HashSet<string>();
            foreach (var c in p.creatures) Assert.IsTrue(ids.Add(c.id), "Kreatur doppelt in Sammlung");
            int copies = 0;
            foreach (var c in p.creatures) copies += c.copies;
            Assert.Greater(copies, 0);
        }

        [Test]
        public void KeineBosseAusBeschwoerung()
        {
            var p = PlayerData.NewGame();
            p.crystals = 1000000;
            Summon.Pull(p, 1000, new Rng(8), out var res, out _);
            Assert.IsFalse(res.Exists(r => r.def.isBoss));
        }

        [Test]
        public void HarteGarantieLegendaer()
        {
            var p = PlayerData.NewGame();
            p.crystals = 1000000;
            Summon.Pull(p, Summon.PityLegend, new Rng(12345), out var res, out _);
            Assert.IsTrue(res.Exists(r => r.def.rarity >= Rarity.Legendaer));
        }
    }

    [TestFixture]
    public class CampaignTests
    {
        [Test]
        public void AlleStufenHabenGueltigeGegner()
        {
            for (int r = 0; r < Campaign.RegionCount; r++)
                for (int i = 0; i < Campaign.StagesPerRegion; i++)
                {
                    var s = Campaign.Get(r, i);
                    Assert.IsTrue(s.enemy.IsValid(out var err), s.name + ": " + err);
                }
        }

        [Test]
        public void BossStufeHatBoss()
        {
            var s = Campaign.Get(0, Campaign.StagesPerRegion - 1);
            Assert.IsTrue(s.boss);
            Assert.IsTrue(s.enemy.units.Exists(u => CreatureDb.Get(u.id).isBoss));
        }

        [Test]
        public void ErsteStufeMitStartteamSchaffbar()
        {
            var p = PlayerData.NewGame();
            int wins = 0;
            for (int seed = 1; seed <= 20; seed++)
                if (BattleSim.Run(p.BuildTeam(), Campaign.Get(0, 0).enemy, seed).winner == 0) wins++;
            Assert.Greater(wins, 15, "Stufe 1-1 zu schwer");
        }

        [Test]
        public void SiegSchaltetNaechsteStufeFrei()
        {
            var p = PlayerData.NewGame();
            var stage = Campaign.Get(0, 0);
            Assert.IsFalse(Campaign.IsUnlocked(p, 0, 1));
            var fake = new BattleResult { winner = 0 };
            var rw = BattleRewards.Apply(p, stage, fake);
            Assert.IsTrue(rw.firstClear);
            Assert.IsTrue(Campaign.IsUnlocked(p, 0, 1));
            Assert.Greater(rw.gold, 0);
            // zweiter Sieg gibt keine Erstbelohnung mehr
            var rw2 = BattleRewards.Apply(p, stage, fake);
            Assert.IsFalse(rw2.firstClear);
        }

        [Test]
        public void ZweiteRegionErstNachDerErsten()
        {
            var p = PlayerData.NewGame();
            Assert.IsFalse(Campaign.IsUnlocked(p, 1, 0));
            p.regionProgress[0] = Campaign.StagesPerRegion;
            Assert.IsTrue(Campaign.IsUnlocked(p, 1, 0));
        }
    }
}

namespace Kesselwerk.Tests
{
    [TestFixture]
    public class GearTests
    {
        [Test]
        public void AusruestungErhoehtWerte()
        {
            var d = CreatureDb.Get("krebs");
            var ohne = new UnitSpec("krebs", 10, 0);
            var mit = new UnitSpec("krebs", 10, 0) { weapon = "w_drache", armor = "r_titan", armorLv = 5 };
            Assert.Greater(Stats.Atk(d, mit), Stats.Atk(d, ohne));
            Assert.Greater(Stats.Hp(d, mit), Stats.Hp(d, ohne));
            Assert.Greater(Stats.Power(d, mit), Stats.Power(d, ohne));
        }

        [Test]
        public void AnlegenNimmtEsDemAnderenAb()
        {
            var p = PlayerData.NewGame();
            var w = p.gear.Find(g => g.Def.slot == GearSlot.Waffe);
            var krebs = p.Get("krebs");
            var moos = p.Get("moos");
            Assert.IsTrue(Progress.Equip(p, krebs, w.uid, out _));
            Assert.IsTrue(Progress.Equip(p, moos, w.uid, out _));
            Assert.AreEqual(0, krebs.weaponUid);
            Assert.AreEqual(w.uid, moos.weaponUid);
            Assert.AreEqual("w_knueppel", p.BuildTeam().units.Find(u => u.id == "moos").weapon);
        }

        [Test]
        public void FalscheAusruestungWirdAbgelehnt()
        {
            var t = new TeamSpec().Add(new UnitSpec("krebs", 1, 0) { weapon = "r_titan" });
            Assert.IsFalse(t.IsValid(out _));
            var t2 = new TeamSpec().Add(new UnitSpec("krebs", 1, 0) { weapon = "w_drache", weaponLv = 9 });
            Assert.IsFalse(t2.IsValid(out _));
            var t3 = new TeamSpec().Add(new UnitSpec("krebs", 25, 0));
            Assert.IsFalse(t3.IsValid(out _), "Stufe 25 mit 1 Stern");
        }

        [Test]
        public void EliteGibtBeimErstenSiegAusruestung()
        {
            var p = PlayerData.NewGame();
            int before = p.gear.Count;
            p.regionProgress[0] = 3;
            var rw = BattleRewards.Apply(p, Campaign.Get(0, 3), new BattleResult { winner = 0, seed = 5 });
            Assert.AreEqual(1, rw.gear.Count);
            Assert.AreEqual(before + 1, p.gear.Count);
        }

        [Test]
        public void AufwertenKostetGold()
        {
            var p = PlayerData.NewGame();
            var g = p.gear[0];
            int gold = p.gold;
            Assert.IsTrue(Progress.UpgradeGear(p, g, out _));
            Assert.AreEqual(2, g.level);
            Assert.Less(p.gold, gold);
        }

        [Test]
        public void BossWirdRasend()
        {
            var player = new TeamSpec().Add(new UnitSpec("donner", 40, 3, 4)).Add(new UnitSpec("zapf", 40, 4, 4)).Add(new UnitSpec("yeti", 40, 1, 4));
            var r = BattleSim.Run(player, Campaign.Get(0, 7).enemy, 3);
            Assert.IsTrue(r.events.Exists(e => e.type == EvType.Buff && e.text == "Raserei"));
        }

        [Test]
        public void VierRegionenMitBoss()
        {
            Assert.AreEqual(4, Campaign.RegionCount);
            for (int r = 0; r < Campaign.RegionCount; r++)
                Assert.IsTrue(Campaign.Get(r, Campaign.StagesPerRegion - 1).enemy.units.Exists(u => CreatureDb.Get(u.id).isBoss));
        }
    }
}
