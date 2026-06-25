using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace TDS.Tests.PlayMode
{
    /// <summary>
    /// "장애물이 많으면 몬스터 이동에 장애가 생기나?" 검증.
    /// 절차 맵의 navmesh가 충분히 연결돼 있어야 가장자리 스폰 분대가 목표(앵커)로 경로를 찾는다.
    /// 절벽/장애물이 영역을 통째로 가두면 연결성이 떨어져 분대가 끼인다(실제로 본 증상).
    /// 바닥 레벨 navmesh 점들을 무작위로 골라 경로 완성률을 측정 → 임계 미만이면 실패.
    /// </summary>
    public class NavMeshConnectivityTests
    {
        private GameObject go;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
            foreach (var s in Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (s != null && s.gameObject != null) Object.DestroyImmediate(s.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Default_density_map_navmesh_is_well_connected()
        {
            go = new GameObject("MapGen");
            go.SetActive(false);
            var mg = go.AddComponent<MapGenerator>();
            var bf = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(MapGenerator).GetField("generateOnStart", bf).SetValue(mg, false);

            // 실제 게임과 같은 스케일/밀도(MapConfig_Default와 동기화). 이 밀도에서 연결성이 유지돼야 함.
            var cfg = ScriptableObject.CreateInstance<MapConfig>();
            cfg.cellSize = 4f; cfg.gridWidth = 256; cfg.gridHeight = 256; // 1024 x 1024
            cfg.obstacleCount = 600; cfg.coverCount = 40; cfg.barrelCount = 20; cfg.centerClearRadius = 6f;
            cfg.clusterCount = 30; cfg.clusterSize = 10; cfg.clusterRadius = 6f;
            cfg.interiorWallCount = 15; cfg.interiorWallLength = 12f;
            cfg.interiorCliffCount = 10; cfg.cliffHeight = 10f; cfg.cliffMinFootprint = 4f; cfg.cliffMaxFootprint = 8f;
            typeof(MapGenerator).GetField("config", bf).SetValue(mg, cfg);

            var surface = go.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            typeof(MapGenerator).GetField("navMeshSurface", bf).SetValue(mg, surface);

            go.SetActive(true);
            mg.Generate(7);
            yield return null;

            var b = mg.LastBounds;
            float hx = b.size.x * 0.5f - 8f, hz = b.size.z * 0.5f - 8f;
            var rng = new System.Random(123);
            var pts = new List<Vector3>();
            for (int i = 0; i < 1500 && pts.Count < 150; i++)
            {
                var p = new Vector3((float)(rng.NextDouble() * 2 - 1) * hx, 0f, (float)(rng.NextDouble() * 2 - 1) * hz);
                if (NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas) && hit.position.y < 1.5f)
                    pts.Add(hit.position); // 바닥 레벨만(절벽 위 고립 섬 제외)
            }
            Assert.Greater(pts.Count, 50, "navmesh 샘플 점이 너무 적음(베이크 실패?)");

            int trials = 0, complete = 0;
            var path = new NavMeshPath();
            for (int i = 0; i < 400 && pts.Count > 2; i++)
            {
                var a = pts[rng.Next(pts.Count)];
                var c = pts[rng.Next(pts.Count)];
                if ((a - c).sqrMagnitude < 1f) continue;
                trials++;
                if (NavMesh.CalculatePath(a, c, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
                    complete++;
            }
            float rate = trials > 0 ? (float)complete / trials : 0f;

            Object.DestroyImmediate(cfg);
            Assert.Greater(rate, 0.80f,
                $"navmesh 연결성 {rate:P0} (<80%) — 장애물/절벽이 영역을 가둬 분대가 목표로 이동 못 함");
        }
    }
}
