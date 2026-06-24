using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 절차적 맵 생성 파라미터(데이터). 여러 개 만들어 맵 프리셋/테마로 사용한다.
/// 프리팹 필드를 비우면 MapGenerator가 프리미티브로 폴백한다(즉시 검증용).
/// </summary>
[CreateAssetMenu(fileName = "MapConfig", menuName = "TDS/Map/Map Config")]
public class MapConfig : ScriptableObject
{
    [Header("결정성")]
    public int defaultSeed = 12345;

    [Header("그리드")]
    [Min(0.5f)] public float cellSize = 4f;
    [Min(4)] public int gridWidth = 16;
    [Min(4)] public int gridHeight = 16;
    [Min(0.5f)] public float wallHeight = 3f;

    [Header("콘텐츠 밀도")]
    [Tooltip("장애물 개수(>0이면 obstacleDensity 대신 이 수만큼만 배치 — 큰 맵 성능). 0이면 셀별 확률 사용")]
    [Min(0)] public int obstacleCount = 0;
    [Range(0f, 0.6f)] public float obstacleDensity = 0.12f;
    [Min(0)] public int coverCount = 12;
    [Tooltip("중앙 플레이어 스폰 주변은 비워둠 (월드 단위 반경)")]
    [Min(0f)] public float centerClearRadius = 6f;

    [Header("복잡도 — 군집 / 내부 구조")]
    [Tooltip("장애물 군집(밀집 포켓) 수. 균일 산포에 더해 덜 휑하게")]
    [Min(0)] public int clusterCount = 0;
    [Tooltip("군집 1개당 장애물 수")]
    [Min(1)] public int clusterSize = 8;
    [Tooltip("군집 반경(월드 단위)")]
    [Min(1f)] public float clusterRadius = 6f;
    [Tooltip("내부 벽 세그먼트 수(초크포인트/엄폐선 — 구조 복잡도)")]
    [Min(0)] public int interiorWallCount = 0;
    [Tooltip("내부 벽 1개 길이(월드 단위)")]
    [Min(1f)] public float interiorWallLength = 12f;

    [Header("프리팹 (비우면 프리미티브로 폴백)")]
    public GameObject floorPrefab;
    public GameObject wallPrefab;
    public List<GameObject> obstaclePrefabs = new List<GameObject>();
    [Tooltip("높은 엄폐물(은폐 전용) 프리팹. Cover 컴포넌트가 미리 붙은 것 권장. 비우면 프리미티브 큐브로 대체")]
    public GameObject coverPrefab;
    [Tooltip("낮은 단상(range가 그 위로 사격) 프리팹 — 높이 ≤0.8 권장. 비우면 프리미티브 큐브로 대체")]
    public GameObject lowCoverPrefab;
    [Range(0f, 1f)]
    [Tooltip("전체 cover 중 낮은 단상(사격 가능) 비율. 나머지는 높은 은폐용")]
    public float lowCoverRatio = 0.6f;
    [Range(0.2f, 0.8f)]
    [Tooltip("프리미티브 낮은 단상 높이(상한 0.8 — 넘으면 총구가 cover에 박혀 못 쏨)")]
    public float lowCoverHeight = 0.7f;
    [Tooltip("엄폐 지점 마커 프리팹(CoverPoint 컴포넌트). 프리미티브 폴백 엄폐물에 런타임 부착용 — Prefab/Enemy_CoverSystem/CoverPoint 권장")]
    public GameObject coverPointPrefab;
    [Tooltip("엄폐 지점 오프셋(x=좌우, y=전후). 엄폐물 풋프린트에 맞춰 4지점이 박스 '밖'에 놓이도록. 예: sea_container ≈ (1.5, 2.8)")]
    public Vector2 coverPointOffset = new Vector2(1.5f, 1.5f);

    [Header("배럴 (movable + breakable 프롭)")]
    [Min(0)]
    [Tooltip("총알에 밀리고 누적 피해로 부서지는 프롭 수")]
    public int barrelCount = 6;
    [Tooltip("배럴 프리팹. 비우면 프리미티브 실린더(≤0.9)로 대체")]
    public GameObject barrelPrefab;

    [Header("주변만 렌더링 (거리 컬링)")]
    [Tooltip("플레이어에서 이 반경 밖 맵 오브젝트는 비활성(렌더+물리 절약 — 밀도 높여도 깔끔). 0이면 끔. 카메라 시야보다 넉넉히 크게(예 70). 바닥/navmesh는 영향 없음.")]
    [Min(0f)] public float cullRadius = 0f;

    [Header("바닥 머티리얼 (프리미티브 바닥용)")]
    [Tooltip("프리미티브 바닥에 입힐 머티리얼(사막 모래 등). 비우면 기본 흰색")]
    public Material floorMaterial;
    [Tooltip("바닥 텍스처를 이 월드 단위마다 1회 반복(큰 맵에서 늘어남 방지). 0이면 타일링 안 함")]
    [Min(0f)] public float floorTileWorldUnits = 0f;
}
