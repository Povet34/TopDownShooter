using NUnit.Framework;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class InsuranceTests
    {
        [Test]
        public void Recovers_floor_of_rate()
        {
            Assert.AreEqual(0, Insurance.Recovered(100, 0f));   // 무보험 → 전손
            Assert.AreEqual(20, Insurance.Recovered(100, 0.2f));
            Assert.AreEqual(2, Insurance.Recovered(5, 0.5f));   // floor(2.5)
            Assert.AreEqual(100, Insurance.Recovered(100, 1f)); // 완전반출
        }

        [Test]
        public void Clamps_and_guards()
        {
            Assert.AreEqual(0, Insurance.Recovered(0, 0.5f));
            Assert.AreEqual(0, Insurance.Recovered(-10, 0.5f));
            Assert.AreEqual(50, Insurance.Recovered(50, 5f)); // rate>1 → clamp 1
        }
    }
}
