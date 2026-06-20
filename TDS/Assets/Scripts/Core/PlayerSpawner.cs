using UnityEngine;

namespace TDS.Core
{
    /// <summary>
    /// 맵 씬에서 플레이어를 절차 맵 중앙(스폰존)에 스폰(Phase 0.2). 먼저 <see cref="GameBootstrap.EnsureSystems"/>로
    /// 시스템을 보장한 뒤 스폰해 순서 의존(ControlsManager 등)을 만족시킨다.
    /// playerPrefab 미할당 시 Resources/Player로 폴백.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private Vector3 mapCenter = Vector3.zero;
        [SerializeField] private float groundY = 0f;
        [SerializeField] private bool spawnOnStart = true;

        public GameObject Spawned { get; private set; }

        private void Start()
        {
            if (spawnOnStart)
                Spawn();
        }

        public GameObject Spawn()
        {
            if (Spawned != null)
                return Spawned;

            GameBootstrap.EnsureSystems(); // 플레이어보다 시스템을 먼저 보장

            var prefab = playerPrefab != null ? playerPrefab : Resources.Load<GameObject>("Player");
            if (prefab == null)
            {
                Debug.LogError("[PlayerSpawner] playerPrefab 미할당 + Resources/Player 없음.");
                return null;
            }

            var pos = PlayerSpawnPoint.Resolve(mapCenter, groundY);
            Spawned = Instantiate(prefab, pos, Quaternion.identity);
            Spawned.name = "Player"; // 적 AI가 GameObject.Find("Player")로 찾으므로 "(Clone)" 제거
            return Spawned;
        }
    }
}
