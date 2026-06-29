namespace TDS.Core
{
    /// <summary>
    /// 전역 서비스 접근점(Phase 0.3). 매니저는 Awake에서 자신을 인터페이스로 <see cref="ServiceRegistry.Register"/>,
    /// 소비자는 <see cref="ServiceRegistry.Resolve"/>. 흩어진 `Singleton.instance` 직접 결합을 점진 대체한다.
    /// </summary>
    public static class GameServices
    {
        private static ServiceRegistry _registry = new ServiceRegistry();

        public static ServiceRegistry Registry => _registry;

        /// <summary>테스트 격리용 — 레지스트리를 비운다.</summary>
        public static void ResetForTests() => _registry = new ServiceRegistry();
    }
}
