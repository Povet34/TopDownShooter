using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using TDS.Core;

/// <summary>
/// 다중 웨이브 디렉터(TDS.Game). 순수 진행 로직은 <see cref="WaveSequencer"/>가 담당하고,
/// 여기선 적 스폰 + 생존 수 추적(유니티 의존)만 한다.
/// 각 웨이브는 SpawnTable에서 가중 선택해 자기 주변 링(navmesh)에 N마리 스폰.
/// 웨이브 전멸 또는 최대 시간 초과 시 다음 웨이브로. 시드로 결정적.
/// </summary>
[DisallowMultipleComponent]
public class SpawnDirector : MonoBehaviour
{
    [System.Serializable]
    public class WaveDef
    {
        public SpawnTable table;
        [Min(1)] public int count = 5;
        [Tooltip("이 시간(초)이 지나면 적이 남아있어도 다음 웨이브로 진행. 0 = 비활성(전멸까지 대기)")]
        public float maxWaveTime = 0f;
    }

    public enum SpawnMode { Waves, Roaming }

    [Tooltip("Waves=클리어/타임아웃 웨이브 진행. Roaming=상시 로밍 분대(가장자리 스폰→플레이어로 순찰→가장자리 디스폰→리스폰, §6.3.2)")]
    [SerializeField] private SpawnMode mode = SpawnMode.Waves;

    [SerializeField] private List<WaveDef> waves = new List<WaveDef>();
    [SerializeField] private float minRadius = 8f;
    [SerializeField] private float maxRadius = 20f;
    [Header("분대 (한 곳에 뭉쳐 스폰 + 공유 인지)")]
    [Tooltip("분대 인원 범위(min,max). 웨이브 count를 이 크기 분대들로 나눠 한 곳씩 뭉쳐 스폰")]
    [SerializeField] private Vector2Int squadSize = new Vector2Int(5, 9);
    [Tooltip("분대원이 분대 중심 주변에 뭉치는 반경")]
    [SerializeField] private float squadClusterRadius = 5f;

    [Header("상시 로밍 (mode=Roaming, §6.3.2)")]
    [Tooltip("로밍 분대가 뽑는 스폰 테이블")]
    [SerializeField] private SpawnTable roamTable;
    [Tooltip("맵에 동시에 유지할 로밍 분대 수")]
    [SerializeField] private int maxSquads = 3;
    [Tooltip("로밍 분대 1개 새로 스폰하는 최소 간격(초)")]
    [SerializeField] private float roamSpawnInterval = 3f;
    [Tooltip("가장자리 스폰을 navmesh 안으로 들이는 여유(경계벽 안쪽)")]
    [SerializeField] private float edgeInset = 6f;
    [Tooltip("순찰 분대가 이 여유 안으로 가장자리에 닿으면 디스폰")]
    [SerializeField] private float despawnMargin = 4f;
    [Tooltip("로밍 멤버의 순찰 대기시간(초). 짧아야 앵커를 따라 계속 이동(프리팹 기본 idleTime은 보통 큼)")]
    [SerializeField] private float roamIdleTime = 1f;
    [Tooltip("맵 bounds 제공(미할당 시 씬에서 자동 탐색). 로밍 스폰/디스폰 영역")]
    [SerializeField] private MapGenerator mapGenerator;

    [SerializeField] private int seed = 1;
    [SerializeField] private bool requirePlayer = true;
    [SerializeField] private bool autoStart = true;

    private WaveSequencer sequencer;
    private readonly List<Enemy> currentWave = new List<Enemy>();
    private readonly List<Squad> roamSquads = new List<Squad>();
    private System.Random rng;
    private float waveStartTime;
    private float lastRoamSpawnTime = -999f;
    private bool running;
    private MapGenerator.MapBounds bounds;
    private bool boundsKnown;

    public int CurrentWaveNumber => sequencer != null ? sequencer.CurrentWave + 1 : 0;
    public int TotalWaves => waves != null ? waves.Count : 0;
    public bool Finished => mode == SpawnMode.Waves && sequencer != null && sequencer.Finished;
    public bool IsRoaming => mode == SpawnMode.Roaming;
    public int AliveCount => mode == SpawnMode.Roaming ? CountRoamingAlive() : CountAlive();

    private void Awake()
    {
        rng = new System.Random(seed);
        sequencer = new WaveSequencer(waves != null ? waves.Count : 0);
        running = autoStart;
    }

    /// <summary>autoStart=false일 때 외부에서 진행 시작.</summary>
    public void StartDirector() => running = true;

    private void Update()
    {
        if (!running)
            return;

        // 적 AI가 플레이어를 참조하므로 플레이어 존재 후 진행
        if (requirePlayer && GameObject.FindWithTag("Player") == null)
            return;

        if (mode == SpawnMode.Roaming)
        {
            UpdateRoaming();
            return;
        }

        if (sequencer == null || sequencer.Finished)
            return;

        int alive = CountAlive();
        int idx = sequencer.CurrentWave;
        float maxT = (idx >= 0 && idx < waves.Count) ? waves[idx].maxWaveTime : 0f;
        float since = Time.time - waveStartTime;

        switch (sequencer.Decide(alive, since, maxT))
        {
            case WaveAction.SpawnNext:
                sequencer.MarkSpawned();
                SpawnWave(waves[sequencer.CurrentWave]);
                waveStartTime = Time.time;
                break;

            case WaveAction.Done:
                sequencer.MarkFinished();
                Debug.Log($"[SpawnDirector] 모든 웨이브({waves.Count}) 클리어.");
                break;
        }
    }

    private int CountAlive()
    {
        int n = 0;
        foreach (var e in currentWave)
            if (e != null && e.health != null && e.health.currentHealth > 0)
                n++;
        return n;
    }

    private int CountRoamingAlive()
    {
        int n = 0;
        foreach (var s in roamSquads)
        {
            if (s == null) continue;
            foreach (var e in s.Members)
                if (e != null && e.health != null && e.health.currentHealth > 0)
                    n++;
        }
        return n;
    }

    private void SpawnWave(WaveDef wave)
    {
        currentWave.Clear();
        if (wave == null || wave.table == null)
        {
            Debug.LogWarning("[SpawnDirector] 웨이브 또는 SpawnTable 미할당 — 건너뜀.");
            return;
        }

        // 웨이브 인원을 분대들로 나눠, 각 분대를 한 곳에 뭉쳐 스폰(흩뿌리지 않음). 분대원은 인지를 공유.
        int remaining = wave.count;
        while (remaining > 0)
        {
            // 남은 인원이 한 분대에 다 들어가면 통째로(자투리 1인 분대 방지), 아니면 랜덤 분대 크기.
            int size = remaining <= squadSize.y
                ? remaining
                : Mathf.Clamp(rng.Next(squadSize.x, squadSize.y + 1), 1, remaining);
            remaining -= size;

            // 분대 중심(링 위 한 점)
            double cAng = rng.NextDouble() * System.Math.PI * 2.0;
            float cDist = Mathf.Lerp(minRadius, maxRadius, (float)rng.NextDouble());
            Vector3 center = transform.position + new Vector3(Mathf.Cos((float)cAng) * cDist, 0f, Mathf.Sin((float)cAng) * cDist);

            var squad = SpawnSquadAt(center, size, wave.table);
            if (squad != null)
                foreach (var e in squad.Members)
                    currentWave.Add(e);
        }
    }

    /// <summary>중심 주변에 황금각 나선으로 분대 1개(size명) 스폰 후 Squad 반환. 웨이브·로밍 공용.</summary>
    private Squad SpawnSquadAt(Vector3 center, int size, SpawnTable table)
    {
        var squadGo = new GameObject("Squad");
        squadGo.transform.position = center;
        var squad = squadGo.AddComponent<Squad>();

        for (int i = 0; i < size; i++)
        {
            var def = table.Pick((float)rng.NextDouble());
            if (def == null || def.prefab == null)
                continue;

            // 분대 중심 주변에 황금각 나선으로 균등 분산(겹쳐 쌓이지 않게) — 순찰 대형과 같은 수식 공유.
            Vector3 pos = TDS.Core.SquadFormation.SpiralPoint(center, i, size, squadClusterRadius);

            if (NavMesh.SamplePosition(pos, out var hit, 3f, NavMesh.AllAreas))
                pos = hit.position; // 작은 샘플 반경 — 멀리 있는 한 점으로 뭉치지 않게

            // 바깥(중심에서 멀어지는 방향)을 보게 → 분대가 사방을 경계(어느 각도든 플레이어 포착)
            Vector3 facing = pos - center; facing.y = 0f;
            Quaternion rot = facing.sqrMagnitude > 0.01f ? Quaternion.LookRotation(facing) : Quaternion.identity;

            var go = Instantiate(def.prefab, pos, rot);
            var enemy = go.GetComponentInChildren<Enemy>();
            if (enemy != null)
                squad.Register(enemy);
        }
        return squad;
    }

    // --- 상시 로밍 (mode=Roaming, §6.3.2) ---

    private void UpdateRoaming()
    {
        if (!ResolveBounds() || roamTable == null)
            return;

        roamSquads.RemoveAll(s => s == null); // 디스폰/전멸한 분대 정리

        int toSpawn = TDS.Core.SquadRoam.SquadsToSpawn(roamSquads.Count, maxSquads);
        if (toSpawn <= 0 || Time.time - lastRoamSpawnTime < roamSpawnInterval)
            return;

        SpawnRoamingSquad(); // 한 번에 하나씩(간격 두고) 채움
        lastRoamSpawnTime = Time.time;
    }

    private void SpawnRoamingSquad()
    {
        // 맵 가장자리(경계벽 안쪽) 둘레의 한 점에서 스폰
        float t = (float)rng.NextDouble();
        Vector3 edge = TDS.Core.SquadRoam.EdgeSpawnPoint(bounds.center, Mathf.Max(0f, bounds.halfExtent - edgeInset), t);
        if (!NavMesh.SamplePosition(edge, out var hit, edgeInset + 5f, NavMesh.AllAreas))
            return; // 가장자리 근처에 navmesh 없으면 이번 틱 건너뜀
        Vector3 center = hit.position;

        int size = Mathf.Clamp(rng.Next(squadSize.x, squadSize.y + 1), 1, squadSize.y);
        var squad = SpawnSquadAt(center, size, roamTable);
        if (squad == null || squad.Members.Count == 0)
        {
            if (squad != null) Destroy(squad.gameObject);
            return;
        }
        // 로밍 멤버는 자주 재이동해야 앵커를 따라간다(프리팹 기본 idleTime이 크면 거의 안 움직임).
        foreach (var e in squad.Members)
            if (e != null)
                e.idleTime = roamIdleTime;

        squad.ConfigureRoaming(bounds.center, bounds.halfExtent, despawnMargin);
        roamSquads.Add(squad);
    }

    private bool ResolveBounds()
    {
        if (boundsKnown)
            return true;
        if (mapGenerator == null)
            mapGenerator = FindObjectOfType<MapGenerator>();
        if (mapGenerator == null || mapGenerator.LastBounds.halfExtent <= 0f)
            return false; // 맵 아직 생성 전
        bounds = mapGenerator.LastBounds;
        boundsKnown = true;
        return true;
    }
}
