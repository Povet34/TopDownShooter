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

    /// <summary>탈출 수송선 위치(레이더가 가리킴). 없으면 false.</summary>
    public bool HasExtraction { get; private set; }
    public Vector3 ExtractionPosition { get; private set; }

    private Transform mapRoot;
    private const string MapRootName = "MapRoot";

    // 주변만 렌더링: 컬링 대상(바닥/벽 제외) + 스로틀. navmesh는 베이크돼 있어 비활성해도 경로엔 영향 없음.
    private readonly System.Collections.Generic.List<Transform> cullables = new System.Collections.Generic.List<Transform>();
    private Transform cullPlayer;
    private float cullTimer;
    private const float CullInterval = 0.4f;

    private void Start()
    {
        if (generateOnStart)
            Generate();
    }

    private void Update()
    {
        float r = config != null ? config.cullRadius : 0f;
        if (r <= 0f || cullables.Count == 0)
            return;

        cullTimer -= Time.deltaTime;
        if (cullTimer > 0f)
            return;
        cullTimer = CullInterval;

        if (cullPlayer == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p == null) return;
            cullPlayer = p.transform;
        }

        Vector3 pp = cullPlayer.position;
        float r2 = r * r;
        for (int i = 0; i < cullables.Count; i++)
        {
            var t = cullables[i];
            if (t == null) continue;
            Vector3 d = t.position - pp; d.y = 0f;
            bool near = d.sqrMagnitude <= r2;
            if (t.gameObject.activeSelf != near)
                t.gameObject.SetActive(near);
        }
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
        PlaceClusters(worldW, worldL, cell, wallH, clearR, rng);
        PlaceInteriorWalls(worldW, worldL, wallH, clearR, rng);
        PlaceCliffs(worldW, worldL, clearR, rng);
        PlaceRockProps(worldW, worldL, clearR, rng);
        PlaceDropship(worldW, worldL);
        PlaceCover(gw, gh, cell, origin, covers, clearR, rng);
        PlaceBarrels(gw, gh, cell, origin, config != null ? config.barrelCount : 6, clearR, rng);
        BakeNavMesh();
        BuildCullList();

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

        cullables.Clear();
        cullPlayer = null;

        mapRoot = new GameObject(MapRootName).transform;
        mapRoot.SetParent(transform, false);
        mapRoot.localPosition = Vector3.zero;
    }

    // 거리 컬링 대상 수집(바닥은 제외 — 항상 보여야 함). navmesh는 이미 베이크돼 비활성해도 경로 영향 없음.
    private void BuildCullList()
    {
        cullables.Clear();
        if (mapRoot == null) return;
        foreach (Transform child in mapRoot)
            if (child.name != "Floor" && child.name != "Dropship") // 바닥/수송선은 항상 보임(랜드마크)
                cullables.Add(child);
    }

    // 수송선 탈출 존을 맵 고정 위치에 배치(콜라이더 없는 시각 랜드마크 + ExtractionZone 근접 감지).
    private void PlaceDropship(float worldW, float worldL)
    {
        HasExtraction = false;
        if (config == null || !config.spawnExtraction) return;

        float hx = worldW * 0.5f, hz = worldL * 0.5f;
        Vector3 pos = new Vector3(
            Mathf.Clamp(config.extractionOffset.x, -1f, 1f) * hx,
            0f,
            Mathf.Clamp(config.extractionOffset.y, -1f, 1f) * hz);
        pos.x = Mathf.Clamp(pos.x, -hx + 12f, hx - 12f); // 경계 안쪽
        pos.z = Mathf.Clamp(pos.z, -hz + 12f, hz - 12f);

        GameObject go;
        if (config.dropshipPrefab != null)
        {
            go = Instantiate(config.dropshipPrefab, mapRoot);
            go.transform.localPosition = pos;
        }
        else
        {
            go = BuildPrimitiveDropship(pos, config.extractionRadius);
        }
        go.name = "Dropship";

        var zone = go.GetComponent<ExtractionZone>() ?? go.AddComponent<ExtractionZone>();
        zone.Configure(config.extractionRadius, config.extractionBoardTime);

        HasExtraction = true;
        ExtractionPosition = go.transform.position;
    }

    // 프리미티브 수송선: 시안 착륙 패드(평평) + 중앙 비콘 기둥(멀리서 보이게). 콜라이더 제거(시각·항법만).
    private GameObject BuildPrimitiveDropship(Vector3 localPos, float radius)
    {
        var root = new GameObject("Dropship");
        root.transform.SetParent(mapRoot, false);
        root.transform.localPosition = localPos;
        var padMat = MakeEmissive(new Color(0.2f, 0.85f, 0.95f));

        var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pad.name = "Pad";
        pad.transform.SetParent(root.transform, false);
        pad.transform.localScale = new Vector3(radius * 2f, 0.1f, radius * 2f);
        pad.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        StripCollider(pad);
        SetRendererMaterial(pad, padMat);

        var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beacon.name = "Beacon";
        beacon.transform.SetParent(root.transform, false);
        beacon.transform.localScale = new Vector3(0.6f, 5f, 0.6f); // 높이 10m
        beacon.transform.localPosition = new Vector3(0f, 5f, 0f);
        StripCollider(beacon);
        SetRendererMaterial(beacon, padMat);

        return root;
    }

    private Material MakeEmissive(Color c)
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        var m = new Material(sh != null ? sh : Shader.Find("Standard"));
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        m.EnableKeyword("_EMISSION");
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * 1.6f);
        return m;
    }

    private static void StripCollider(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
    }

    private static void SetRendererMaterial(GameObject go, Material m)
    {
        var r = go.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = m;
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
                // base뿐 아니라 normal/AO도 같은 스케일로(안 그러면 노멀이 늘어나 평평해 보임).
                float tile = config.floorTileWorldUnits;
                if (tile > 0f)
                {
                    Vector2 s = new Vector2(worldW / tile, worldL / tile);
                    var mat = rend.material;
                    // _BaseMap을 명시적으로 스케일(URP Lit/커스텀 셰이더 둘 다 동작, _MainTex 경고 회피).
                    if (mat.HasProperty("_BaseMap")) mat.SetTextureScale("_BaseMap", s);
                    else mat.mainTextureScale = s;
                    if (mat.HasProperty("_BumpMap")) mat.SetTextureScale("_BumpMap", s);
                    if (mat.HasProperty("_SecondMap")) mat.SetTextureScale("_SecondMap", s);
                    if (mat.HasProperty("_SecondBump")) mat.SetTextureScale("_SecondBump", s);
                    if (mat.HasProperty("_OcclusionMap")) mat.SetTextureScale("_OcclusionMap", s);
                }
            }
        }
    }

    private void BuildPerimeterWalls(float worldW, float worldL, float cell, float wallH)
    {
        // boundaryAsCliff면 경계를 절벽 높이/머티리얼로 — '절벽으로 갇힌 맵' 느낌.
        bool asCliff = config != null && config.boundaryAsCliff;
        float h = asCliff ? config.cliffHeight : wallH;
        Material mat = asCliff ? config.cliffMaterial : null;
        float t = Mathf.Max(0.5f, cell * 0.25f); // 벽 두께
        if (asCliff) t = Mathf.Max(t, 3f);        // 절벽은 두껍게
        float y = h * 0.5f;

        SpawnWall(new Vector3(0f, y, -worldL * 0.5f), new Vector3(worldW + t, h, t), "Wall_S", mat);
        SpawnWall(new Vector3(0f, y,  worldL * 0.5f), new Vector3(worldW + t, h, t), "Wall_N", mat);
        SpawnWall(new Vector3(-worldW * 0.5f, y, 0f), new Vector3(t, h, worldL + t), "Wall_W", mat);
        SpawnWall(new Vector3( worldW * 0.5f, y, 0f), new Vector3(t, h, worldL + t), "Wall_E", mat);
    }

    private void SpawnWall(Vector3 localPos, Vector3 size, string label, Material mat = null)
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
        if (mat != null) ApplyRockMaterial(go, size, mat);
    }

    // 절벽/바위 머티리얼을 입히고 풋프린트에 맞춰 타일링(2K 텍스처가 늘어나지 않게).
    private void ApplyRockMaterial(GameObject go, Vector3 size, Material mat)
    {
        var rend = go.GetComponentInChildren<Renderer>();
        if (rend == null) return;
        rend.sharedMaterial = mat;
        var inst = rend.material; // 인스턴스(타일링만 변경, 공유 에셋 안 건드림)
        Vector2 s = new Vector2(Mathf.Max(1f, size.x / 4f), Mathf.Max(1f, size.y / 4f));
        if (inst.HasProperty("_BaseMap")) inst.SetTextureScale("_BaseMap", s);
        else inst.mainTextureScale = s;
        if (inst.HasProperty("_BumpMap")) inst.SetTextureScale("_BumpMap", s);
        if (inst.HasProperty("_OcclusionMap")) inst.SetTextureScale("_OcclusionMap", s);
    }

    // 내부 절벽(메사): 못 올라가는 임패서블 바위 덩어리. 가팔라 navmesh가 자동 제외 → 엔티티 우회.
    private void PlaceCliffs(float worldW, float worldL, float clearR, System.Random rng)
    {
        int count = config != null ? config.interiorCliffCount : 0;
        if (count <= 0) return;
        float h    = config != null ? config.cliffHeight : 10f;
        float minF = config != null ? config.cliffMinFootprint : 5f;
        float maxF = config != null ? Mathf.Max(config.cliffMinFootprint, config.cliffMaxFootprint) : 14f;
        Material mat = config != null ? config.cliffMaterial : null;
        float halfW = Mathf.Max(0f, worldW * 0.5f - maxF);
        float halfL = Mathf.Max(0f, worldL * 0.5f - maxF);

        for (int i = 0; i < count; i++)
        {
            Vector3 center = Vector3.zero;
            bool ok = false;
            float keepOut = clearR + maxF;
            for (int guard = 0; guard < 20; guard++)
            {
                center = new Vector3((float)(rng.NextDouble() * 2 - 1) * halfW, 0f, (float)(rng.NextDouble() * 2 - 1) * halfL);
                if (center.x * center.x + center.z * center.z >= keepOut * keepOut) { ok = true; break; }
            }
            if (!ok) continue;

            // 메사 1개 = 1~3개 지터된 블록(덜 박스답게)
            int blocks = 1 + rng.Next(3);
            float baseF = minF + (float)rng.NextDouble() * (maxF - minF);
            for (int b = 0; b < blocks; b++)
            {
                Vector2 o = b == 0 ? Vector2.zero : RandomInCircle(rng) * (baseF * 0.4f);
                float fx = baseF * (0.6f + (float)rng.NextDouble() * 0.5f);
                float fz = baseF * (0.6f + (float)rng.NextDouble() * 0.5f);
                float bh = h * (0.7f + (float)rng.NextDouble() * 0.4f);
                SpawnCliffBlock(center + new Vector3(o.x, 0f, o.y), new Vector3(fx, bh, fz), mat, rng);
            }
        }
    }

    private void SpawnCliffBlock(Vector3 localXZ, Vector3 size, Material mat, System.Random rng)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.SetParent(mapRoot, false);
        go.name = "Cliff";
        go.transform.localScale = size;
        Vector3 p = localXZ; p.y = size.y * 0.5f;
        go.transform.localPosition = p;
        go.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
        if (mat != null) ApplyRockMaterial(go, size, mat);

        var mo = go.AddComponent<MapObject>();
        mo.role = TDS.Core.MapObjectRole.Blocking;

        // 절벽 위를 작은 바위로 덮음(시각). 탑다운이라 위가 보임 → 단조로운 평면 윗면 가림.
        bool cap = config != null && config.capCliffsWithRocks && config.rockMaterial != null;
        if (cap)
        {
            int rocks = 2 + rng.Next(4);
            float topY = size.y;
            float fp = Mathf.Min(size.x, size.z) * 0.4f;
            for (int i = 0; i < rocks; i++)
            {
                Vector2 o = RandomInCircle(rng) * fp;
                float rs = Mathf.Max(size.x, size.z) * (0.18f + (float)rng.NextDouble() * 0.18f);
                SpawnRock(new Vector3(localXZ.x + o.x, topY, localXZ.z + o.y), rs, config.rockMaterial, rng, "CliffRock", true);
            }
        }
    }

    // 바닥에 작은 바위 산재(순수 시각 디테일 — 콜라이더/네브메시 영향 없음).
    private void PlaceRockProps(float worldW, float worldL, float clearR, System.Random rng)
    {
        int count = config != null ? config.rockPropCount : 0;
        Material mat = config != null ? config.rockMaterial : null;
        if (count <= 0 || mat == null) return;
        float size = config != null ? config.rockPropSize : 1.4f;
        float halfW = Mathf.Max(0f, worldW * 0.5f - size);
        float halfL = Mathf.Max(0f, worldL * 0.5f - size);

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = new Vector3((float)(rng.NextDouble() * 2 - 1) * halfW, 0f, (float)(rng.NextDouble() * 2 - 1) * halfL);
            if (pos.x * pos.x + pos.z * pos.z < clearR * clearR) continue;
            SpawnRock(pos, size, mat, rng, "RockProp", false);
        }
    }

    // 바위 1개 — 비균일 스케일 + 회전으로 각진 돌 느낌. 콜라이더 제거(시각 전용).
    private void SpawnRock(Vector3 surfaceLocalPos, float size, Material mat, System.Random rng, string label, bool fullTumble)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.SetParent(mapRoot, false);
        go.name = label;
        float sx = size * (0.7f + (float)rng.NextDouble() * 0.6f);
        float sy = size * (0.5f + (float)rng.NextDouble() * 0.6f);
        float sz = size * (0.7f + (float)rng.NextDouble() * 0.6f);
        go.transform.localScale = new Vector3(sx, sy, sz);
        Vector3 p = surfaceLocalPos; p.y = surfaceLocalPos.y + sy * 0.5f;
        go.transform.localPosition = p;
        go.transform.localRotation = fullTumble
            ? Quaternion.Euler((float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f)
            : Quaternion.Euler((float)(rng.NextDouble() * 16 - 8), (float)rng.NextDouble() * 360f, (float)(rng.NextDouble() * 16 - 8));
        ApplyRockMaterial(go, go.transform.localScale, mat);

        var col = go.GetComponent<Collider>();
        if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
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

    // 장애물 군집(밀집 포켓) — 균일 산포에 더해 "덜 휑한" 국소 밀도. 중앙 스폰존은 비움.
    private void PlaceClusters(float worldW, float worldL, float cell, float wallH, float clearR, System.Random rng)
    {
        int clusters = config != null ? config.clusterCount : 0;
        if (clusters <= 0) return;
        int size = config != null ? Mathf.Max(1, config.clusterSize) : 8;
        float cr = config != null ? config.clusterRadius : 6f;
        float halfW = Mathf.Max(0f, worldW * 0.5f - cell);
        float halfL = Mathf.Max(0f, worldL * 0.5f - cell);

        for (int c = 0; c < clusters; c++)
        {
            Vector3 center = Vector3.zero;
            for (int guard = 0; guard < 20; guard++)
            {
                center = new Vector3((float)(rng.NextDouble() * 2 - 1) * halfW, 0f, (float)(rng.NextDouble() * 2 - 1) * halfL);
                if (center.x * center.x + center.z * center.z >= (clearR + cr) * (clearR + cr)) break;
            }
            for (int i = 0; i < size; i++)
            {
                Vector2 o = RandomInCircle(rng) * cr;
                Vector3 pos = center + new Vector3(o.x, 0f, o.y);
                if (pos.x * pos.x + pos.z * pos.z < clearR * clearR) continue;
                SpawnObstacle(pos, cell, wallH, rng);
            }
        }
    }

    // 내부 벽 세그먼트 — 초크포인트/엄폐선으로 구조 복잡도. 중앙 스폰존은 비움.
    private void PlaceInteriorWalls(float worldW, float worldL, float wallH, float clearR, System.Random rng)
    {
        int count = config != null ? config.interiorWallCount : 0;
        if (count <= 0) return;
        float len = config != null ? config.interiorWallLength : 12f;
        const float t = 0.6f; // 두께
        float halfW = Mathf.Max(0f, worldW * 0.5f - len);
        float halfL = Mathf.Max(0f, worldL * 0.5f - len);

        for (int i = 0; i < count; i++)
        {
            Vector3 p = new Vector3((float)(rng.NextDouble() * 2 - 1) * halfW, wallH * 0.5f, (float)(rng.NextDouble() * 2 - 1) * halfL);
            if (p.x * p.x + p.z * p.z < clearR * clearR) continue;
            bool alongX = rng.Next(2) == 0;
            Vector3 size = alongX ? new Vector3(len, wallH, t) : new Vector3(t, wallH, len);
            SpawnWall(p, size, "InnerWall");
        }
    }

    private static Vector2 RandomInCircle(System.Random rng)
    {
        double a = rng.NextDouble() * System.Math.PI * 2.0;
        double r = System.Math.Sqrt(rng.NextDouble());
        return new Vector2((float)(System.Math.Cos(a) * r), (float)(System.Math.Sin(a) * r));
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
            var explosive = go.AddComponent<Explosive>(); // 부서질 때 폭발(범위 피해 + 폭발음 90m + 연쇄)
            if (config != null && config.explosionFXPrefab != null)
                explosive.ExplosionFX = config.explosionFXPrefab;
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
