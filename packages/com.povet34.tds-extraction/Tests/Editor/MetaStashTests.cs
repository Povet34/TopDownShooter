using NUnit.Framework;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class MetaStashTests
    {
        [Test]
        public void Adds_currency_and_items()
        {
            var s = new MetaStash();
            s.AddCurrency(30);
            s.AddCurrency(20);
            s.AddItem("Salvage", 2);
            s.AddItem("Salvage");
            s.AddItem("Scrap");
            Assert.AreEqual(50, s.Currency);
            Assert.AreEqual(3, s.Items["Salvage"]);
            Assert.AreEqual(4, s.TotalItemCount);
        }

        [Test]
        public void Ignores_non_positive_and_empty()
        {
            var s = new MetaStash();
            s.AddCurrency(-5);
            s.AddItem("", 3);
            s.AddItem("x", 0);
            s.AddItem(null);
            Assert.AreEqual(0, s.Currency);
            Assert.AreEqual(0, s.TotalItemCount);
        }

        [Test]
        public void Serialize_deserialize_round_trips()
        {
            var s = new MetaStash();
            s.AddCurrency(123);
            s.AddItem("Salvage", 2);
            s.AddItem("Parts", 5);

            var back = MetaStash.Deserialize(s.Serialize());
            Assert.AreEqual(123, back.Currency);
            Assert.AreEqual(2, back.Items["Salvage"]);
            Assert.AreEqual(5, back.Items["Parts"]);
            Assert.AreEqual(7, back.TotalItemCount);
        }

        [Test]
        public void Deserialize_handles_empty_and_garbage()
        {
            Assert.AreEqual(0, MetaStash.Deserialize(null).TotalItemCount);
            Assert.AreEqual(0, MetaStash.Deserialize("").Currency);
            Assert.AreEqual(0, MetaStash.Deserialize("not valid").Currency);
            Assert.AreEqual(0, MetaStash.Deserialize("99").Currency); // no '|'
            // 통화만, 아이템 없음
            var s = MetaStash.Deserialize("42|");
            Assert.AreEqual(42, s.Currency);
            Assert.AreEqual(0, s.TotalItemCount);
        }

        [Test]
        public void TrySpend_deducts_only_when_affordable()
        {
            var s = new MetaStash();
            s.AddCurrency(100);
            Assert.IsFalse(s.TrySpend(150)); // 부족 → 변화 없음
            Assert.AreEqual(100, s.Currency);
            Assert.IsTrue(s.TrySpend(40));
            Assert.AreEqual(60, s.Currency);
            Assert.IsFalse(s.TrySpend(0));   // 비양수 거부
        }

        [Test]
        public void Clear_resets()
        {
            var s = new MetaStash();
            s.AddCurrency(10); s.AddItem("a");
            s.Clear();
            Assert.AreEqual(0, s.Currency);
            Assert.AreEqual(0, s.TotalItemCount);
        }
    }
}
