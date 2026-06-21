namespace TDS.Core
{
    /// <summary>
    /// RunToCover 도착 판정(순수). cover point가 도달 불가 위치면 에이전트가 근처에서 비비며(grinding)
    /// 영영 못 닿는다 → 도달 반경 안이거나 일정 시간 진전이 없으면 도착으로 간주(BattleState 전이).
    /// </summary>
    public static class CoverApproach
    {
        public const float ArriveRadius = 1.2f;
        public const float StallGiveUpSeconds = 1.0f;

        public static bool ShouldEngage(float distanceToCover, float secondsSinceProgress)
        {
            return distanceToCover <= ArriveRadius || secondsSinceProgress >= StallGiveUpSeconds;
        }
    }
}
