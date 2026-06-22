using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TDS.Core;

namespace TDS.Tests.PlayMode
{
    /// <summary>
    /// §12 BattleMover 글루 통합 검증.
    /// - 최근 피격(그레이스) 적: 플레이어 시야 정면을 피하는 목적지를 고른다.
    /// - 평소(미피격) 적: 그냥 공격 사거리까지 근접한다(둘러싸서 때림).
    /// </summary>
    public class BattleMoveIntegrationTests
    {
        private GameObject floor, player, enemyGo;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var g in new[] { enemyGo, player, floor })
                if (g != null) Object.DestroyImmediate(g);
            foreach (var s in Object.FindObjectsByType<Unity.AI.Navigation.NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(s.gameObject);
            if (GameBootstrap.Instance != null)
                Object.DestroyImmediate(GameBootstrap.Instance.gameObject);
            GameServices.ResetForTests();
            // 피격 FX(CFXR) 정리(루트 단위, null-safe)
            var roots = new System.Collections.Generic.HashSet<GameObject>();
            foreach (var fx in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (fx != null && fx.transform.root.name.Contains("CFXR")) roots.Add(fx.transform.root.gameObject);
            foreach (var r in roots) if (r != null) Object.DestroyImmediate(r);
            yield return null;
        }

        private IEnumerator Setup(Vector3 playerForward, Vector3 enemyPos, int entryIndex = 0)
        {
            GameServices.ResetForTests();
            GameBootstrap.EnsureSystems();

            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "TestFloor";
            floor.transform.localScale = new Vector3(40f, 1f, 40f);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            var surface = floor.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
            surface.collectObjects = Unity.AI.Navigation.CollectObjects.All;
            surface.BuildNavMesh();
            yield return null;

            player = Object.Instantiate(Resources.Load<GameObject>("Player"));
            player.name = "Player";
            player.transform.position = Vector3.zero;
            player.transform.rotation = Quaternion.LookRotation(playerForward);
            yield return null;

            var table = Resources.Load<SpawnTable>("ST_Basic");
            enemyGo = Object.Instantiate(table.entries[entryIndex].prefab, enemyPos, Quaternion.identity);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Threatened_melee_avoids_player_front()
        {
            // 플레이어 +z 바라봄, 적은 정면(+z)에 배치
            yield return Setup(Vector3.forward, new Vector3(0f, 0f, 5f));

            var melee = enemyGo.GetComponentInChildren<Enemy_Melee>();
            Assert.IsNotNull(melee);

            // 최근 피격 상태로 만듦 → 회피 활성
            var dmg = enemyGo.GetComponentInChildren<IDamagable>();
            Assert.IsNotNull(dmg, "히트박스 없음");
            dmg.TakeDamage(2);

            melee.stateMachine.ChangeState(melee.chaseState);

            float minExposure = 1f;
            for (int i = 0; i < 40; i++)
            {
                yield return null;
                if (melee == null || melee.agent == null) break;
                float exp = BattleMover.FrontExposure(melee.agent.destination, player.transform.position, player.transform.forward, 60f);
                if (exp < minExposure) minExposure = exp;
            }

            Assert.Less(minExposure, 0.5f, "피격 후에도 정면(높은 노출)으로 향함 — 회피 미작동");
        }

        [UnityTest]
        public IEnumerator Calm_melee_closes_to_attack_range()
        {
            // 미피격. 적이 공격 사거리까지 근접해야 함(시야 안 거리 8 — 더 멀면 미발각으로 battle 이탈).
            yield return Setup(Vector3.forward, new Vector3(0f, 0f, 8f));

            var melee = enemyGo.GetComponentInChildren<Enemy_Melee>();
            Assert.IsNotNull(melee);
            float startDist = Vector3.Distance(melee.transform.position, player.transform.position);

            // 인지 모델(시야 기반): 적이 플레이어를 보게 하고 먼저 교전 진입 → 추격 중 perception이
            // 재교전(recovery)으로 yank하거나 시야상실로 이탈하지 않게 한다(결정적 근접).
            melee.transform.rotation = Quaternion.LookRotation(player.transform.position - melee.transform.position);
            melee.EnterBattleMode();
            melee.stateMachine.ChangeState(melee.chaseState);

            // 프레임 수가 아니라 실제 시간으로 대기 — 테스트 러너는 프레임 dt가 작아 느린 agent가
            // 프레임 기준으론 거의 못 움직인다(§ BattleMove 글루는 정상, 시간이 필요).
            float t = 0f;
            while (t < 5f)
            {
                yield return null;
                if (melee == null) break;
                t += Time.deltaTime;
            }

            float endDist = Vector3.Distance(melee.transform.position, player.transform.position);
            // §6.5: 근접은 공격 사거리까지 접근해 둘러싸 공격 → 시작보다 확연히 가까워져야 함.
            Assert.Less(endDist, startDist - 3f, $"평소 적이 근접하지 않음(거리 {startDist:0.0}→{endDist:0.0})");
        }

        [UnityTest]
        public IEnumerator Threatened_ranged_repositions()
        {
            // 원거리(Enemy_Range, ST_Basic 두 번째 엔트리)
            yield return Setup(Vector3.forward, new Vector3(0f, 0f, 8f), entryIndex: 1);

            var er = enemyGo.GetComponentInChildren<Enemy_Range>();
            Assert.IsNotNull(er, "Enemy_Range 없음");
            er.stateMachine.ChangeState(er.battleState); // 교전(정지) 상태
            yield return null;
            Vector3 startPos = er.transform.position;

            // 피격 → 재배치(이동) 트리거
            enemyGo.GetComponentInChildren<IDamagable>().TakeDamage(2);

            // 프레임 수가 아니라 실제 시간으로 — 러너 프레임 dt가 작아 느린 agent가 프레임 기준으론 거의 안 움직임.
            float maxMove = 0f, t = 0f;
            while (t < 3f)
            {
                yield return null;
                if (er == null) break;
                maxMove = Mathf.Max(maxMove, Vector3.Distance(startPos, er.transform.position));
                t += Time.deltaTime;
            }

            Assert.Greater(maxMove, 1f, "피격당한 원거리 적이 재배치하지 않음(굳어있음)");
        }
    }
}
