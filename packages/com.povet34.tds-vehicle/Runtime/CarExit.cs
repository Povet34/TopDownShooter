using System;
using UnityEngine;

namespace TDS.Core
{
    /// <summary>차량 하차 지점 선택(순수, 테스트 가능). 후보 중 '비어있는' 첫 지점을 고른다.</summary>
    public static class CarExit
    {
        /// <param name="points">후보 하차 지점(월드 좌표)</param>
        /// <param name="isClear">해당 지점이 비어있는지(장애물 없음) 판정(물리 오버랩 등 글루가 주입)</param>
        /// <param name="result">선택된 지점(없으면 첫 후보로 폴백)</param>
        /// <returns>후보가 하나라도 있으면 true</returns>
        public static bool TryPickClearPoint(Vector3[] points, Func<Vector3, bool> isClear, out Vector3 result)
        {
            result = default;
            if (points == null || points.Length == 0)
                return false;

            for (int i = 0; i < points.Length; i++)
            {
                if (isClear != null && isClear(points[i]))
                {
                    result = points[i];
                    return true;
                }
            }

            result = points[0]; // 전부 막혔으면 첫 후보(기존 동작 유지)
            return true;
        }
    }
}
