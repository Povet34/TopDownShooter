namespace TDS.Core
{
    /// <summary>미션 진행 서비스. <c>MissionManager</c>가 구현·등록한다.</summary>
    public interface IMissionService
    {
        void StartMission();
        bool MissionCompleted();
    }
}
