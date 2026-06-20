namespace TDS.Core
{
    /// <summary>입력 컨트롤 전환 서비스. <c>ControlsManager</c>가 구현·등록한다.</summary>
    public interface IControlsService
    {
        void SwitchToCharacterControls();
        void SwitchToUIControls();
        void SwitchToCarControls();
    }
}
