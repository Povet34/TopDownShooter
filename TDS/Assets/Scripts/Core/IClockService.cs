namespace TDS.Core
{
    /// <summary>시간 제어 서비스(슬로모/일시정지). <c>TimeManager</c>가 구현·등록한다.</summary>
    public interface IClockService
    {
        void PauseTime();
        void ResumeTime();
        void SlowMotionFor(float seconds);
    }
}
