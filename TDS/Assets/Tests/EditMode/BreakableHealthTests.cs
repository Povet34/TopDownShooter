using NUnit.Framework;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class BreakableHealthTests
    {
        [Test]
        public void Partial_damage_does_not_break()
        {
            int hp = BreakableHealth.Apply(30, 10, out bool broke);
            Assert.AreEqual(20, hp);
            Assert.IsFalse(broke);
        }

        [Test]
        public void Lethal_damage_breaks()
        {
            int hp = BreakableHealth.Apply(10, 15, out bool broke);
            Assert.LessOrEqual(hp, 0);
            Assert.IsTrue(broke);
        }

        [Test]
        public void Exact_zero_breaks()
        {
            BreakableHealth.Apply(10, 10, out bool broke);
            Assert.IsTrue(broke);
        }

        [Test]
        public void Already_broken_does_not_rebreak()
        {
            // 이미 0 이하 → 추가 피해는 broke=false(중복 파괴 방지)
            BreakableHealth.Apply(-5, 10, out bool broke);
            Assert.IsFalse(broke);
        }
    }
}
