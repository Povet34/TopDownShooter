using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TDS.Tests.PlayMode
{
    /// <summary>
    /// 전장의 안개 가시성 폴리곤(GPU 마스크의 입력): 시야 콘 안은 멀리(사거리/장애물까지) 뻗고,
    /// 시야 밖(뒤)은 nearRadius 근처, 장애물은 폴리곤을 잘라낸다. 렌더 무관하게 메시 정점으로 검증.
    /// </summary>
    public class VisionMaskTests
    {
        private readonly List<GameObject> created = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var g in created)
                if (g != null) Object.DestroyImmediate(g);
            created.Clear();
            foreach (var n in new[] { "VisionFogQuad", "VisionMesh", "VisionMaskCamera" })
            {
                var o = GameObject.Find(n);
                if (o != null) Object.DestroyImmediate(o);
            }
            yield return null;
        }

        private VisionMask MakeMask()
        {
            var go = new GameObject("FoV");
            go.transform.position = Vector3.zero;
            go.transform.forward = Vector3.forward;
            go.AddComponent<FieldOfView>();
            created.Add(go);
            return go.AddComponent<VisionMask>();
        }

        private static float DistInDir(Vector3[] verts, Vector3 player, Vector3 dir, bool max)
        {
            float best = max ? 0f : 999f;
            for (int i = 1; i < verts.Length; i++)
            {
                Vector3 d = verts[i] - player; d.y = 0f;
                float dist = d.magnitude;
                if (dist < 1e-3f) continue;
                if (Vector3.Dot(d.normalized, dir) > 0.95f)
                    best = max ? Mathf.Max(best, dist) : Mathf.Min(best, dist);
            }
            return best;
        }

        [UnityTest]
        public IEnumerator Polygon_reaches_far_in_view_and_near_behind()
        {
            var vm = MakeMask();
            yield return null; // Awake가 메시 빌드
            yield return null;

            var verts = vm.DebugVerts;
            Assert.IsNotNull(verts);

            float front = DistInDir(verts, Vector3.zero, Vector3.forward, max: true);
            float back = DistInDir(verts, Vector3.zero, Vector3.back, max: false);

            Assert.Greater(front, 10f, $"전방(시야 안) 폴리곤이 멀리 뻗어야(={front:0.0})");
            Assert.Less(back, 4f, $"후방(시야 밖) 폴리곤이 nearRadius 근처여야(={back:0.0})");
        }

        [UnityTest]
        public IEnumerator Occluder_cuts_the_polygon()
        {
            var vm = MakeMask();

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 1.5f, 5f);
            wall.transform.localScale = new Vector3(8f, 3f, 1f);
            created.Add(wall);
            Physics.SyncTransforms();
            yield return null;
            yield return null;

            float front = DistInDir(vm.DebugVerts, Vector3.zero, Vector3.forward, max: true);
            Assert.Less(front, 6f, $"전방 폴리곤이 벽(z=5)에서 잘려야(={front:0.0})");
        }
    }
}
