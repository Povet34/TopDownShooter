namespace TDS.Core
{
    public enum WaveAction
    {
        /// <summary>현재 웨이브 진행 중 — 대기.</summary>
        Wait,
        /// <summary>다음 웨이브를 스폰해야 함.</summary>
        SpawnNext,
        /// <summary>모든 웨이브 종료.</summary>
        Done
    }

    /// <summary>
    /// 웨이브 진행 순수 로직(유니티 비의존, EditMode 테스트 가능).
    /// 현재 웨이브의 생존 수/경과 시간을 받아 다음 행동(대기/다음 스폰/종료)을 결정한다.
    /// 진행 조건: 적 전멸(clear) 또는 최대 시간 초과(timeout). tension formula는 맵 수정 이후 FUTURE.
    /// </summary>
    public class WaveSequencer
    {
        public int TotalWaves { get; }

        /// <summary>현재(마지막으로 스폰된) 웨이브 인덱스. -1 = 아직 아무 웨이브도 스폰 안 함.</summary>
        public int CurrentWave { get; private set; } = -1;

        public bool Finished { get; private set; }

        public WaveSequencer(int totalWaves)
        {
            TotalWaves = totalWaves < 0 ? 0 : totalWaves;
            if (TotalWaves == 0)
                Finished = true;
        }

        /// <summary>
        /// 매 틱 호출해 다음 행동을 얻는다(순수 — 상태를 바꾸지 않음).
        /// </summary>
        /// <param name="aliveEnemies">현재 웨이브에서 살아있는 적 수.</param>
        /// <param name="timeSinceWaveStart">현재 웨이브 시작 후 경과(초).</param>
        /// <param name="maxWaveTime">이 시간 지나면 적이 남아도 진행. 0 이하 = 비활성.</param>
        public WaveAction Decide(int aliveEnemies, float timeSinceWaveStart, float maxWaveTime)
        {
            if (Finished)
                return WaveAction.Done;

            // 아직 첫 웨이브를 안 뿌렸으면 무조건 스폰
            if (CurrentWave < 0)
                return WaveAction.SpawnNext;

            bool cleared = aliveEnemies <= 0;
            bool timedOut = maxWaveTime > 0f && timeSinceWaveStart >= maxWaveTime;
            if (!cleared && !timedOut)
                return WaveAction.Wait;

            // 현재 웨이브 종료 → 다음 웨이브 or 전체 종료
            if (CurrentWave + 1 >= TotalWaves)
                return WaveAction.Done;

            return WaveAction.SpawnNext;
        }

        /// <summary>디렉터가 실제로 다음 웨이브를 스폰했을 때 호출.</summary>
        public void MarkSpawned()
        {
            if (Finished)
                return;
            CurrentWave++;
        }

        /// <summary>Decide가 Done을 돌려준 뒤 디렉터가 호출해 종료를 확정.</summary>
        public void MarkFinished() => Finished = true;
    }
}
