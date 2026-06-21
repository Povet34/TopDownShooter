namespace TDS.Core
{
    /// <summary>입력 컨트롤 전환 서비스. <c>ControlsManager</c>가 구현·등록한다.</summary>
    public interface IControlsService
    {
        void SwitchToCharacterControls();
        void SwitchToUIControls();
        void SwitchToCarControls();

        /// <summary>
        /// 입력 컨트롤을 새 인스턴스로 교체(옛 구독 폐기). 씬 리로드/플레이어 재스폰 시 호출해
        /// 파괴된 플레이어의 입력 콜백이 영속 컨트롤에 누적되는 누수를 막는다.
        /// </summary>
        void RecreateControls();
    }
}
