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
    [Range(0f, 0.6f)] public float obstacleDensity = 0.12f;
    [Min(0)] public int coverCount = 12;
    [Tooltip("중앙 플레이어 스폰 주변은 비워둠 (월드 단위 반경)")]
    [Min(0f)] public float centerClearRadius = 6f;

    [Header("프리팹 (비우면 프리미티브로 폴백)")]
    public GameObject floorPrefab;
    public GameObject wallPrefab;
    public List<GameObject> obstaclePrefabs = new List<GameObject>();
    [Tooltip("Cover 컴포넌트가 미리 붙은 엄폐물 프리팹(권장). 비우면 프리미티브 큐브로 대체")]
    public GameObject coverPrefab;
    [Tooltip("엄폐 지점 마커 프리팹(CoverPoint 컴포넌트). 프리미티브 폴백 엄폐물에 런타임 부착용 — Prefab/Enemy_CoverSystem/CoverPoint 권장")]
    public GameObject coverPointPrefab;
}
