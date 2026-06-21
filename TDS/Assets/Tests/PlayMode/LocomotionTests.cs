using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TDS.Core;

namespace TDS.Tests.PlayMode
{
    /// <summary>
    /// 이동 애니 폴리시 통합 검증: 적이 이동(locomotion) 상태에서 실제 속도에 맞춰 anim.speed를 조절한다.
    /// (미구동이면 anim.speed가 항상 1 → 가속/감속 중 제자리걸음. 구동되면 속도비율을 따라감.)
    /// </summary>
    public class LocomotionTests
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
        public IEnumerator Locomotion_anim_speed_tracks_velocity()
        {
            GameServices.ResetForTests();
            GameBootstrap.EnsureSystems();

            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "TestFloor";
            floor.transform.localScale = new Vector3(60f, 1f, 60f);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            var surface = floor.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
            surface.collectObjects = Unity.AI.Navigation.CollectObjects.All;
            surface.BuildNavMesh();
            yield return null;

            // 멀리 둔 플레이어(추격 경로 확보)
            player = Object.Instantiate(Resources.Load<GameObject>("Player"));
            player.name = "Player";
            player.transform.position = new Vector3(20f, 0f, 0f);
            yield return null;

            var table = Resources.Load<SpawnTable>("ST_Basic");
            enemyGo = Object.Instantiate(table.entries[0].prefab, Vector3.zero, Quaternion.identity);
            yield return null;
            yield return null;

            var melee = enemyGo.GetComponentInChildren<Enemy_Melee>();
            Assert.IsNotNull(melee, "Enemy_Melee 없음");

            // 추격(이동) 상태로 강제 → 플레이어로 가속 이동
            melee.stateMachine.ChangeState(melee.chaseState);

            bool sawMoving = false, sawBelowOne = false, relationHeld = true;
            for (int i = 0; i < 90; i++)
            {
                yield return null;
                if (melee == null || melee.agent == null) break;

                var st = melee.stateMachine.currentState;
                Vector3 v = melee.agent.velocity; v.y = 0f;
                if (st != null && st.IsLocomotion && v.magnitude > 0.4f)
                {
                    sawMoving = true;
                    float expected = LocomotionAnim.PlaybackSpeed(v.magnitude, melee.agent.speed);
                    if (Mathf.Abs(expected - melee.anim.speed) > 0.12f)
                        relationHeld = false;
                    if (melee.anim.speed < 0.95f)
                        sawBelowOne = true;
                }
            }

            Assert.IsTrue(sawMoving, "적이 이동(locomotion+속도>0.4) 상태에 도달하지 못함");
            Assert.IsTrue(relationHeld, "이동 중 anim.speed가 속도-비율 시임과 불일치");
            Assert.IsTrue(sawBelowOne, "가속 중에도 anim.speed가 1 미만으로 안 떨어짐(폴리시 미구동 의심)");
        }
    }
}
