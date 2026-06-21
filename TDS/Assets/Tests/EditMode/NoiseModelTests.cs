using NUnit.Framework;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    // §6.2 소음: 최근(시간 내) + 반경 안이면 들린다 → 경계 트리거.
    public class NoiseModelTests
    {
        [Test]
        public void Recent_and_within_radius_is_heard()
        {
            Assert.IsTrue(NoiseModel.Heard(distanceToNoise: 5f, noiseRadius: 10f, ageSeconds: 0.1f, maxAgeSeconds: 0.25f));
        }

        [Test]
        public void Beyond_radius_is_not_heard()
        {
            Assert.IsFalse(NoiseModel.Heard(12f, 10f, 0.1f, 0.25f));
        }

        [Test]
        public void Stale_noise_is_not_heard()
        {
            Assert.IsFalse(NoiseModel.Heard(5f, 10f, ageSeconds: 1f, maxAgeSeconds: 0.25f));
        }

        [Test]
        public void Exactly_at_radius_is_heard()
        {
            Assert.IsTrue(NoiseModel.Heard(10f, 10f, 0f, 0.25f));
        }

        [Test]
        public void Negative_age_is_not_heard()
        {
            // 아직 발생 안 한(미래) 소음은 무시
            Assert.IsFalse(NoiseModel.Heard(1f, 10f, ageSeconds: -1f, maxAgeSeconds: 0.25f));
        }
    }
}
