using UnityEngine;
using UnityEngine.AI;

namespace TDS.Core
{
    /// <summary>
    /// 맵에 배치하는 몬스터 스포너(Phase C). 플레이어가 생긴 뒤, 스폰 테이블에서 가중 선택해
    /// 자기 주변 링(navmesh 위)에 N마리 스폰한다. 시드로 결정적.
    /// 기존 적 AI(Enemy)를 그대로 재사용 — prefab만 인스턴스화.
    /// </summary>
    [DisallowMultipleComponent]
    public class MonsterSpawner : MonoBehaviour
    {
        [SerializeField] private SpawnTable table;
        [SerializeField] private int count = 5;
        [SerializeField] private float minRadius = 8f;
        [SerializeField] private float maxRadius = 20f;
        [SerializeField] private int seed = 1;
        [SerializeField] private bool requirePlayer = true;
        [SerializeField] private bool spawnOnStart = true;

        private bool done;

        public int SpawnedCount { get; private set; }

        private void Update()
        {
            if (done || !spawnOnStart)
                return;

            // 적 AI가 플레이어를 참조하므로 플레이어 존재 후 스폰
            if (requirePlayer && GameObject.FindWithTag("Player") == null)
                return;

            done = true;
            SpawnWave();
        }

        public void SpawnWave()
        {
            if (table == null)
            {
                Debug.LogWarning("[MonsterSpawner] SpawnTable 미할당.");
                return;
            }

            var rng = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                var def = table.Pick((float)rng.NextDouble());
                if (def == null || def.prefab == null)
                    continue;

                double ang = rng.NextDouble() * System.Math.PI * 2.0;
                float dist = Mathf.Lerp(minRadius, maxRadius, (float)rng.NextDouble());
                Vector3 pos = transform.position + new Vector3(Mathf.Cos((float)ang) * dist, 0f, Mathf.Sin((float)ang) * dist);

                if (NavMesh.SamplePosition(pos, out var hit, 8f, NavMesh.AllAreas))
                    pos = hit.position;

                Instantiate(def.prefab, pos, Quaternion.identity);
                SpawnedCount++;
            }
        }
    }
}
