namespace TDS.Core
{
    /// <summary>게임 흐름(시작/재시작/종료) 서비스. <c>GameManager</c>가 구현·등록한다.</summary>
    public interface IGameStateService
    {
        void GameStart();
        void RestartScene();
        void GameCompleted();
        void GameOver();
    }
}
