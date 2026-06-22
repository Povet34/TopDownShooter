using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TDS.Core;

namespace TDS.Tests.PlayMode
{
    /// <summary>
    /// 웨이브 디렉터 통합 검증: 웨이브 스폰 → 전멸 시 다음 웨이브 → 마지막 전멸 시 종료.
    /// (진행 결정 로직은 WaveSequencer EditMode 테스트가 별도로 검증.)
    /// </summary>
    public class SpawnDirectorTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var d in Object.FindObjectsByType<SpawnDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(d.gameObject);
            foreach (var sq in Object.FindObjectsByType<Squad>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(sq.gameObject);
            foreach (var e in Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(e.gameObject);
            foreach (var p in Object.FindObjectsByType<Player>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(p.gameObject);
            foreach (var b in Object.FindObjectsByType<Bullet>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(b.gameObject);
            foreach (var s in Object.FindObjectsByType<Unity.AI.Navigation.NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(s.gameObject);
            if (GameBootstrap.Instance != null)
                Object.DestroyImmediate(GameBootstrap.Instance.gameObject);
            GameServices.ResetForTests();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Director_advances_through_waves_as_enemies_die()
        {
            GameServices.ResetForTests();
            GameBootstrap.EnsureSystems();

            // 적 AI(NavMeshAgent)가 navmesh를 요구 → 테스트용 바닥+navmesh 베이크
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "TestFloor";
            floor.transform.localScale = new Vector3(40f, 1f, 40f);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            var surface = floor.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
            surface.collectObjects = Unity.AI.Navigation.CollectObjects.All;
            surface.BuildNavMesh();
            yield return null;

            // 적 AI가 GameObject.Find("Player")를 쓰므로 플레이어 먼저
            var player = Object.Instantiate(Resources.Load<GameObject>("Player"));
            player.name = "Player";
            yield return null;

            var table = Resources.Load<SpawnTable>("ST_Basic");
            Assert.IsNotNull(table, "Resources/ST_Basic 로드 실패");

            // 디렉터: 2웨이브 × 2마리. 비활성 상태로 만들고 private 필드 세팅 후 활성화(Awake 실행)
            var go = new GameObject("Director");
            go.SetActive(false);
            var dir = go.AddComponent<SpawnDirector>();
            ConfigureDirector(dir, table, waveCount: 2, perWave: 2);
            go.SetActive(true);

            // 몇 프레임 진행 → 웨이브0 스폰
            for (int i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(1, dir.CurrentWaveNumber, "첫 웨이브가 스폰되지 않음");
            Assert.AreEqual(2, dir.AliveCount, "웨이브0 생존 수가 2가 아님");

            // 웨이브0 전멸 → 웨이브1로 진행
            KillAliveEnemies();
            for (int i = 0; i < 4; i++) yield return null;
            Assert.AreEqual(2, dir.CurrentWaveNumber, "웨이브0 전멸 후 웨이브1로 진행하지 않음");
            Assert.AreEqual(2, dir.AliveCount, "웨이브1 생존 수가 2가 아님");

            // 웨이브1(마지막) 전멸 → 종료
            KillAliveEnemies();
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsTrue(dir.Finished, "마지막 웨이브 전멸 후 디렉터가 종료되지 않음");
        }

        // §6.3.2 상시 로밍: 디렉터가 maxSquads만큼 분대를 맵 가장자리에 유지(디스폰 시 리스폰).
        [UnityTest]
        public IEnumerator Roaming_director_keeps_squads_at_map_edge()
        {
            GameServices.ResetForTests();
            GameBootstrap.EnsureSystems();

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "TestFloor";
            floor.transform.localScale = new Vector3(40f, 1f, 40f);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            var surface = floor.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
            surface.collectObjects = Unity.AI.Navigation.CollectObjects.All;
            surface.BuildNavMesh();
            yield return null;

            var player = Object.Instantiate(Resources.Load<GameObject>("Player"));
            player.name = "Player";
            yield return null;

            var table = Resources.Load<SpawnTable>("ST_Basic");
            const float halfExtent = 18f, edgeInset = 6f;
            const int maxSquads = 2;

            var go = new GameObject("Director");
            go.SetActive(false);
            var dir = go.AddComponent<SpawnDirector>();
            ConfigureRoaming(dir, table, maxSquads, halfExtent, edgeInset);
            go.SetActive(true);

            // 스폰까지 진행
            for (int i = 0; i < 12; i++) yield return null;

            var squads = Object.FindObjectsByType<Squad>(FindObjectsSortMode.None);
            Assert.AreEqual(maxSquads, squads.Length, "로밍 디렉터가 maxSquads만큼 분대를 유지하지 않음");

            // 각 분대는 맵 가장자리(중심에서 edgeInset 안쪽 둘레)에서 스폰됨
            float innerEdge = halfExtent - edgeInset; // 12
            foreach (var s in squads)
            {
                Vector3 c = Centroid(s);
                float ring = Mathf.Max(Mathf.Abs(c.x), Mathf.Abs(c.z));
                Assert.Greater(ring, innerEdge - 5f, $"분대가 가장자리가 아니라 안쪽에 스폰됨(ring={ring:0.0})");
            }

            // 리스폰: 분대 전부 제거 → 다시 maxSquads로 채움
            foreach (var s in squads) Object.DestroyImmediate(s.gameObject);
            for (int i = 0; i < 12; i++) yield return null;
            Assert.AreEqual(maxSquads, Object.FindObjectsByType<Squad>(FindObjectsSortMode.None).Length,
                "분대 제거 후 디렉터가 리스폰하지 않음");
        }

        private static Vector3 Centroid(Squad s)
        {
            Vector3 c = Vector3.zero; int n = 0;
            foreach (var e in s.Members) if (e != null) { c += e.transform.position; n++; }
            return n > 0 ? c / n : s.transform.position;
        }

        private static void ConfigureRoaming(SpawnDirector dir, SpawnTable table, int maxSquads, float halfExtent, float edgeInset)
        {
            SetPrivate(dir, "mode", SpawnDirector.SpawnMode.Roaming);
            SetPrivate(dir, "roamTable", table);
            SetPrivate(dir, "maxSquads", maxSquads);
            SetPrivate(dir, "roamSpawnInterval", 0f);   // 매 틱 채움(테스트 빠르게)
            SetPrivate(dir, "edgeInset", edgeInset);
            SetPrivate(dir, "despawnMargin", 4f);
            SetPrivate(dir, "roamIdleTime", 1f);
            SetPrivate(dir, "squadSize", new Vector2Int(2, 3));
            SetPrivate(dir, "seed", 999);
            SetPrivate(dir, "requirePlayer", true);
            SetPrivate(dir, "autoStart", true);
            // MapGenerator 없이 bounds 직접 주입
            SetPrivate(dir, "bounds", new MapGenerator.MapBounds { center = Vector3.zero, halfExtent = halfExtent });
            SetPrivate(dir, "boundsKnown", true);
        }

        private static void ConfigureDirector(SpawnDirector dir, SpawnTable table, int waveCount, int perWave)
        {
            var waves = new List<SpawnDirector.WaveDef>();
            for (int i = 0; i < waveCount; i++)
                waves.Add(new SpawnDirector.WaveDef { table = table, count = perWave, maxWaveTime = 0f });

            SetPrivate(dir, "waves", waves);
            SetPrivate(dir, "minRadius", 2f);   // 테스트 바닥(40x40) 안쪽에 스폰
            SetPrivate(dir, "maxRadius", 4f);
            SetPrivate(dir, "seed", 12345);
            SetPrivate(dir, "requirePlayer", true);
            SetPrivate(dir, "autoStart", true);
        }

        private static void SetPrivate(object obj, string field, object value)
        {
            var f = obj.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, $"SpawnDirector private 필드 '{field}' 없음");
            f.SetValue(obj, value);
        }

        private static void KillAliveEnemies()
        {
            foreach (var e in Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (e.health == null || e.health.currentHealth <= 0)
                    continue; // 이미 죽은 적(랙돌)은 건너뜀
                var dmg = e.GetComponentInChildren<IDamagable>();
                if (dmg != null)
                    dmg.TakeDamage(99999);
            }
        }
    }
}
