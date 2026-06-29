using NUnit.Framework;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class LootTests
    {
        [Test]
        public void Wallet_adds_carried()
        {
            var w = new LootWallet();
            w.Add(3); w.Add(2);
            Assert.AreEqual(5, w.Carried);
            Assert.AreEqual(0, w.Banked);
        }

        [Test]
        public void Wallet_ignores_non_positive()
        {
            var w = new LootWallet();
            w.Add(0); w.Add(-5);
            Assert.AreEqual(0, w.Carried);
        }

        [Test]
        public void Bank_moves_carried_to_banked_and_returns_it()
        {
            var w = new LootWallet();
            w.Add(7);
            int banked = w.Bank();
            Assert.AreEqual(7, banked);
            Assert.AreEqual(0, w.Carried);
            Assert.AreEqual(7, w.Banked);
        }

        [Test]
        public void DropCarried_clears_carried_only()
        {
            var w = new LootWallet();
            w.Add(4); w.Bank(); w.Add(3);
            w.DropCarried();
            Assert.AreEqual(0, w.Carried);
            Assert.AreEqual(4, w.Banked);
        }

        [Test]
        public void ShouldDrop_respects_chance_bounds()
        {
            Assert.IsTrue(LootDrop.ShouldDrop(0.0, 1f));
            Assert.IsTrue(LootDrop.ShouldDrop(0.99, 1f));
            Assert.IsFalse(LootDrop.ShouldDrop(0.0, 0f));
            Assert.IsTrue(LootDrop.ShouldDrop(0.4, 0.5f));
            Assert.IsFalse(LootDrop.ShouldDrop(0.6, 0.5f));
        }

        [Test]
        public void Amount_within_range()
        {
            Assert.AreEqual(1, LootDrop.Amount(0.0, 1, 3));
            Assert.AreEqual(3, LootDrop.Amount(0.999, 1, 3));
            Assert.AreEqual(2, LootDrop.Amount(0.5, 1, 3));
            Assert.AreEqual(5, LootDrop.Amount(0.5, 5, 5)); // min==max
        }
    }
}
