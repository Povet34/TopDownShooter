using NUnit.Framework;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class StashUpgradesTests
    {
        [Test]
        public void Cost_scales_linearly_with_level()
        {
            var u = StashUpgrades.Default();
            Assert.AreEqual(30, u.CostOf("vitality"));   // level 0 → base
            u.Purchase("vitality");
            Assert.AreEqual(60, u.CostOf("vitality"));   // level 1 → 2×base
            u.Purchase("vitality");
            Assert.AreEqual(90, u.CostOf("vitality"));
        }

        [Test]
        public void Purchase_raises_level_and_bonus()
        {
            var u = StashUpgrades.Default();
            Assert.AreEqual(0f, u.TotalBonus("vitality"), 0.001f);
            u.Purchase("vitality");
            u.Purchase("vitality");
            Assert.AreEqual(2, u.LevelOf("vitality"));
            Assert.AreEqual(50f, u.TotalBonus("vitality"), 0.001f); // 2 × 25 HP
        }

        [Test]
        public void Cannot_exceed_max_level()
        {
            var u = StashUpgrades.Default();
            for (int i = 0; i < 10; i++) u.Purchase("padding"); // max 4
            Assert.AreEqual(4, u.LevelOf("padding"));
            Assert.IsTrue(u.IsMaxed("padding"));
            Assert.AreEqual(0, u.CostOf("padding"));         // 최대치면 비용 0
            Assert.IsFalse(u.Purchase("padding"));
        }

        [Test]
        public void CanAfford_respects_currency_and_max()
        {
            var u = StashUpgrades.Default();
            Assert.IsFalse(u.CanAfford("swiftness", 39));
            Assert.IsTrue(u.CanAfford("swiftness", 40));
            Assert.IsFalse(u.CanAfford("nonexistent", 9999));
        }

        [Test]
        public void Levels_round_trip_through_serialize()
        {
            var u = StashUpgrades.Default();
            u.Purchase("vitality"); u.Purchase("vitality"); u.Purchase("swiftness");

            var back = StashUpgrades.Default();
            back.LoadLevels(u.SerializeLevels());
            Assert.AreEqual(2, back.LevelOf("vitality"));
            Assert.AreEqual(1, back.LevelOf("swiftness"));
            Assert.AreEqual(0, back.LevelOf("padding"));
        }

        [Test]
        public void LoadLevels_ignores_garbage_and_unknown_ids()
        {
            var u = StashUpgrades.Default();
            u.LoadLevels("vitality:3;bogus:9;junk");
            Assert.AreEqual(3, u.LevelOf("vitality"));
            Assert.AreEqual(0, u.LevelOf("bogus"));
        }
    }
}
