using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TDS.Core;

namespace TDS.Tests.PlayMode
{
    /// <summary>
    /// 전투 루프 검증: 총알이 호출하는 피해 경로(적 히트박스 IDamagable.TakeDamage → Enemy.GetHit)로
    /// 적이 피해를 입고 사망하는지. (총알 물리 충돌은 표준 Unity 물리 — 여기선 피해 적용을 검증.)
    /// </summary>
    public class EnemyCombatTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var e in Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(e.gameObject);
            foreach (var p in Object.FindObjectsByType<Player>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(p.gameObject);
            foreach (var b in Object.FindObjectsByType<Bullet>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(b.gameObject);
            foreach (var s in Object.FindObjectsByType<Unity.AI.Navigation.NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(s.gameObject); // 테스트 바닥 정리
            if (GameBootstrap.Instance != null)
                Object.DestroyImmediate(GameBootstrap.Instance.gameObject);
            GameServices.ResetForTests();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Enemy_takes_damage_and_dies_from_hits()
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
            var enemyGo = Object.Instantiate(table.entries[0].prefab, new Vector3(3f, 0f, 0f), Quaternion.identity);
            yield return null;
            yield return null;

            var enemy = enemyGo.GetComponentInChildren<Enemy>();
            Assert.IsNotNull(enemy, "Enemy 컴포넌트 없음");
            var hp = enemy.health;
            int before = hp.currentHealth;
            Assert.Greater(before, 0, "초기 체력이 0");

            var damagable = enemyGo.GetComponentInChildren<IDamagable>();
            Assert.IsNotNull(damagable, "적 IDamagable(히트박스) 없음");

            // 피해 → 체력 감소
            damagable.TakeDamage(2);
            Assert.Less(hp.currentHealth, before, "피해를 입어도 체력이 줄지 않음");

            // 치명타 → 사망
            damagable.TakeDamage(before + 1000);
            yield return null;
            Assert.Less(hp.currentHealth, 0, "치명타 후에도 죽지 않음(체력 0 이상)");
        }

        [UnityTest]
        public IEnumerator Dead_enemy_ragdoll_freezes_after_delay()
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
            var enemyGo = Object.Instantiate(table.entries[0].prefab, new Vector3(3f, 0f, 0f), Quaternion.identity);
            yield return null;
            yield return null;

            var enemy = enemyGo.GetComponentInChildren<Enemy>();
            var ragdoll = enemyGo.GetComponentInChildren<Ragdoll>();
            Assert.IsNotNull(ragdoll, "Ragdoll 없음");

            // 테스트를 빠르게: 고정 지연을 짧게(0.3s)
            typeof(Enemy).GetField("deadFreezeDelay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(enemy, 0.3f);

            // 치명타 → 사망 → 래그돌 물리 활성(미고정)
            enemyGo.GetComponentInChildren<IDamagable>().TakeDamage(99999);
            yield return null;
            Assert.IsFalse(ragdoll.IsFrozen, "사망 직후엔 래그돌이 물리 활성(미고정)이어야 함");

            // 지연 경과 → 고정(정지). 히트스톱(timeScale 0) 고려해 프레임으로 대기.
            for (int i = 0; i < 150 && !ragdoll.IsFrozen; i++)
                yield return null;

            Assert.IsTrue(ragdoll.IsFrozen, "지연 후 래그돌이 고정(정지)되지 않음");
        }
    }
}
