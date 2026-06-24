using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TDS.Tests.PlayMode
{
    /// <summary>
    /// 절차적 맵 생성기 통합 검증: 시드 결정성 + 중앙 스폰존 비움 + 콘텐츠 존재.
    /// config 미할당(프리미티브 폴백) + navMeshSurface 미할당(베이크 스킵)으로 빠르게 돈다.
    /// </summary>
    public class MapGeneratorTests
    {
        private GameObject go;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
            foreach (var c in Object.FindObjectsByType<Cover>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(c.gameObject);
            yield return null;
        }

        /// <summary>generateOnStart 자동 생성이 명시적 Generate(seed)를 덮지 않도록 끈 채로 생성.</summary>
        private MapGenerator MakeGenerator()
        {
            go = new GameObject("MapGen");
            go.SetActive(false);
            var mg = go.AddComponent<MapGenerator>();
            typeof(MapGenerator).GetField("generateOnStart", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(mg, false);
            go.SetActive(true);
            return mg;
        }

        // play 모드에서 PrepareRoot의 DestroySafe는 Destroy(지연)라 옛 MapRoot가 프레임 끝까지 남는다.
        // Find("MapRoot")는 옛 루트를 줄 수 있으므로 항상 최신을 가리키는 private mapRoot 필드를 읽는다.
        private static Transform CurrentRoot(MapGenerator mg)
            => (Transform)typeof(MapGenerator)
                .GetField("mapRoot", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(mg);

        private static List<Vector3> ContentPositions(Transform mapRoot)
        {
            var list = new List<Vector3>();
            foreach (Transform child in mapRoot)
                if (child.name == "Obstacle" || child.name == "Cover")
                    list.Add(child.localPosition);
            return list;
        }

        [UnityTest]
        public IEnumerator Same_seed_produces_identical_layout()
        {
            var mg = MakeGenerator();

            mg.Generate(777);
            var first = ContentPositions(CurrentRoot(mg));
            yield return null;

            mg.Generate(777); // 같은 시드 재생성
            var second = ContentPositions(CurrentRoot(mg));

            Assert.Greater(first.Count, 0, "콘텐츠가 하나도 안 생성됨");
            Assert.AreEqual(first.Count, second.Count, "같은 시드인데 콘텐츠 수가 다름");
            for (int i = 0; i < first.Count; i++)
                Assert.AreEqual(first[i], second[i], $"같은 시드인데 {i}번 위치가 다름");
        }

        [UnityTest]
        public IEnumerator Different_seed_changes_layout()
        {
            var mg = MakeGenerator();

            mg.Generate(1);
            var a = ContentPositions(CurrentRoot(mg));
            yield return null;
            mg.Generate(2);
            var b = ContentPositions(CurrentRoot(mg));

            bool identical = a.Count == b.Count;
            if (identical)
                for (int i = 0; i < a.Count; i++)
                    if (a[i] != b[i]) { identical = false; break; }
            Assert.IsFalse(identical, "다른 시드인데 배치가 동일함");
        }

        [UnityTest]
        public IEnumerator Center_spawn_zone_is_clear()
        {
            var mg = MakeGenerator();
            mg.Generate(12345);
            yield return null;

            float clearR = 6f; // config 기본값
            foreach (var p in ContentPositions(CurrentRoot(mg)))
            {
                float distSqXZ = p.x * p.x + p.z * p.z;
                Assert.GreaterOrEqual(distSqXZ, clearR * clearR - 0.01f,
                    $"중앙 스폰존(반경 {clearR}) 안에 콘텐츠가 있음: {p}");
            }
        }

        // 1024x1024 큰 맵에서도 장애물 수가 obstacleCount로 상한된다(셀별 확률 폭발 방지 = 성능).
        [UnityTest]
        public IEnumerator Large_map_bounds_obstacle_count()
        {
            var mg = MakeGenerator();
            var cfg = ScriptableObject.CreateInstance<MapConfig>();
            cfg.cellSize = 4f; cfg.gridWidth = 256; cfg.gridHeight = 256; // 1024 x 1024
            cfg.obstacleCount = 300; cfg.coverCount = 0; cfg.barrelCount = 0;
            cfg.centerClearRadius = 6f;
            typeof(MapGenerator).GetField("config", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(mg, cfg);

            mg.Generate(123);
            yield return null;

            int obstacles = 0;
            foreach (Transform child in CurrentRoot(mg))
                if (child.name == "Obstacle") obstacles++;

            Assert.LessOrEqual(obstacles, 300, $"장애물이 상한 초과({obstacles}) — 카운트 상한 실패");
            Assert.Greater(obstacles, 250, $"장애물이 너무 적음({obstacles}) — 배치 실패");
            Assert.AreEqual(1024f, mg.LastBounds.size.x, 0.1f, "1024 폭이 아님");

            Object.DestroyImmediate(cfg);
        }

        [UnityTest]
        public IEnumerator Reports_bounds_and_seed()
        {
            var mg = MakeGenerator();
            mg.Generate(42);
            yield return null;

            Assert.AreEqual(42, mg.LastSeed);
            Assert.Greater(mg.LastBounds.halfExtent, 0f, "맵 경계가 0");
            Assert.Greater(mg.LastBounds.size.x, 0f);
        }
    }
}
