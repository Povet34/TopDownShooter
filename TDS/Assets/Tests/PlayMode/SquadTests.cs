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

            // NoisePing은 static이라 테스트 간 상태가 남는다 → 두 채널을 들리지 않게 중화(이전 테스트 핑 오염 방지).
            NoisePing.EmitMuzzle(new Vector3(9999f, 0f, 9999f), 0.01f);
            NoisePing.EmitImpact(new Vector3(9999f, 0f, 9999f), 0.01f);

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

        // §6.2.1 분대 소음 조사: 분대원이 소음을 들으면 분대가 그 지점을 조사 목표(앵커)로 삼아 그쪽으로 향한다.
        // (멤버가 앵커를 따라 이동하는 것은 로코모션의 일이고, 여기선 분대 조사 로직을 검증.)
        [UnityTest]
        public IEnumerator Squad_targets_heard_noise_for_investigation()
        {
            yield return BuildSquad(3);
            var squad = squadGo.GetComponent<Squad>();
            var fAnchor = typeof(Squad).GetField("patrolAnchor", BindingFlags.NonPublic | BindingFlags.Instance);

            Vector3 noise = new Vector3(8f, 0f, 16f); // 테스트 바닥(±20) 안

            // 발포음 발신(분대 발포음 청각 50m로 들림) → 분대 조사 시작
            for (int i = 0; i < 6; i++) { NoisePing.EmitMuzzle(noise, 3f); yield return null; }
            Assert.IsTrue(squad.Investigating, "소음을 듣고도 분대가 조사를 시작하지 않음");

            yield return null; yield return null; // 앵커 갱신 한두 틱

            Vector3 anchor = (Vector3)fAnchor.GetValue(squad);
            float dist = new Vector2(anchor.x - noise.x, anchor.z - noise.z).magnitude;
            Assert.Less(dist, 5f, $"분대 조사 앵커가 소음 지점으로 향하지 않음 anchor=({anchor.x:0.0},{anchor.z:0.0}) noise=({noise.x:0.0},{noise.z:0.0})");
        }

        // §6.2.1 멤버가 갱신된 조사 지점을 추종: 처음 지점(A)으로 끝까지 가지 않고, 조사 지점이 B로
        // 바뀌면 이동 중에도 B로 재추종한다(MoveState가 분대 앵커를 주기적으로 재설정).
        [UnityTest]
        public IEnumerator Squad_members_follow_updated_investigate_target()
        {
            yield return BuildSquad(2);
            foreach (var m in members) if (m != null) m.idleTime = 0.1f;
            var squad = squadGo.GetComponent<Squad>();

            Vector3 A = new Vector3(18f, 0f, 10f);
            Vector3 B = new Vector3(-15f, 0f, 10f);

            // A로 조사 시작 + 멤버를 이동 상태로(하네스의 stale idle 우회)
            squad.OnMemberHeardNoise(A);
            yield return null; yield return null; // Squad.Update가 앵커=A 설정
            foreach (var m in members) { var mm = m as Enemy_Melee; if (mm != null) mm.stateMachine.ChangeState(mm.moveState); }

            float t = 0f; while (t < 1.5f) { yield return null; t += Time.deltaTime; }
            Assert.Less(AvgMemberDestDist(A), AvgMemberDestDist(B), "초기엔 멤버 목적지가 A쪽이어야");

            // 조사 지점을 B로 갱신 → 멤버 목적지가 B로 따라와야(처음 A로 끝까지 가지 않음)
            squad.OnMemberHeardNoise(B);
            t = 0f; while (t < 1.5f) { yield return null; t += Time.deltaTime; }
            Assert.Less(AvgMemberDestDist(B), AvgMemberDestDist(A), "갱신된 지점(B)으로 재추종하지 않고 A에 머묾");
        }

        private float AvgMemberDestDist(Vector3 p)
        {
            float sum = 0f; int n = 0;
            foreach (var m in members)
                if (m != null && m.agent != null) { sum += Vector3.Distance(m.agent.destination, p); n++; }
            return n > 0 ? sum / n : 999f;
        }

        // §6.2.1 분대 발포음 청각: 분대원은 발포음(muzzle) 반경이 작아도 squadHearingRadius(50m) 안이면 듣는다.
        [UnityTest]
        public IEnumerator Squad_member_hears_distant_gunshot_within_hearing_radius()
        {
            yield return BuildSquad(1);
            var e = members[0];
            Assert.AreEqual(PerceptionState.Patrol, e.PerceptionState);

            // 멤버에서 ~40m 떨어진 곳에 아주 작은 반경(2)의 발포음 — 일반 가청이면 안 들리지만 분대 발포음 청각(50m)이면 들림
            Vector3 far = e.transform.position + new Vector3(0f, 0f, 40f);
            for (int i = 0; i < 6; i++)
            {
                NoisePing.EmitMuzzle(far, 2f);
                NoisePing.EmitImpact(new Vector3(9999f, 0f, 9999f), 0.01f);
                yield return null;
            }

            Assert.AreEqual(PerceptionState.Alert, e.PerceptionState, "분대원이 50m 안의 작은 발포음을 못 들음");
        }

        // §6.2.1 피격음은 근거리(발신 반경 ~10m)만 — 분대 청각 부스트 미적용. 12m 밖 피격음은 안 들림.
        [UnityTest]
        public IEnumerator Impact_noise_is_not_boosted_by_squad_hearing()
        {
            yield return BuildSquad(1);
            var e = members[0];
            Assert.AreEqual(PerceptionState.Patrol, e.PerceptionState);

            // 12m 떨어진 피격음(반경 10) — 분대여도 부스트 안 되므로 안 들려야(거리 12 > 10)
            Vector3 far = e.transform.position + new Vector3(0f, 0f, 12f);
            for (int i = 0; i < 6; i++)
            {
                NoisePing.EmitMuzzle(new Vector3(9999f, 0f, 9999f), 0.01f);
                NoisePing.EmitImpact(far, 10f);
                yield return null;
            }

            float d = Vector3.Distance(e.transform.position, NoisePing.Impact.position);
            Assert.AreEqual(PerceptionState.Patrol, e.PerceptionState,
                $"12m 밖 피격음에 반응함(피격음은 ~10m만이어야). dist={d:0.0} impactR={NoisePing.Impact.radius} muzzleR={NoisePing.Muzzle.radius} muzzleAge={(Time.time-NoisePing.Muzzle.time):0.00}");
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
