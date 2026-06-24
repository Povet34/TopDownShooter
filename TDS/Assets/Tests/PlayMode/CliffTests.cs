using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace TDS.Tests.PlayMode
{
    /// <summary>
    /// 절벽(못 올라가는 지형)이 navmesh에서 제외돼 엔티티가 들어가지 못하는지 검증.
    /// 가파른(높은) 블록은 navmesh 경사/높이 기준에서 빠지므로 그 발밑은 못 걷는 영역이 된다.
    /// </summary>
    public class CliffTests
    {
        private readonly List<GameObject> created = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var g in created) if (g != null) Object.DestroyImmediate(g);
            created.Clear();
            foreach (var s in Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (s != null) Object.DestroyImmediate(s.gameObject);
            yield return null;
        }

        private static float PathLength(NavMeshPath path)
        {
            float len = 0f;
            var c = path.corners;
            for (int i = 1; i < c.Length; i++) len += Vector3.Distance(c[i - 1], c[i]);
            return len;
        }

        // 절벽이 바닥 navmesh를 막아, 절벽을 가로지르는 직선 경로가 우회로 강제되는지 검증.
        // (엔티티는 절벽 안으로 못 들어가고 돌아간다 = 동선 차단.)
        [UnityTest]
        public IEnumerator Cliff_blocks_ground_path_forcing_detour()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.localScale = new Vector3(40f, 1f, 40f);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            created.Add(floor);

            // x 5..11, z -3..3 을 막는 높은 절벽 — A(0,0,0)→B(16,0,0) 직선(z=0) 위에 놓임.
            var cliff = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cliff.transform.localScale = new Vector3(6f, 10f, 6f);
            cliff.transform.position = new Vector3(8f, 5f, 0f);
            created.Add(cliff);

            var surface = floor.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.BuildNavMesh();
            yield return null;

            Assert.IsTrue(NavMesh.SamplePosition(new Vector3(0f, 0f, 0f), out var a, 2f, NavMesh.AllAreas),
                "A 지점이 navmesh에 없음(전제)");
            Assert.IsTrue(NavMesh.SamplePosition(new Vector3(16f, 0f, 0f), out var b, 2f, NavMesh.AllAreas),
                "B 지점이 navmesh에 없음(전제)");

            var path = new NavMeshPath();
            bool ok = NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path);
            Assert.IsTrue(ok && path.status == NavMeshPathStatus.PathComplete,
                "A→B 경로가 완성되지 않음(절벽을 돌아갈 수 있어야)");

            float straight = Vector3.Distance(a.position, b.position);
            float actual = PathLength(path);
            // 직선이면 ~16. 절벽을 돌면 분명히 더 길다.
            Assert.Greater(actual, straight + 1.5f,
                $"경로가 절벽을 가로질러 직진함(우회 없음) — straight={straight:F1} actual={actual:F1}. 절벽이 바닥을 막지 못함");
        }
    }
}
