using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    // §6.2.1 소음 테이블: 수치=가청 거리, 가장 큰 소리 우선, 종류별 조사 위치(발생자/소음 위치).
    public class NoiseModelTests
    {
        // --- Heard 경계 ---

        [Test]
        public void Recent_and_within_radius_is_heard()
            => Assert.IsTrue(NoiseModel.Heard(5f, 10f, 0.1f, 0.25f));

        [Test]
        public void Beyond_radius_is_not_heard()
            => Assert.IsFalse(NoiseModel.Heard(12f, 10f, 0.1f, 0.25f));

        [Test]
        public void Stale_noise_is_not_heard()
            => Assert.IsFalse(NoiseModel.Heard(5f, 10f, 1f, 0.25f));

        [Test]
        public void Exactly_at_radius_is_heard()
            => Assert.IsTrue(NoiseModel.Heard(10f, 10f, 0f, 0.25f));

        [Test]
        public void Negative_age_is_not_heard()
            => Assert.IsFalse(NoiseModel.Heard(1f, 10f, -1f, 0.25f));

        // --- 소음 테이블 ---

        [Test]
        public void Catalog_loudness_orders_gunshot_below_explosion_above_impact()
        {
            Assert.AreEqual(35f, NoiseCatalog.Loudness(NoiseType.Gunshot), 1e-4f);
            Assert.AreEqual(9f, NoiseCatalog.Loudness(NoiseType.BulletImpact), 1e-4f);
            Assert.AreEqual(90f, NoiseCatalog.Loudness(NoiseType.Explosion), 1e-4f);
            Assert.Greater(NoiseCatalog.Loudness(NoiseType.Gunshot), NoiseCatalog.Loudness(NoiseType.BulletImpact));
            Assert.Greater(NoiseCatalog.Loudness(NoiseType.Explosion), NoiseCatalog.Loudness(NoiseType.Gunshot));
        }

        [Test]
        public void Catalog_reveal_source_flags()
        {
            Assert.IsTrue(NoiseCatalog.Profile(NoiseType.Gunshot).revealsSource);   // 총구=플레이어
            Assert.IsTrue(NoiseCatalog.Profile(NoiseType.Explosion).revealsSource); // 폭발=던진 플레이어
            Assert.IsFalse(NoiseCatalog.Profile(NoiseType.BulletImpact).revealsSource); // 박힌 위치만
        }

        // --- Resolve: 가장 큰 소리 우선 + 종류별 조사 위치 ---

        private static NoiseReading R(NoiseType t, float dist, Vector3 noisePos, Vector3 sourcePos, float age = 0.1f)
            => new NoiseReading { type = t, distance = dist, age = age, noisePos = noisePos, sourcePos = sourcePos };

        [Test]
        public void Gunshot_beats_impact_when_both_heard()
        {
            // 발포음(35)·피격음(9) 둘 다 가청 → 발포음 우선, 조사 위치=발생자(플레이어)
            var player = new Vector3(20f, 0f, 0f);
            var impactPos = new Vector3(0f, 0f, 5f);
            var readings = new List<NoiseReading>
            {
                R(NoiseType.BulletImpact, 5f, impactPos, impactPos),
                R(NoiseType.Gunshot, 20f, player, player),
            };
            Assert.IsTrue(NoiseModel.Resolve(readings, 0.3f, out var target, out var type));
            Assert.AreEqual(NoiseType.Gunshot, type);
            Assert.AreEqual(player, target);
        }

        [Test]
        public void Impact_only_investigates_impact_position()
        {
            var impactPos = new Vector3(-2f, 0f, 4f);
            var readings = new List<NoiseReading> { R(NoiseType.BulletImpact, 5f, impactPos, Vector3.zero) };
            Assert.IsTrue(NoiseModel.Resolve(readings, 0.3f, out var target, out var type));
            Assert.AreEqual(NoiseType.BulletImpact, type);
            Assert.AreEqual(impactPos, target);
        }

        [Test]
        public void Explosion_reveals_player_not_blast_point()
        {
            // 폭발음은 던진 플레이어 위치를 알린다(폭발 지점이 아니라).
            var blast = new Vector3(50f, 0f, 0f);
            var player = new Vector3(-30f, 0f, 10f);
            var readings = new List<NoiseReading> { R(NoiseType.Explosion, 50f, blast, player) };
            Assert.IsTrue(NoiseModel.Resolve(readings, 0.3f, out var target, out var type));
            Assert.AreEqual(NoiseType.Explosion, type);
            Assert.AreEqual(player, target);
        }

        [Test]
        public void Out_of_range_noise_is_not_resolved()
        {
            // 발포음 35m인데 40m면 안 들림
            var readings = new List<NoiseReading> { R(NoiseType.Gunshot, 40f, Vector3.zero, Vector3.zero) };
            Assert.IsFalse(NoiseModel.Resolve(readings, 0.3f, out _, out var type));
            Assert.AreEqual(NoiseType.None, type);
        }

        [Test]
        public void Nothing_heard_is_none()
        {
            Assert.IsFalse(NoiseModel.Resolve(new List<NoiseReading>(), 0.3f, out _, out var type));
            Assert.AreEqual(NoiseType.None, type);
            Assert.IsFalse(NoiseModel.Resolve(null, 0.3f, out _, out _));
        }
    }
}
