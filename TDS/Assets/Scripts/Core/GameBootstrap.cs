using UnityEngine;

namespace TDS.Core
{
    /// <summary>
    /// 영속 시스템 부트스트랩(Phase 0.1, 결정 D6). 어떤 씬으로 진입하든 `EnsureSystems()`가
    /// `Resources/Systems` 프리팹을 **정확히 한 번** 띄운다(멱등 + DontDestroyOnLoad).
    /// 프리팹에 담긴 매니저들은 Awake에서 <see cref="GameServices"/>에 자기 등록한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameBootstrap : MonoBehaviour
    {
        public const string SystemsResourcePath = "Systems";

        public static GameBootstrap Instance { get; private set; }

        /// <summary>Systems가 없으면 생성, 있으면 재사용(멱등). 생성/기존 인스턴스를 반환.</summary>
        public static GameBootstrap EnsureSystems()
        {
            SystemsEnsurer.Ensure(() => Instance != null, Spawn);
            return Instance;
        }

        private static void Spawn()
        {
            var prefab = Resources.Load<GameObject>(SystemsResourcePath);
            if (prefab == null)
            {
                Debug.LogError($"[GameBootstrap] Resources/{SystemsResourcePath}.prefab 을 찾지 못했습니다.");
                return;
            }
            var go = Instantiate(prefab);
            go.name = "Systems";
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
