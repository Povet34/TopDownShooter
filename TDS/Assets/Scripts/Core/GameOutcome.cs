namespace TDS.Core
{
    public enum MatchState
    {
        Playing,
        Victory,
        Defeat
    }

    /// <summary>
    /// 매치 결과 판정 순수 로직(유니티 비의존, EditMode 테스트 가능).
    /// HUD의 승/패/진행 판정을 여기로 뽑아 테스트 가능하게 한다.
    /// </summary>
    public static class GameOutcome
    {
        /// <summary>
        /// 체력·웨이브 완료·생존 적 수로 매치 상태를 판정한다.
        /// 패배(체력 0 이하)가 승리보다 우선.
        /// </summary>
        public static MatchState Evaluate(int playerHealth, bool allWavesFinished, int aliveEnemies)
        {
            if (playerHealth <= 0)
                return MatchState.Defeat;

            if (allWavesFinished && aliveEnemies <= 0)
                return MatchState.Victory;

            return MatchState.Playing;
        }
    }
}
