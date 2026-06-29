using System.Collections.Generic;
using UnityEngine;

namespace TDS.Core
{
    /// <summary>
    /// 분대 대형 수학(순수, §6.2 그룹 인지). 황금각 나선으로 멤버를 중심 주변에 균등 분산하고,
    /// 함께 순찰할 때 "모두 모였는지(앵커 전진 게이트)"를 판정한다.
    /// 군집 스폰(SpawnDirector)과 순찰 대형(Squad)이 같은 분산 공식을 쓰므로 한 곳으로 모음(중복 제거).
    /// </summary>
    public static class SquadFormation
    {
        public const float GoldenAngle = 2.39996323f; // 황금각(rad)

        /// <summary>
        /// 중심 기준 index번째 멤버의 대형 오프셋(황금각 나선 — 겹쳐 쌓이지 않게 균등 분산). y=0.
        /// 반경은 radius*sqrt((index+0.5)/count)라 index가 커질수록 단조 증가하고 항상 radius 미만.
        /// </summary>
        public static Vector3 SpiralOffset(int index, int count, float radius)
        {
            int n = Mathf.Max(1, count);
            if (index < 0) index = 0;
            float ga = index * GoldenAngle;
            float r = radius * Mathf.Sqrt((index + 0.5f) / n);
            return new Vector3(Mathf.Cos(ga) * r, 0f, Mathf.Sin(ga) * r);
        }

        /// <summary>중심 + 나선 오프셋(편의).</summary>
        public static Vector3 SpiralPoint(Vector3 center, int index, int count, float radius)
            => center + SpiralOffset(index, count, radius);

        /// <summary>
        /// 가장 뒤처진 멤버까지 앵커 주변(formationRadius+slack 평면거리)에 모였는가 → 앵커 전진 허용.
        /// 비어 있으면 true(전진 막을 이유 없음). 한 명이라도 멀면 false(낙오 대기 → 뭉침 유지).
        /// </summary>
        public static bool AllGathered(IReadOnlyList<Vector3> positions, Vector3 anchor, float formationRadius, float slack = 3f)
        {
            if (positions == null || positions.Count == 0)
                return true;

            float threshold = formationRadius + slack;
            for (int i = 0; i < positions.Count; i++)
            {
                Vector3 d = positions[i] - anchor; d.y = 0f;
                if (d.magnitude > threshold)
                    return false;
            }
            return true;
        }
    }
}
