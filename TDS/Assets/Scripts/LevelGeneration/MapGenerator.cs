using System;
using Unity.AI.Navigation;
using UnityEngine;

/// <summary>
/// 시드 기반 그리드 절차적 맵 생성기. "맵만" 생성한다 — 바닥/경계벽/장애물/엄폐물 + NavMesh 베이크.
/// 적/플레이어/스포너는 포함하지 않는다(데이터·스포너로 분리, Roadmap §D2).
/// <see cref="MapConfig"/>(SO)로 데이터 조합. 프리팹이 비면 프리미티브로 폴백해 즉시 검증 가능.
/// 결정성: 동일 시드 → 동일 맵 (전용 System.Random 사용, 전역 UnityEngine.Random 오염 안 함).
/// </summary>
[DisallowMultipleComponent]
public class MapGenerator : MonoBehaviour
{
    public struct MapBounds
    {
        public Vector3 center;
        public Vector3 size;       // x = 월드 폭, z = 월드 길이
        public float halfExtent;   // max(폭,길이)/2 — 스폰/카메라 클램프용
    }

    [SerializeField] private MapConfig config;
    [Tooltip("0 이상이면 config.defaultSeed 대신 이 값을 시드로 사용")]
    [SerializeField] private int seedOverride = -1;
    [SerializeField] private NavMeshSurface navMeshSurface;
    [SerializeField] private bool generateOnStart = true;

    /// <summary>생성 완료 시 맵 경계 통지 (스포너/카메라 클램프 등이 구독).</summary>
    public event Action<MapBounds> onGenerated;

    public int LastSeed { get; private set; }
    public MapBounds LastBounds { get; private set; }

    private Transform mapRoot;
    private const string MapRootName = "MapRoot";

    private void Start()
    {
        if (generateOnStart)
            Generate();
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        int seed = seedOverride >= 0
            ? seedOverride
            : (config != null ? config.defaultSeed : 12345);
        Generate(seed);
    }

    public void Generate(int seed)
    {
        LastSeed = seed;
        var rng = new System.Random(seed);

        float cell   = config != null ? config.cellSize        : 4f;
        int   gw     = config != null ? config.gridWidth       : 16;
        int   gh     = config != null ? config.gridHeight      : 16;
        float wallH  = config != null ? config.wallHeight      : 3f;
        float dens   = config != null ? config.obstacleDensity : 0.12f;
        int   obsCnt = config != null ? config.obstacleCount   : 0;
        int   covers = config != null ? config.coverCount      : 12;
        float clearR = config != null ? config.centerClearRadius : 6f;

        float worldW = gw * cell;
        float worldL = gh * cell;
        Vector3 origin = new Vector3(-worldW * 0.5f, 0f, -worldL * 0.5f);

        PrepareRoot();
        BuildFloor(worldW, worldL);
        BuildPerimeterWalls(worldW, worldL, cell, wallH);
        ScatterObstacles(gw, gh, cell, wallH, origin, dens, obsCnt, clearR, rng);
        PlaceCover(gw, gh, cell, origin, covers, clearR, rng);
        PlaceBarrels(gw, gh, cell, origin, config != null ? config.barrelCount : 6, clearR, rng);
        BakeNavMesh();

        LastBounds = new MapBounds
        {
            center = transform.position,
            size = new Vector3(worldW, wallH, worldL),
            halfExtent = Mathf.Max(worldW, worldL) * 0.5f
        };
        onGenerated?.Invoke(LastBounds);
    }

    // ---------- build steps ----------

    private void PrepareRoot()
    {
        var existing = transform.Find(MapRootName);
        if (existing != null) DestroySafe(existing.gameObject);

        mapRoot = new GameObject(MapRootName).transform;
        mapRoot.SetParent(transform, false);
        mapRoot.localPosition = Vector3.zero;
    }

    private void BuildFloor(float worldW, float worldL)
    {
        if (config != null && config.floorPrefab != null)
        {
            var floor = Instantiate(config.floorPrefab, mapRoot);
            floor.transform.localPosition = Vector3.zero;
            floor.name = "Floor";
            return;
        }

        var prim = GameObject.CreatePrimitive(PrimitiveType.Cube);
        prim.name = "Floor";
        prim.transform.SetParent(mapRoot, false);
        prim.transform.localPosition = new Vector3(0f, -0.1f, 0f);
        prim.transform.localScale = new Vector3(worldW, 0.2f, worldL);

        if (config != null && config.floorMaterial != null)
        {
            var rend = prim.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = config.floorMaterial;
                // 큰 맵에서 텍스처가 늘어나지 않게 타일링(머티리얼 인스턴스에만 — 공유 에셋 안 건드림).
                float tile = config.floorTileWorldUnits;
                if (tile > 0f)
                    rend.material.mainTextureScale = new Vector2(worldW / tile, worldL / tile);
            }
        }
    }

    private void BuildPerimeterWalls(float worldW, float worldL, float cell, float wallH)
    {
        float t = Mathf.Max(0.5f, cell * 0.25f); // 벽 두께
        float y = wallH * 0.5f;

        SpawnWall(new Vector3(0f, y, -worldL * 0.5f), new Vector3(worldW + t, wallH, t), "Wall_S");
        SpawnWall(new Vector3(0f, y,  worldL * 0.5f), new Vector3(worldW + t, wallH, t), "Wall_N");
        SpawnWall(new Vector3(-worldW * 0.5f, y, 0f), new Vector3(t, wallH, worldL + t), "Wall_W");
        SpawnWall(new Vector3( worldW * 0.5f, y, 0f), new Vector3(t, wallH, worldL + t), "Wall_E");
    }

    private void SpawnWall(Vector3 localPos, Vector3 size, string label)
    {
        GameObject go;
        if (config != null && config.wallPrefab != null)
        {
            go = Instantiate(config.wallPrefab, mapRoot);
            go.transform.localScale = size;
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(mapRoot, false);
            go.transform.localScale = size;
        }
        go.name = label;
        go.transform.localPosition = localPos;
    }

    private void ScatterObstacles(int gw, int gh, float cell, float wallH, Vector3 origin,
                                  float density, int count, float clearR, System.Random rng)
    {
        // count>0: 카운트 기반(셀 1개당 최대 1개, 상한 — 큰 맵 성능). 0: 셀별 확률(레거시 소형 맵).
        if (count > 0)
        {
            var used = new System.Collections.Generic.HashSet<long>();
            int placed = 0, attempts = 0, maxAttempts = count * 20;
            while (placed < count && attempts++ < maxAttempts)
            {
                int x = 1 + rng.Next(Mathf.Max(1, gw - 2));
                int z = 1 + rng.Next(Mathf.Max(1, gh - 2));
                long key = ((long)x << 32) ^ (uint)z;
                if (!used.Add(key)) continue; // 같은 셀 중복 방지

                Vector3 pos = CellCenter(x, z, cell, origin);
                if (pos.x * pos.x + pos.z * pos.z < clearR * clearR) continue; // 중앙 스폰존 비움

                SpawnObstacle(pos, cell, wallH, rng);
                placed++;
            }
            return;
        }

        density = Mathf.Clamp01(density);
        for (int x = 1; x < gw - 1; x++)
        {
            for (int z = 1; z < gh - 1; z++)
            {
                if (rng.NextDouble() > density) continue;

                Vector3 pos = CellCenter(x, z, cell, origin);
                if (pos.x * pos.x + pos.z * pos.z < clearR * clearR) continue; // 중앙 스폰존 비움

                SpawnObstacle(pos, cell, wallH, rng);
            }
        }
    }

    private void SpawnObstacle(Vector3 localPos, float cell, float wallH, System.Random rng)
    {
        bool usingPrefab = config != null && config.obstaclePrefabs != null && config.obstaclePrefabs.Count > 0;
        GameObject go;
        if (usingPrefab)
        {
            var prefab = config.obstaclePrefabs[rng.Next(config.obstaclePrefabs.Count)];
            go = Instantiate(prefab, mapRoot);
            go.transform.localRotation = Quaternion.Euler(0f, rng.Next(4) * 90f, 0f);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(mapRoot, false);
            float s = cell * (float)(0.5 + rng.NextDouble() * 0.4);
            go.transform.localScale = new Vector3(s, wallH, s);
        }
        go.name = "Obstacle";
        Vector3 p = localPos;
        // 임포트 프리팹은 베이스 피벗 → 바닥(y=0). 프리미티브는 절반 높이만큼 올림.
        p.y = usingPrefab ? 0f : go.transform.localScale.y * 0.5f;
        go.transform.localPosition = p;

        // 정적 장애물은 convex 불필요 → 복잡 메시(>256 poly) convex 변환 경고 방지 + 전체 메시 충돌
        if (usingPrefab)
            foreach (var mc in go.GetComponentsInChildren<MeshCollider>())
                mc.convex = false;

        var mo = go.AddComponent<MapObject>();
        mo.role = TDS.Core.MapObjectRole.Blocking;
    }

    private void PlaceBarrels(int gw, int gh, float cell, Vector3 origin, int count, float clearR, System.Random rng)
    {
        int placed = 0, attempts = 0, maxAttempts = Mathf.Max(1, count) * 20;
        while (placed < count && attempts++ < maxAttempts)
        {
            int x = 1 + rng.Next(Mathf.Max(1, gw - 2));
            int z = 1 + rng.Next(Mathf.Max(1, gh - 2));
            Vector3 pos = CellCenter(x, z, cell, origin);
            if (pos.x * pos.x + pos.z * pos.z < clearR * clearR) continue;

            GameObject go;
            float h = 0.9f;
            var prefab = config != null ? config.barrelPrefab : null;
            if (prefab != null)
            {
                go = Instantiate(prefab, mapRoot);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.transform.SetParent(mapRoot, false);
                go.transform.localScale = new Vector3(0.8f, h * 0.5f, 0.8f); // 실린더 높이 = scale.y*2
            }
            go.name = "Barrel";
            Vector3 p = pos;
            p.y = prefab != null ? 0f : h * 0.5f;
            go.transform.localPosition = p;

            go.AddComponent<Movable>();
            go.AddComponent<Breakable>();
            var mo = go.AddComponent<MapObject>();
            mo.role = TDS.Core.MapObjectClassifier.Classify(h, isCover: false, breakable: true, movable: true);
            placed++;
        }
    }

    private void PlaceCover(int gw, int gh, float cell, Vector3 origin,
                            int count, float clearR, System.Random rng)
    {
        int placed = 0, attempts = 0, maxAttempts = Mathf.Max(1, count) * 20;
        while (placed < count && attempts++ < maxAttempts)
        {
            int x = 1 + rng.Next(Mathf.Max(1, gw - 2));
            int z = 1 + rng.Next(Mathf.Max(1, gh - 2));
            Vector3 pos = CellCenter(x, z, cell, origin);
            if (pos.x * pos.x + pos.z * pos.z < clearR * clearR) continue;

            // 낮은 단상(사격 가능) vs 높은 cover(은폐 전용)를 섞어 배치.
            float lowRatio = config != null ? config.lowCoverRatio : 0.6f;
            bool makeLow = rng.NextDouble() < lowRatio;

            GameObject go;
            float pivotY;
            if (makeLow)
            {
                GameObject lowPrefab = config != null ? config.lowCoverPrefab : null;
                if (lowPrefab != null) { go = Instantiate(lowPrefab, mapRoot); pivotY = 0f; }
                else
                {
                    float h = Mathf.Min(0.8f, config != null ? config.lowCoverHeight : 0.7f);
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.transform.SetParent(mapRoot, false);
                    go.transform.localScale = new Vector3(1.6f, h, 1.6f); // 작은 풋프린트(엄폐점 ±1.5가 밖) + 낮은 높이
                    pivotY = h * 0.5f;
                }
            }
            else
            {
                bool usingPrefab = config != null && config.coverPrefab != null;
                if (usingPrefab) { go = Instantiate(config.coverPrefab, mapRoot); pivotY = 0f; }
                else
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.transform.SetParent(mapRoot, false);
                    go.transform.localScale = new Vector3(2f, 2f, 2f);
                    pivotY = 1f;
                }
            }

            go.name = "Cover";
            Vector3 p = pos;
            p.y = pivotY;
            go.transform.localPosition = p;
            go.transform.localRotation = Quaternion.Euler(0f, rng.Next(4) * 90f, 0f);

            // 엄폐 기능 배선: Cover 컴포넌트 보장(프리팹에 이미 있으면 유지) + 엄폐 지점 마커(Start 전 Configure)
            if (go.GetComponent<Cover>() == null)
            {
                var cover = go.AddComponent<Cover>();
                Vector2 off = config != null ? config.coverPointOffset : new Vector2(1.5f, 1.5f);
                cover.Configure(config != null ? config.coverPointPrefab : null, off.x, off.y);
            }
            placed++;
        }
    }

    private void BakeNavMesh()
    {
        if (navMeshSurface == null)
        {
            Debug.LogWarning("[MapGenerator] navMeshSurface 미할당 — NavMesh 베이크 생략.");
            return;
        }
        navMeshSurface.BuildNavMesh();
    }

    // ---------- helpers ----------

    private Vector3 CellCenter(int x, int z, float cell, Vector3 origin)
        => origin + new Vector3((x + 0.5f) * cell, 0f, (z + 0.5f) * cell);

    private void DestroySafe(GameObject go)
    {
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }
}
