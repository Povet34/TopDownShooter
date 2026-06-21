using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Unity.AI.Navigation;
using TDS.Core;

namespace TDS.Tests.PlayMode
{
    /// <summary>
    /// Cover 검증 툴 — 각 cover가 range 적에게 적당한지(도달 가능 지점 + 높이 분류) 확인.
    /// 낮은 단상(≤0.8)=ShootFrom, 높은 것=HideOnly, navmesh 밖(도달 불가)=Unusable.
    /// </summary>
    public class CoverAuditTests
    {
        private readonly List<GameObject> created = new List<GameObject>();
        private GameObject coverPointTemplate;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var go in created)
                if (go != null) Object.DestroyImmediate(go);
            created.Clear();
            if (coverPointTemplate != null) Object.DestroyImmediate(coverPointTemplate);

            foreach (var c in Object.FindObjectsByType<Cover>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(c.gameObject);
            yield return null;
        }

        private GameObject MakeCover(Vector3 groundPos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Cover";
            go.transform.localScale = scale;
            go.transform.position = new Vector3(groundPos.x, scale.y * 0.5f, groundPos.z); // 땅에 붙임
            var cover = go.AddComponent<Cover>();
            cover.Configure(coverPointTemplate, 1.5f, 1.5f);
            created.Add(go);
            return go;
        }

        [UnityTest]
        public IEnumerator Covers_are_classified_by_height_and_reachability()
        {
            // 1. 바닥 + navmesh 베이크 (40×40)
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "TestFloor";
            floor.transform.localScale = new Vector3(40f, 1f, 40f);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            created.Add(floor);

            var surface = floor.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.BuildNavMesh();
            yield return null;

            // 엄폐 지점 마커 템플릿(CoverPoint 컴포넌트)
            coverPointTemplate = new GameObject("CoverPointTemplate");
            coverPointTemplate.AddComponent<CoverPoint>();
            coverPointTemplate.SetActive(false);

            // 2. 낮은 단상(≤0.8) — 사격 가능
            var low = MakeCover(new Vector3(6f, 0f, 0f), new Vector3(1.6f, 0.7f, 1.6f));
            // 3. 높은 cover — 은폐 전용
            var high = MakeCover(new Vector3(-6f, 0f, 0f), new Vector3(2f, 2.5f, 2f));
            // 4. navmesh 밖 — 도달 불가
            var far = MakeCover(new Vector3(500f, 0f, 500f), new Vector3(1.6f, 0.7f, 1.6f));

            // Cover.Start(높이 계산 + 엄폐점 navmesh 샘플)가 돌도록 대기
            yield return null;
            yield return null;

            var lowCover = low.GetComponent<Cover>();
            var highCover = high.GetComponent<Cover>();
            var farCover = far.GetComponent<Cover>();

            Assert.AreEqual(CoverSuitability.ShootFrom, lowCover.AuditForRange(),
                $"낮은 단상이 사격용으로 분류돼야 함 (height={lowCover.CoverHeight:0.00}, points={lowCover.CoverPointCount})");
            Assert.AreEqual(CoverSuitability.HideOnly, highCover.AuditForRange(),
                $"높은 cover는 은폐 전용이어야 함 (height={highCover.CoverHeight:0.00})");
            Assert.AreEqual(CoverSuitability.Unusable, farCover.AuditForRange(),
                "navmesh 밖 cover는 도달 불가(Unusable)여야 함");

            // 낮은 단상은 실제로 도달 가능한 엄폐점이 있어야 함(비비는 버그 방지의 핵심)
            Assert.Greater(lowCover.CoverPointCount, 0, "낮은 단상에 도달 가능한 엄폐점이 없음");
            Assert.IsTrue(lowCover.IsShootable, "낮은 단상이 사격 가능으로 표시되지 않음");
            Assert.IsFalse(highCover.IsShootable, "높은 cover가 사격 가능으로 잘못 표시됨");
        }
    }
}
