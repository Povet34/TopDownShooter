namespace TDS.Core
{
    /// <summary>range 적 관점에서 cover의 적합도.</summary>
    public enum CoverSuitability
    {
        /// <summary>낮은 단상(≤0.8) — 뒤에 서서 그 위로 사격 가능. 교전 시 선호.</summary>
        ShootFrom,
        /// <summary>높은 cover — 못 쏘지만 완전 은폐. "공격도 안 받고 싶을 때"만.</summary>
        HideOnly,
        /// <summary>도달 가능한 지점이 없음 — 쓸 수 없는 cover.</summary>
        Unusable
    }

    /// <summary>
    /// cover의 높이와 도달성으로 range 적합도를 판정(순수). 땅에 붙은 단상 기준 0.8을 넘으면
    /// 총구가 cover에 박혀 그 위로 못 쏘므로 은폐 전용. 낮을수록 교전 선호.
    /// </summary>
    public static class CoverEvaluation
    {
        public const float MaxShootableHeight = 0.8f;

        public static bool IsShootable(float coverHeight) => coverHeight <= MaxShootableHeight;

        public static CoverSuitability Evaluate(float coverHeight, bool hasReachablePoint)
        {
            if (!hasReachablePoint)
                return CoverSuitability.Unusable;

            return IsShootable(coverHeight) ? CoverSuitability.ShootFrom : CoverSuitability.HideOnly;
        }

        /// <summary>교전(사격) 선호 점수: 낮을수록 1에 가깝고, 사격 불가 높이는 0.</summary>
        public static float EngageScore(float coverHeight)
        {
            if (coverHeight > MaxShootableHeight)
                return 0f;
            if (coverHeight < 0f)
                coverHeight = 0f;

            return 1f - (coverHeight / MaxShootableHeight);
        }
    }
}
