using NUnit.Framework;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    // 폭발 피해 falloff: 중심 최대 → 반경에서 0, 선형.
    public class ExplosionModelTests
    {
        [Test]
        public void Center_takes_full_damage()
            => Assert.AreEqual(100f, ExplosionModel.DamageAt(0f, 10f, 100f), 1e-4f);

        [Test]
        public void Half_radius_takes_half_damage()
            => Assert.AreEqual(50f, ExplosionModel.DamageAt(5f, 10f, 100f), 1e-4f);

        [Test]
        public void At_radius_takes_no_damage()
            => Assert.AreEqual(0f, ExplosionModel.DamageAt(10f, 10f, 100f), 1e-4f);

        [Test]
        public void Beyond_radius_takes_no_damage()
            => Assert.AreEqual(0f, ExplosionModel.DamageAt(15f, 10f, 100f), 1e-4f);

        [Test]
        public void Zero_radius_is_safe()
            => Assert.AreEqual(0f, ExplosionModel.DamageAt(1f, 0f, 100f), 1e-4f);

        [Test]
        public void Negative_distance_clamps_to_max()
            => Assert.AreEqual(100f, ExplosionModel.DamageAt(-3f, 10f, 100f), 1e-4f);
    }
}
