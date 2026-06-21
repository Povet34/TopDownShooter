using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TDS.Core;

namespace TDS.Tests.PlayMode
{
    /// <summary>
    /// 엄폐 시스템 통합 검증: 엄폐 perk를 가진 원거리 적이 맵의 Cover를 찾아 유효 엄폐 지점을 점유한다.
    /// (Phase B에서 절차 맵의 엄폐물에 Cover 컴포넌트를 붙이고 Cover 변형 prefab의 perk를 켠 것.)
    /// </summary>
    public class CoverTests
    {
        private GameObject floor, player, coverGo, cpPrefab, enemyGo;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var g in new[] { enemyGo, coverGo, cpPrefab, player, floor })
                if (g != null) Object.DestroyImmediate(g);
            foreach (var s in Object.FindObjectsByType<Unity.AI.Navigation.NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(s.gameObject);
            if (GameBootstrap.Instance != null)
                Object.DestroyImmediate(GameBootstrap.Instance.gameObject);
            GameServices.ResetForTests();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Ranged_cover_enemy_acquires_cover()
        {
            GameServices.ResetForTests();
            GameBootstrap.EnsureSystems();

            // 적 AI navmesh
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "TestFloor";
            floor.transform.localScale = new Vector3(40f, 1f, 40f);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            var surface = floor.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
            surface.collectObjects = Unity.AI.Navigation.CollectObjects.All;
            surface.BuildNavMesh();
            yield return null;

            // 플레이어(원점)
            player = Object.Instantiate(Resources.Load<GameObject>("Player"));
            player.name = "Player";
            player.transform.position = Vector3.zero;
            yield return null;

            // 런타임 CoverPoint 마커 프리팹
            cpPrefab = new GameObject("CP_Prefab");
            cpPrefab.AddComponent<CoverPoint>();

            // 엄폐물 (6,0,0) — 낮은 단상(≤0.8, 사격 가능)이라야 교전용 cover로 선택됨
            coverGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            coverGo.name = "TestCover";
            coverGo.transform.position = new Vector3(6f, 0.35f, 0f);
            coverGo.transform.localScale = new Vector3(1.6f, 0.7f, 1.6f);
            var cover = coverGo.AddComponent<Cover>();
            cover.Configure(cpPrefab, 1.5f, 1.5f);
            yield return null; // Cover.Start → 4 지점 생성
            yield return null;

            // 엄폐 변형 적 (Resources의 ST_RangedDefense에서)
            var table = Resources.Load<SpawnTable>("ST_RangedDefense");
            Assert.IsNotNull(table, "Resources/ST_RangedDefense 로드 실패");
            GameObject coverPrefab = null;
            foreach (var e in table.entries)
                if (e != null && e.prefab != null && e.prefab.name.Contains("Cover")) { coverPrefab = e.prefab; break; }
            Assert.IsNotNull(coverPrefab, "테이블에서 Cover 변형 prefab을 못 찾음");

            enemyGo = Object.Instantiate(coverPrefab, new Vector3(5f, 0f, 0f), Quaternion.identity);
            enemyGo.name = "TestCoverEnemy";
            yield return null;
            yield return null; // Awake/Start 초기화

            var er = enemyGo.GetComponentInChildren<Enemy_Range>();
            Assert.IsNotNull(er, "Enemy_Range 컴포넌트 없음");
            Assert.AreNotEqual(CoverPerk.Unavalible, er.coverPerk, "Cover 변형인데 coverPerk가 Unavalible");

            er.EnterBattleMode(); // CanGetCover → 엄폐 탐색 → currentCover 설정
            yield return null;

            Assert.IsNotNull(er.currentCover, "엄폐 perk 적이 Cover를 못 찾음(currentCover null)");
        }
    }
}
