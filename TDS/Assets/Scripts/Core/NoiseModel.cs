using UnityEngine;

namespace TDS.Core
{
    /// <summary>들린 소음의 종류 — 조사 위치를 무엇이 만들었는지.</summary>
    public enum NoiseKind { None, Muzzle, Impact }

    /// <summary>
    /// 소음 가청 판정(순수, §6.2). 소음은 "발각"이 아니라 그쪽으로 고개를 돌리게 하는 트리거 —
    /// 최근(maxAge 이내)에 난 소음이 가청 반경 안이면 들린다.
    /// 소음원은 두 가지: 총구음(발사 위치, 큼) / 피격음(총알이 땅·벽에 박힌 위치, 작음).
    /// </summary>
    public static class NoiseModel
    {
        public static bool Heard(float distanceToNoise, float noiseRadius, float ageSeconds, float maxAgeSeconds)
        {
            if (ageSeconds < 0f || ageSeconds > maxAgeSeconds)
                return false;
            return distanceToNoise <= noiseRadius;
        }

        /// <summary>
        /// 들린 두 소음원 중 조사할 곳을 고른다. 총구음이 들리면 그쪽 우선(플레이어에 더 가까운 단서) →
        /// 총구음이 안 들렸어도 피격음(땅에 박히는 소리)이 들렸으면 그 위치로 가 플레이어를 수색.
        /// 둘 다 안 들리면 None.
        /// </summary>
        public static NoiseKind Investigate(
            bool muzzleHeard, Vector3 muzzlePos,
            bool impactHeard, Vector3 impactPos,
            out Vector3 target)
        {
            if (muzzleHeard) { target = muzzlePos; return NoiseKind.Muzzle; }
            if (impactHeard) { target = impactPos; return NoiseKind.Impact; }
            target = Vector3.zero;
            return NoiseKind.None;
        }
    }
}
