using NUnit.Framework;
using UnityEngine;
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

        // --- 두 소음원 조사 우선순위(총구음 > 피격음) ---

        [Test]
        public void Muzzle_heard_investigates_muzzle()
        {
            var muzzle = new Vector3(3f, 0f, 7f);
            var kind = NoiseModel.Investigate(true, muzzle, false, Vector3.zero, out var target);
            Assert.AreEqual(NoiseKind.Muzzle, kind);
            Assert.AreEqual(muzzle, target);
        }

        [Test]
        public void Impact_only_investigates_impact_position()
        {
            // 발사음은 못 들었지만 총알이 근처에 박힘 → 그 위치로 가 수색
            var impact = new Vector3(-2f, 0f, 4f);
            var kind = NoiseModel.Investigate(false, Vector3.zero, true, impact, out var target);
            Assert.AreEqual(NoiseKind.Impact, kind);
            Assert.AreEqual(impact, target);
        }

        [Test]
        public void Muzzle_takes_priority_over_impact()
        {
            // 둘 다 들리면 총구음(플레이어에 더 가까운 단서) 우선
            var muzzle = new Vector3(10f, 0f, 0f);
            var impact = new Vector3(0f, 0f, 10f);
            var kind = NoiseModel.Investigate(true, muzzle, true, impact, out var target);
            Assert.AreEqual(NoiseKind.Muzzle, kind);
            Assert.AreEqual(muzzle, target);
        }

        [Test]
        public void Nothing_heard_is_none()
        {
            var kind = NoiseModel.Investigate(false, Vector3.one, false, Vector3.one, out var target);
            Assert.AreEqual(NoiseKind.None, kind);
            Assert.AreEqual(Vector3.zero, target);
        }
    }
}
