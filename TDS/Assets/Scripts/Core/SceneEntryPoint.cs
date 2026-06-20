using UnityEngine;

namespace TDS.Core
{
    /// <summary>
    /// 씬 진입점(Phase 0.1, 결정 D6). 씬에 하나 두면, 그 씬으로 **단독 진입**해도
    /// <see cref="GameBootstrap.EnsureSystems"/>가 영속 Systems를 보장한다.
    /// (Boot 씬 강제 없이 맵 씬 단독 Play/테스트 가능.)
    /// </summary>
    [DisallowMultipleComponent]
    public class SceneEntryPoint : MonoBehaviour
    {
        private void Awake() => GameBootstrap.EnsureSystems();
    }
}
