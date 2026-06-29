using UnityEngine;

namespace TDS.Core
{
    /// <summary>전리품 드랍 판정(순수, 테스트 가능). 확률·수량 결정을 글루에서 분리.</summary>
    public static class LootDrop
    {
        /// <param name="rngValue">[0,1) 난수</param>
        /// <param name="chance">드랍 확률(0~1)</param>
        public static bool ShouldDrop(double rngValue, float chance)
            => rngValue < Mathf.Clamp01(chance);

        /// <summary>드랍 수량(min~max 균등). rngValue [0,1).</summary>
        public static int Amount(double rngValue, int min, int max)
        {
            if (max < min) max = min;
            int span = max - min + 1;
            int v = min + (int)(Mathf.Clamp01((float)rngValue) * span);
            return Mathf.Clamp(v, min, max);
        }
    }
}
