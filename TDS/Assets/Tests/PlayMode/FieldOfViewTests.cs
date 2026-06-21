using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TDS.Tests.PlayMode
{
    /// <summary>FoV 가시성: 콘 안+LoS 확보=보임, 차폐 뒤=숨김, 사거리/콘 밖=숨김.</summary>
    public class FieldOfViewTests
    {
        private readonly List<GameObject> created = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var g in created)
                if (g != null) Object.DestroyImmediate(g);
            created.Clear();
            yield return null;
        }

        private FieldOfView MakeFoV()
        {
            var go = new GameObject("FoV");
            go.transform.position = Vector3.zero;
            go.transform.forward = Vector3.forward; // +z 바라봄
            created.Add(go);
            return go.AddComponent<FieldOfView>();
        }

        [UnityTest]
        public IEnumerator Cone_and_range_gate_visibility()
        {
            var fov = MakeFoV();
            yield return null;

            Assert.IsTrue(fov.IsVisible(new Vector3(0, 0, 5)), "정면 근거리 + 가림 없음 → 보여야");
            Assert.IsFalse(fov.IsVisible(new Vector3(0, 0, -5)), "뒤 → 안 보여야");
            Assert.IsFalse(fov.IsVisible(new Vector3(0, 0, 30)), "사거리 밖 → 안 보여야");
            Assert.IsFalse(fov.IsVisible(new Vector3(20, 0, 0)), "콘 밖(옆) → 안 보여야");
        }

        [UnityTest]
        public IEnumerator Occluder_blocks_line_of_sight()
        {
            var fov = MakeFoV();

            // 플레이어(0)와 대상(0,0,8) 사이 벽
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 1.5f, 4f);
            wall.transform.localScale = new Vector3(4f, 3f, 1f);
            created.Add(wall);
            Physics.SyncTransforms(); // 콜라이더를 실제 위치로(미동기 시 원점에 있어 레이가 벽 안에서 시작)
            yield return null;

            Assert.IsFalse(fov.IsVisible(new Vector3(0, 0, 8)), "벽 뒤 대상 → 가려 안 보여야");
            // 벽을 비껴간 같은 거리 대각 지점은 보임
            Assert.IsTrue(fov.IsVisible(new Vector3(6, 0, 6)), "벽을 비껴간 시야는 보여야");
        }

        [UnityTest]
        public IEnumerator Adjacent_enemy_is_seen_even_behind()
        {
            var fov = MakeFoV();
            yield return null;
            // 바로 뒤(콘 밖)지만 nearRadius(2.5) 안 → 인지
            Assert.IsTrue(fov.IsVisible(new Vector3(0, 0, -1.5f)), "인접 적은 콘 밖이라도 보여야");
        }

        [UnityTest]
        public IEnumerator Reveal_temporarily_extends_range()
        {
            var fov = MakeFoV();
            yield return null;

            Assert.IsFalse(fov.IsVisible(new Vector3(0, 0, 22)), "기본 사거리(18) 밖");
            fov.Reveal(); // 발사 → 사거리 +6 = 24
            Assert.IsTrue(fov.IsVisible(new Vector3(0, 0, 22)), "발사 후 확대 사거리 안 → 보여야");
        }
    }
}
