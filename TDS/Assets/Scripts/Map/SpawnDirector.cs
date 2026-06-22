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

    [SerializeField] private List<WaveDef> waves = new List<WaveDef>();
    [SerializeField] private float minRadius = 8f;
    [SerializeField] private float maxRadius = 20f;
    [Header("분대 (한 곳에 뭉쳐 스폰 + 공유 인지)")]
    [Tooltip("분대 인원 범위(min,max). 웨이브 count를 이 크기 분대들로 나눠 한 곳씩 뭉쳐 스폰")]
    [SerializeField] private Vector2Int squadSize = new Vector2Int(5, 9);
    [Tooltip("분대원이 분대 중심 주변에 뭉치는 반경")]
    [SerializeField] private float squadClusterRadius = 5f;
    [SerializeField] private int seed = 1;
    [SerializeField] private bool requirePlayer = true;
    [SerializeField] private bool autoStart = true;

    private WaveSequencer sequencer;
    private readonly List<Enemy> currentWave = new List<Enemy>();
    private System.Random rng;
    private float waveStartTime;
    private bool running;

    public int CurrentWaveNumber => sequencer != null ? sequencer.CurrentWave + 1 : 0;
    public int TotalWaves => waves != null ? waves.Count : 0;
    public bool Finished => sequencer != null && sequencer.Finished;
    public int AliveCount => CountAlive();

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
        if (!running || sequencer == null || sequencer.Finished)
            return;

        // 적 AI가 플레이어를 참조하므로 플레이어 존재 후 진행
        if (requirePlayer && GameObject.FindWithTag("Player") == null)
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

            var squadGo = new GameObject("Squad");
            squadGo.transform.position = center;
            var squad = squadGo.AddComponent<Squad>();

            for (int i = 0; i < size; i++)
            {
                var def = wave.table.Pick((float)rng.NextDouble());
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
                {
                    squad.Register(enemy);
                    currentWave.Add(enemy);
                }
            }
        }
    }
}
