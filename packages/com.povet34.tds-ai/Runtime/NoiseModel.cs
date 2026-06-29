using System.Collections.Generic;
using UnityEngine;

namespace TDS.Core
{
    /// <summary>소리 종류(§6.2.1 소음 테이블). 플레이어가 내는 소리만 적이 반응한다(적끼리 X — 글루에서 보장).</summary>
    public enum NoiseType { None, Gunshot, BulletImpact, Explosion, Footstep, Reload }

    /// <summary>소리 한 종류의 프로파일.</summary>
    public struct NoiseProfile
    {
        public float loudness;     // = 최소 가청 거리(m). 이 거리 안의 적이 듣는다.
        public bool revealsSource; // true = 소음 발생자(플레이어) 위치를 조사 / false = 소음이 난 위치를 조사
    }

    /// <summary>
    /// 소음 테이블(§6.2.1, 기획 2026-06-25). 수치 = 최소 가청 거리(m).
    /// - 발포음(Gunshot): 35 — 발생자=플레이어 위치를 알림(총구가 플레이어에 있음).
    /// - 피격음(BulletImpact): 9 — 박힌 위치만(실탄 근거리). 발생자 안 알림.
    /// - 폭발음(Explosion, 유탄): 90 — **발생자(플레이어=던진 사람) 위치를 알림**. 폭발은 사실상 플레이어가
    ///   자기 위치를 광역으로 광고하는 것 → 폭발음 들은 모든 적이 플레이어 위치를 앎.
    /// - 발소리/재장전: 근거리, 플레이어 위치를 알림(추후 글루 연결).
    /// 값은 게임플레이 상수 — 추후 SO로 빼도 됨.
    /// </summary>
    public static class NoiseCatalog
    {
        public static NoiseProfile Profile(NoiseType type)
        {
            switch (type)
            {
                case NoiseType.Gunshot:      return new NoiseProfile { loudness = 35f, revealsSource = true };
                case NoiseType.BulletImpact: return new NoiseProfile { loudness = 9f,  revealsSource = false };
                case NoiseType.Explosion:    return new NoiseProfile { loudness = 90f, revealsSource = true };
                case NoiseType.Footstep:     return new NoiseProfile { loudness = 8f,  revealsSource = true };
                case NoiseType.Reload:       return new NoiseProfile { loudness = 12f, revealsSource = true };
                default:                     return new NoiseProfile { loudness = 0f,  revealsSource = false };
            }
        }

        public static float Loudness(NoiseType type) => Profile(type).loudness;
    }

    /// <summary>적이 검사할 소음 1건(글루가 채워 넣음).</summary>
    public struct NoiseReading
    {
        public NoiseType type;
        public float distance;    // 적 → 소음 위치
        public float age;         // 발생 후 경과(초)
        public Vector3 noisePos;  // 소음이 난 위치
        public Vector3 sourcePos; // 발생자(플레이어) 위치
    }

    /// <summary>
    /// 소음 가청/우선순위 판정(순수, §6.2.1). 가청 = 최근(maxAge) + 거리 ≤ loudness.
    /// 여러 소음이 동시에 들리면 **가장 큰 소리(loudness 최대)가 이긴다** → 발포음(35)이 피격음(9)을 이겨,
    /// "발포음 들리는데 피격음 따라가던" 문제를 해결. 조사 위치는 revealsSource면 발생자(플레이어), 아니면 소음 위치.
    /// </summary>
    public static class NoiseModel
    {
        public static bool Heard(float distanceToNoise, float noiseRadius, float ageSeconds, float maxAgeSeconds)
        {
            if (ageSeconds < 0f || ageSeconds > maxAgeSeconds)
                return false;
            return distanceToNoise <= noiseRadius;
        }

        /// <summary>들리는 소음 중 가장 큰 소리를 골라 조사 위치/종류 반환. 아무것도 안 들리면 false.</summary>
        public static bool Resolve(IReadOnlyList<NoiseReading> readings, float maxAge, out Vector3 investigatePoint, out NoiseType type)
        {
            type = NoiseType.None;
            investigatePoint = Vector3.zero;
            float best = -1f;

            if (readings == null)
                return false;

            for (int i = 0; i < readings.Count; i++)
            {
                var r = readings[i];
                var p = NoiseCatalog.Profile(r.type);
                if (p.loudness > best && Heard(r.distance, p.loudness, r.age, maxAge))
                {
                    best = p.loudness;
                    type = r.type;
                    investigatePoint = p.revealsSource ? r.sourcePos : r.noisePos;
                }
            }
            return type != NoiseType.None;
        }
    }
}
