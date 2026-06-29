namespace TDS.Core
{
    /// <summary>
    /// 히트스톱(순간 정지) 순수 모델. 잔여 시간을 unscaled 시간으로 추적한다(정지 중엔 scaled 시간이 0이므로).
    /// 글루(CombatFeedback)가 매 프레임 Tick 결과를 Time.timeScale에 적용한다.
    /// </summary>
    public class HitStop
    {
        private float remaining;

        public bool IsActive => remaining > 0f;

        /// <summary>지정 시간(초, unscaled) 동안 정지 시작. 더 긴 요청만 반영(겹쳐도 짧아지지 않음).</summary>
        public void Trigger(float duration)
        {
            if (duration > remaining)
                remaining = duration;
        }

        /// <summary>
        /// unscaled delta로 갱신하고 적용할 timeScale을 반환한다.
        /// 정지 중이면 0, 끝났거나 비활성이면 normalScale.
        /// </summary>
        public float Tick(float unscaledDeltaTime, float normalScale = 1f)
        {
            if (remaining <= 0f)
                return normalScale;

            remaining -= unscaledDeltaTime;
            if (remaining <= 0f)
            {
                remaining = 0f;
                return normalScale; // 이 프레임에 해제
            }
            return 0f;
        }
    }
}
