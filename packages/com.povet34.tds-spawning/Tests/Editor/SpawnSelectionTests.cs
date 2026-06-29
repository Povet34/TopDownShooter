using System.Collections.Generic;
using NUnit.Framework;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class SpawnSelectionTests
    {
        [Test]
        public void Empty_returns_minus_one()
        {
            Assert.AreEqual(-1, SpawnSelection.PickIndex(new List<float>(), 0.5f));
        }

        [Test]
        public void Roll_zero_picks_first_positive_weight()
        {
            var w = new List<float> { 1f, 1f, 1f };
            Assert.AreEqual(0, SpawnSelection.PickIndex(w, 0f));
        }

        [Test]
        public void Roll_near_one_picks_last()
        {
            var w = new List<float> { 1f, 1f, 1f };
            Assert.AreEqual(2, SpawnSelection.PickIndex(w, 0.99f));
        }

        [Test]
        public void Respects_weight_proportions()
        {
            // [8, 2] -> roll 0.5 (target 5.0) < 8 => index 0; roll 0.9 (target 9.0) >= 8 => index 1
            var w = new List<float> { 8f, 2f };
            Assert.AreEqual(0, SpawnSelection.PickIndex(w, 0.5f));
            Assert.AreEqual(1, SpawnSelection.PickIndex(w, 0.9f));
        }

        [Test]
        public void Zero_weight_entries_are_skipped()
        {
            // [0, 5] -> any roll picks index 1
            var w = new List<float> { 0f, 5f };
            Assert.AreEqual(1, SpawnSelection.PickIndex(w, 0f));
            Assert.AreEqual(1, SpawnSelection.PickIndex(w, 0.99f));
        }
    }
}
