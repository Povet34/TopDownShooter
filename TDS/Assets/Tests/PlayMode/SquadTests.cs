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
    /// 분대 공유 인지(§6.2 그룹): 뭉쳐 스폰된 적은 의식을 공유 — 한 명이 발각/피격되면 전원 교전.
    /// </summary>
    public class SquadTests
    {
        private GameObject floor, player, squadGo;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private List<Enemy> members;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var g in spawned) if (g != null) Object.DestroyImmediate(g);
            spawned.Clear();
            foreach (var g in new[] { squadGo, player, floor })
                if (g != null) Object.DestroyImmediate(g);
            foreach (var s in Object.FindObjectsByType<Unity.AI.Navigation.NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(s.gameObject);
            if (GameBootstrap.Instance != null) Object.DestroyImmediate(GameBootstrap.Instance.gameObject);
            GameServices.ResetForTests();
            yield return null;
        }

        private IEnumerator BuildSquad(int n)
        {
            GameServices.ResetForTests();
            GameBootstrap.EnsureSystems();

            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.localScale = new Vector3(40f, 1f, 40f);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            var surface = floor.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
            surface.collectObjects = Unity.AI.Navigation.CollectObjects.All;
            surface.BuildNavMesh();
            yield return null;

            player = Object.Instantiate(Resources.Load<GameObject>("Player"));
            player.name = "Player";
            player.transform.position = Vector3.zero;
            yield return null;

            var table = Resources.Load<SpawnTable>("ST_Basic");
            squadGo = new GameObject("Squad");
            var squad = squadGo.AddComponent<Squad>();
            members = new List<Enemy>();
            for (int i = 0; i < n; i++)
            {
                // 플레이어(원점) 반대편(+x)을 보게 둬서 각자는 플레이어를 못 본다 → 공유로만 교전 확인
                var go = Object.Instantiate(table.entries[0].prefab, new Vector3(6f + i * 2f, 0f, 6f), Quaternion.LookRotation(Vector3.right));
                spawned.Add(go);
                var e = go.GetComponentInChildren<Enemy>();
                squad.Register(e);
                members.Add(e);
            }
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator One_member_hit_engages_whole_squad()
        {
            yield return BuildSquad(3);

            // 사전: 아무도 플레이어를 안 봄(뒤돌아 있음) → 미교전
            foreach (var m in members)
                Assert.IsFalse(m.inBattleMode, "초기엔 미교전이어야(뒤돌아 있음)");

            // 한 명만 피격
            members[0].GetHit(2);

            for (int i = 0; i < 15; i++) yield return null;

            // 분대 공유: 맞지 않은 나머지도 전원 교전
            foreach (var m in members)
                Assert.IsTrue(m.inBattleMode, "분대원 한 명 피격 시 전원 교전해야(공유 인지)");
        }

        // §6.2.1 피격음: 발사음(muzzle)을 못 들었어도 총알이 근처에 박히면(impact) 그쪽을 조사(경계).
        [UnityTest]
        public IEnumerator Impact_noise_alone_makes_member_investigate()
        {
            yield return BuildSquad(1);
            var e = members[0];
            Assert.AreEqual(PerceptionState.Patrol, e.PerceptionState, "초기엔 순찰");

            // 총구음은 멀리/작게(안 들림), 피격음만 적 위에 발신
            for (int i = 0; i < 6; i++)
            {
                NoisePing.EmitMuzzle(new Vector3(9999f, 0f, 9999f), 0.01f);
                NoisePing.EmitImpact(e.transform.position, 10f);
                yield return null;
            }

            Assert.AreEqual(PerceptionState.Alert, e.PerceptionState, "피격음 들으면 경계(조사)로 전환");
        }

        // §6.3.2 순찰 방향 고정: 분대 앵커가 플레이어를 추적하지 않고 처음 방향(축)을 유지한다.
        [UnityTest]
        public IEnumerator Patrol_direction_stays_fixed_not_homing_on_player()
        {
            yield return BuildSquad(3);
            foreach (var m in members) if (m != null) m.idleTime = 0.2f; // 자주 재이동 → 앵커 여러 번 전진

            var squad = squadGo.GetComponent<Squad>();
            var fDir = typeof(Squad).GetField("patrolDir", BindingFlags.NonPublic | BindingFlags.Instance);
            var fInit = typeof(Squad).GetField("patrolInit", BindingFlags.NonPublic | BindingFlags.Instance);

            // 순찰 방향 초기화 대기
            float w = 0f;
            while (!(bool)fInit.GetValue(squad) && w < 2f) { yield return null; w += Time.deltaTime; }
            Assert.IsTrue((bool)fInit.GetValue(squad), "순찰 방향이 초기화되지 않음");
            Vector3 dir0 = (Vector3)fDir.GetValue(squad);

            // 여러 번 전진할 시간(플레이어는 원점에 있음 — 옛 동작이면 그쪽으로 방향이 휘어짐)
            float t = 0f;
            while (t < 3f) { yield return null; t += Time.deltaTime; }
            Vector3 dir1 = (Vector3)fDir.GetValue(squad);

            // 축이 회전하지 않음 = 같거나 정확히 반전(벽 반사)만 허용. 플레이어로 휘면 |dot| < 1.
            float dot = Vector3.Dot(dir0.normalized, dir1.normalized);
            Assert.GreaterOrEqual(Mathf.Abs(dot), 0.999f, $"순찰 방향이 회전함(플레이어 추적 의심) dot={dot:0.000}");
        }

        // §6.2.1 분대 청각: 분대원은 소음 반경이 작아도(여기선 2) squadHearingRadius(기본 50m) 안이면 듣는다.
        [UnityTest]
        public IEnumerator Squad_member_hears_quiet_noise_within_hearing_radius()
        {
            yield return BuildSquad(1);
            var e = members[0];
            Assert.AreEqual(PerceptionState.Patrol, e.PerceptionState);

            // 멤버에서 ~40m 떨어진 곳에 아주 작은 반경(2)의 피격음 — 일반 가청이면 안 들리지만 분대 청각(50m)이면 들림
            Vector3 far = e.transform.position + new Vector3(0f, 0f, 40f);
            for (int i = 0; i < 6; i++)
            {
                NoisePing.EmitMuzzle(new Vector3(9999f, 0f, 9999f), 0.01f);
                NoisePing.EmitImpact(far, 2f);
                yield return null;
            }

            Assert.AreEqual(PerceptionState.Alert, e.PerceptionState, "분대원이 50m 안의 작은 소음을 못 들음");
        }

        // 피격음이 가청 반경 밖이면 반응하지 않는다(거짓 양성 방지).
        [UnityTest]
        public IEnumerator Distant_impact_noise_is_ignored()
        {
            yield return BuildSquad(1);
            var e = members[0];

            for (int i = 0; i < 6; i++)
            {
                NoisePing.EmitMuzzle(new Vector3(9999f, 0f, 9999f), 0.01f);
                NoisePing.EmitImpact(e.transform.position + new Vector3(60f, 0f, 0f), 8f); // 반경 8, 거리 60
                yield return null;
            }

            Assert.AreEqual(PerceptionState.Patrol, e.PerceptionState, "먼 피격음엔 반응 없음");
        }
    }
}
