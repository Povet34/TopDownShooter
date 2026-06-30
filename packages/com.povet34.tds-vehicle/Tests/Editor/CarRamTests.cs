using NUnit.Framework;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class CarRamTests
    {
        [Test]
        public void CanDamage_requires_min_speed()
        {
            Assert.IsFalse(CarRam.CanDamage(1f, 3f));
            Assert.IsTrue(CarRam.CanDamage(3f, 3f));
            Assert.IsTrue(CarRam.CanDamage(10f, 3f));
            Assert.IsFalse(CarRam.CanDamage(0f, 0f)); // 정지 = 데미지 없음
        }

        [Test]
        public void DamageAt_zero_below_min_speed()
        {
            Assert.AreEqual(0, CarRam.DamageAt(2.9f, 3f, 50, 200));
        }

        [Test]
        public void DamageAt_base_at_min_speed()
        {
            Assert.AreEqual(50, CarRam.DamageAt(3f, 3f, 50, 200));
        }

        [Test]
        public void DamageAt_scales_with_speed()
        {
            Assert.AreEqual(100, CarRam.DamageAt(6f, 3f, 50, 200)); // 2x 속도 → 2x base
        }

        [Test]
        public void DamageAt_caps_at_max()
        {
            Assert.AreEqual(200, CarRam.DamageAt(20f, 3f, 50, 200)); // 20/3*50 ≈ 333 → 200 상한
        }
    }
}
