using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TDS.Core;

namespace TDS.Tests.PlayMode
{
    /// <summary>
    /// §12 BattleMover 글루 통합 검증: 추격 중인 근접 적이 플레이어 시야 정면을 피하는 목적지를 고른다.
    /// (직진이면 정면(노출 1)으로 향함. 시야-회피면 플랭크/뒤(낮은 노출)로 향함.)
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
            yield return null;
        }

        [UnityTest]
        public IEnumerator Chasing_melee_avoids_player_front()
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

            // 플레이어: 원점, +z를 바라봄. (직접 인스턴스화 → controlsEnabled false → 회전 override 없음 → 방향 유지)
            player = Object.Instantiate(Resources.Load<GameObject>("Player"));
            player.name = "Player";
            player.transform.position = Vector3.zero;
            player.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            yield return null;

            // 근접 적: 플레이어 정면(+z)에 배치 → 직진이면 계속 정면
            var table = Resources.Load<SpawnTable>("ST_Basic");
            enemyGo = Object.Instantiate(table.entries[0].prefab, new Vector3(0f, 0f, 5f), Quaternion.identity);
            yield return null;
            yield return null;

            var melee = enemyGo.GetComponentInChildren<Enemy_Melee>();
            Assert.IsNotNull(melee, "Enemy_Melee 없음");
            melee.stateMachine.ChangeState(melee.chaseState);

            // 목적지 갱신(0.25s throttle) + 이동 시간
            float minExposureSeen = 1f;
            for (int i = 0; i < 40; i++)
            {
                yield return null;
                if (melee == null || melee.agent == null) break;
                Vector3 dest = melee.agent.destination;
                float exp = BattleMover.FrontExposure(dest, player.transform.position, player.transform.forward, 60f);
                if (exp < minExposureSeen) minExposureSeen = exp;
            }

            Assert.Less(minExposureSeen, 0.5f,
                "추격 목적지가 플레이어 정면(높은 노출)에 머묾 — 시야-회피 플랭킹 미작동");
        }
    }
}
