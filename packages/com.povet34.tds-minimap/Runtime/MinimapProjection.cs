using UnityEngine;

namespace TDS.Core
{
    /// <summary>
    /// 미니맵/레이더 좌표 변환(순수, 테스트 가능). 플레이어 중심·북-업 고정(맵 회전 X — 탑다운 카메라가 고정 방위라 일관).
    /// 월드 (x,z)를 미니맵 로컬 픽셀 (x,y)로 — z(앞)가 미니맵 위(y+)로 간다.
    /// </summary>
    public static class MinimapProjection
    {
        /// <param name="entityXZ">대상 월드 좌표의 (x, z)</param>
        /// <param name="playerXZ">플레이어 월드 좌표의 (x, z)</param>
        /// <param name="worldRange">레이더 반경에 해당하는 월드 거리(이 거리 = 가장자리)</param>
        /// <param name="radiusPixels">레이더 반경(픽셀)</param>
        /// <param name="clampedOutside">대상이 범위 밖이라 가장자리에 붙었으면 true(방향만 표시)</param>
        /// <returns>레이더 중심 기준 로컬 픽셀 오프셋. 중심=플레이어.</returns>
        public static Vector2 ToMinimap(Vector2 entityXZ, Vector2 playerXZ, float worldRange, float radiusPixels, out bool clampedOutside)
        {
            clampedOutside = false;
            if (worldRange <= 0f || radiusPixels <= 0f)
                return Vector2.zero;

            Vector2 d = (entityXZ - playerXZ) / worldRange * radiusPixels;
            float mag = d.magnitude;
            if (mag > radiusPixels)
            {
                d = mag > 0f ? d / mag * radiusPixels : Vector2.zero;
                clampedOutside = true;
            }
            return d;
        }

        /// <summary>대상이 레이더 표시 범위(worldRange) 안에 있는가.</summary>
        public static bool IsInRange(Vector2 entityXZ, Vector2 playerXZ, float worldRange)
            => (entityXZ - playerXZ).sqrMagnitude <= worldRange * worldRange;
    }
}
