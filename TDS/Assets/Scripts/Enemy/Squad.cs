using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using TDS.Core;

/// <summary>
/// 분대(Squad) — 한 곳에서 뭉쳐 스폰된 적들이 "의식을 공유"한다(§6.2 그룹 인지).
/// 멤버 중 한 명이라도 플레이어를 보거나(시야) 누군가 피격당하면 분대 전원이 교전에 들어간다.
/// 개별 적의 시야/이동 AI는 그대로 두고, 이 레이어가 "교전 공유"만 얹는다(재작성 X).
///
/// 참고: 형제 프로젝트 SpawntableGenerator의 MonsterPack(앵커+공유 PackState) 개념을 TDS의
/// 적별 perception 구조에 맞게 경량화한 것.
/// </summary>
public class Squad : MonoBehaviour
{
    private readonly List<Enemy> members = new List<Enemy>();

    [Tooltip("누군가 피격당한 뒤 이 시간(초) 동안 분대 전원 교전 유지(시야 없어도)")]
    [SerializeField] private float hitAlertDuration = 4f;

    [Header("함께 순찰(앵커-추종 로밍)")]
    [Tooltip("분대원이 앵커 주변에 유지하는 대형 반경")]
    [SerializeField] private float patrolFormationRadius = 2.5f;
    [Tooltip("앵커가 한 번에 전진하는 거리")]
    [SerializeField] private float patrolAdvance = 8f;

    private float hitAlertUntil = -999f;
    private Vector3 patrolAnchor;
    private Vector3 patrolDir;
    private bool patrolInit;

    // 상시 로밍(§6.3.2) — 디렉터가 ConfigureRoaming으로 켠다. 안 켜면 기존(웨이브) 동작 유지.
    private bool roaming;
    private Vector3 mapCenter;
    private float mapHalfExtent;
    private float despawnMargin = 4f;
    private bool hasLeftEdge; // 스폰 가장자리를 한 번 벗어나야 반대편 가장자리에서 디스폰(스폰 즉시 디스폰 방지)
    private Transform player;  // 첫 순찰 방향 계산용(1회). 이후 방향 재조정엔 안 씀.

    [Header("소음 조사(§6.2.1)")]
    [Tooltip("소리 난 곳 도착 판정 반경")]
    [SerializeField] private float investigateArriveRadius = 3.5f;
    [Tooltip("도착해서 살펴보는 시간(초). 이 동안 머물다 없으면 순찰 복귀")]
    [SerializeField] private float investigateDwell = 4f;
    [Tooltip("도달 실패 시 조사 포기까지 최대 이동 시간(초)")]
    [SerializeField] private float investigateMaxTravel = 25f;

    private bool investigating;
    private Vector3 investigatePoint;
    private float investigateArrivedAt = -1f; // 도착 시각(-1=아직 가는 중)
    private float investigateStartAt;

    public IReadOnlyList<Enemy> Members => members;
    public bool Engaged { get; private set; }
    public bool Investigating => investigating;

    /// <summary>
    /// 상시 로밍 모드 켜기(§6.3.2): 플레이어 쪽으로 순찰 전진 + 순찰 상태로 맵 가장자리 도달 시 디스폰.
    /// halfExtent ≤ 0이면 로밍 비활성(웨이브 동작).
    /// </summary>
    public void ConfigureRoaming(Vector3 center, float halfExtent, float despawnEdgeMargin)
    {
        roaming = halfExtent > 0f;
        mapCenter = center;
        mapHalfExtent = halfExtent;
        despawnMargin = despawnEdgeMargin;
    }

    public void Register(Enemy e)
    {
        if (e == null || members.Contains(e))
            return;
        members.Add(e);
        e.Squad = this;
    }

    /// <summary>멤버가 피격당함 → 분대 전원 즉시 교전(뒤돌아 있어도). 적이 GetHit에서 호출.</summary>
    public void OnMemberHit()
    {
        hitAlertUntil = Time.time + hitAlertDuration;
    }

    /// <summary>
    /// 분대원이 소음을 들음(§6.2.1) → 분대가 함께 그 지점으로 가 살펴본다(교전 중이 아니면).
    /// 개별 멤버가 따로 수색하지 않고 분대 앵커가 그쪽으로 이동 → 도착 후 dwell → 순찰 복귀.
    /// </summary>
    public void OnMemberHeardNoise(Vector3 pos)
    {
        if (Engaged)
            return;
        // 같은 소음 재통지(여러 멤버·여러 프레임)는 무시 — 도착 타이머 리셋 방지.
        if (investigating && Flat(pos - investigatePoint).sqrMagnitude < 1f)
            return;
        investigating = true;
        investigatePoint = pos;
        investigateArrivedAt = -1f;
        investigateStartAt = Time.time;
    }

    private void Update()
    {
        PruneDead();
        if (members.Count == 0)
        {
            Destroy(gameObject);
            return;
        }

        // 한 명이라도 플레이어를 보거나, 최근 누군가 맞았으면 → 분대 전원 교전.
        bool trigger = Time.time < hitAlertUntil;
        if (!trigger)
        {
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i] != null && members[i].SeesPlayer())
                {
                    trigger = true;
                    break;
                }
            }
        }

        Engaged = trigger;
        if (!trigger)
        {
            if (investigating)
            {
                UpdateInvestigate(); // 소리 난 곳으로 가 살펴봄 → 끝나면 순찰 복귀
                return;
            }
            switch (CurrentIntent())
            {
                case SquadIntent.Despawning: Despawn(); return;        // 순찰 상태로 반대편 가장자리 도달
                default:                     AdvancePatrol(); return;  // Patrolling — 함께 로밍(앵커 전진)
            }
        }

        // 교전이 조사보다 우선 — 교전 진입 시 조사 종료.
        investigating = false;

        // ForceEngage는 시야 상실 타이머를 리셋하므로, 트리거가 유지되는 한 전원 교전을 유지한다.
        // 트리거가 사라지면(아무도 안 보이고 피격도 오래됨) 각 적이 제 lose-sight 타이머로 개별 이탈.
        for (int i = 0; i < members.Count; i++)
            if (members[i] != null)
                members[i].SquadEngage();
    }

    // 소음 조사: 앵커를 소리 난 곳으로 옮겨 분대가 그쪽으로 이동 → 도착하면 dwell 동안 머물며 살펴봄 →
    // 없으면(없을 수밖에) 순찰 복귀(현재 위치에서 patrolDir 그대로). 도달 실패는 maxTravel로 포기.
    private void UpdateInvestigate()
    {
        EnsurePatrolInit();

        Vector3 c = Centroid();
        if (investigateArrivedAt < 0f)
        {
            // 앵커=소음 지점(네브메시로 스냅) → 멤버가 그 주변 대형으로 이동
            patrolAnchor = NavMesh.SamplePosition(investigatePoint, out var hit, 6f, NavMesh.AllAreas)
                ? hit.position : investigatePoint;

            if (Flat(c - patrolAnchor).magnitude <= investigateArriveRadius)
                investigateArrivedAt = Time.time;                      // 도착 → 살펴보기 시작
            else if (Time.time - investigateStartAt > investigateMaxTravel)
                EndInvestigate(c);                                     // 너무 오래 못 감 → 포기
        }
        else if (Time.time - investigateArrivedAt >= investigateDwell)
        {
            EndInvestigate(c);                                         // 다 살펴봄 → 순찰 복귀
        }
        // dwell 중엔 앵커를 소음 지점에 둔 채 대기(멤버는 주변 대형 유지 = "둘러봄").
    }

    private void EndInvestigate(Vector3 resumeCentroid)
    {
        investigating = false;
        patrolAnchor = resumeCentroid; // 현재 위치에서 patrolDir 그대로 순찰 재개
    }

    // 순찰 앵커/방향 1회 초기화. 첫 방향은 플레이어 쪽(가장자리 스폰이라 안쪽으로 들어가야 함),
    // 플레이어를 못 찾으면 랜덤. 이후엔 재조정 없이 고정.
    private void EnsurePatrolInit()
    {
        if (patrolInit)
            return;
        patrolAnchor = Centroid();
        if (PlayerPos(out var pp))
            patrolDir = SquadRoam.InitialPatrolDirection(patrolAnchor, pp);
        else
        {
            Vector2 r = Random.insideUnitCircle.normalized;
            patrolDir = new Vector3(r.x, 0f, r.y);
            if (patrolDir.sqrMagnitude < 1e-4f) patrolDir = Vector3.forward;
        }
        patrolInit = true;
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    // 분대 앵커를 처음 정한 방향으로 직진시킨다(벽/네브메시 끝에서만 반전). 멤버는 앵커 주변 대형으로 따라온다.
    // 플레이어를 추적하지 않는다 — 그냥 정해진 방향으로 순찰. 경계→순찰 복귀 시 방향이 유지돼 가던 길 계속.
    private void AdvancePatrol()
    {
        EnsurePatrolInit();

        // 경계 등으로 분대가 앵커에서 크게 벗어났으면(흩어졌다 복귀) 현재 중심으로 앵커 재설정 →
        // 옛 앵커로 되돌아가지 않고 "있던 자리에서 같은 방향으로" 순찰을 이어간다.
        Vector3 drift = Centroid() - patrolAnchor; drift.y = 0f;
        if (drift.magnitude > patrolAdvance * 1.5f)
            patrolAnchor = Centroid();

        // 가장 뒤처진 멤버까지 앵커 근처에 모였을 때만 전진(낙오 방지 → 뭉침 유지)
        if (!SquadFormation.AllGathered(MemberPositions(), patrolAnchor, patrolFormationRadius))
            return; // 아직 모이는 중 — 앵커 정지(뒤처진 멤버 대기)

        // 처음 방향 그대로 한 칸 전진(막히면 반대로 반전 — 맵 끝에 닿으면 §6.3.2 디스폰).
        Vector3 next = patrolAnchor + patrolDir * patrolAdvance;
        bool blocked = !NavMesh.SamplePosition(next, out var hit, 5f, NavMesh.AllAreas);
        if (!blocked)
            patrolAnchor = hit.position;
        patrolDir = SquadRoam.NextPatrolDirection(patrolDir, blocked);
    }

    // 이번 틱 분대 의사결정(순수 SquadDecision). 비교전 호출 — 가장자리 latch(hasLeftEdge)도 여기서 갱신.
    private SquadIntent CurrentIntent()
    {
        bool atEdge = roaming && SquadRoam.IsAtEdge(Centroid(), mapCenter, mapHalfExtent, despawnMargin);
        if (roaming && !atEdge)
            hasLeftEdge = true; // 안쪽으로 들어옴 — 이제부터 가장자리 도달 시 디스폰 가능
        return SquadDecision.Resolve(Engaged, roaming, hasLeftEdge, atEdge);
    }

    // 멤버 전부 + 분대 오브젝트 제거(맵 끝까지 순찰해 사라짐). 디렉터가 빈자리를 새 분대로 채운다.
    private void Despawn()
    {
        for (int i = members.Count - 1; i >= 0; i--)
            if (members[i] != null)
                Destroy(members[i].transform.root.gameObject);
        members.Clear();
        Destroy(gameObject);
    }

    // 첫 순찰 방향용 플레이어 위치(태그로 1회 탐색·캐싱).
    private bool PlayerPos(out Vector3 pos)
    {
        if (player == null)
        {
            var go = GameObject.FindWithTag("Player");
            if (go != null) player = go.transform;
        }
        if (player != null) { pos = player.position; return true; }
        pos = default;
        return false;
    }

    /// <summary>멤버의 순찰 목표 = 앵커 주변 대형 위치(황금각 분산). 분대원 GetPatrolDestination이 사용.</summary>
    public bool TryGetPatrolPoint(Enemy m, out Vector3 point)
    {
        if (!patrolInit)
        {
            point = Centroid();
            return members.Count > 0;
        }

        int idx = members.IndexOf(m);
        Vector3 p = SquadFormation.SpiralPoint(patrolAnchor, idx, members.Count, patrolFormationRadius);
        if (NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas))
            p = hit.position;
        point = p;
        return true;
    }

    private Vector3 Centroid()
    {
        Vector3 c = Vector3.zero; int n = 0;
        for (int i = 0; i < members.Count; i++)
            if (members[i] != null) { c += members[i].transform.position; n++; }
        return n > 0 ? c / n : transform.position;
    }

    // 살아있는 멤버의 현재 위치(전진 게이트 계산용).
    private List<Vector3> MemberPositions()
    {
        var list = new List<Vector3>(members.Count);
        for (int i = 0; i < members.Count; i++)
            if (members[i] != null)
                list.Add(members[i].transform.position);
        return list;
    }

    private void PruneDead()
    {
        for (int i = members.Count - 1; i >= 0; i--)
        {
            var m = members[i];
            bool dead = m == null || m.health == null || m.health.currentHealth <= 0;
            if (dead)
                members.RemoveAt(i);
        }
    }

    // 상태 변경 없이 현재 의도만 읽기(기즈모용).
    private SquadIntent PeekIntent(out bool atEdge)
    {
        atEdge = roaming && SquadRoam.IsAtEdge(Centroid(), mapCenter, mapHalfExtent, despawnMargin);
        return SquadDecision.Resolve(Engaged, roaming, hasLeftEdge, atEdge);
    }

    // 분대가 "무엇을 하려는지" 한눈에: 의도색 앵커 구슬 + 전진 화살표 + 대형 목표점 + (로밍) 디스폰 경계·플레이어 라인.
    // 게임뷰/씬뷰 Gizmos 토글로 플레이 중에도 보인다. 그리는 결정은 SquadDecision(테스트됨)과 동일.
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || members.Count == 0)
            return;

        var intent = PeekIntent(out bool atEdge);
        Color c;
        switch (intent)
        {
            case SquadIntent.Engaging:   c = Color.red; break;
            case SquadIntent.Despawning: c = Color.yellow; break;
            default:                     c = Color.cyan; break; // Patrolling
        }

        Vector3 centroid = Centroid();
        Vector3 anchor = patrolInit ? patrolAnchor : centroid;
        Vector3 up = Vector3.up * 0.4f;

        // 앵커(분대가 모이려는 지점) + 분대 중심→앵커 라인
        Gizmos.color = c;
        Gizmos.DrawSphere(anchor + up, 0.5f);
        Gizmos.color = new Color(c.r, c.g, c.b, 0.5f);
        Gizmos.DrawLine(centroid + up, anchor + up);
        Gizmos.DrawWireSphere(anchor + up, patrolFormationRadius);

        // 멤버별 대형 목표점(황금각) + 멤버→목표 라인
        Gizmos.color = new Color(c.r, c.g, c.b, 0.35f);
        for (int i = 0; i < members.Count; i++)
        {
            if (members[i] == null) continue;
            Vector3 fp = SquadFormation.SpiralPoint(anchor, i, members.Count, patrolFormationRadius);
            Gizmos.DrawLine(members[i].transform.position + up, fp + up);
            Gizmos.DrawWireSphere(fp + up, 0.18f);
        }

        // 전진 방향 화살표(앵커 → 다음 전진 지점)
        if (patrolInit && patrolDir.sqrMagnitude > 1e-4f)
        {
            Vector3 tip = anchor + patrolDir.normalized * patrolAdvance + up;
            Gizmos.color = c;
            Gizmos.DrawLine(anchor + up, tip);
            Vector3 back = -patrolDir.normalized;
            Gizmos.DrawLine(tip, tip + (Quaternion.AngleAxis(25f, Vector3.up) * back) * 1.2f);
            Gizmos.DrawLine(tip, tip + (Quaternion.AngleAxis(-25f, Vector3.up) * back) * 1.2f);
        }

        // 로밍: 디스폰 경계(안쪽 사각)
        if (roaming)
        {
            float inner = mapHalfExtent - despawnMargin;
            Gizmos.color = atEdge ? Color.yellow : new Color(c.r, c.g, c.b, 0.25f);
            DrawSquareXZ(mapCenter, inner, mapCenter.y + 0.05f);
        }

#if UNITY_EDITOR
        var style = new UnityEngine.GUIStyle { fontSize = 12 };
        style.normal.textColor = c;
        UnityEditor.Handles.Label(anchor + Vector3.up * 1.2f, $"{intent}  ({members.Count})", style);
#endif
    }

    private static void DrawSquareXZ(Vector3 center, float half, float y)
    {
        Vector3 a = new Vector3(center.x - half, y, center.z - half);
        Vector3 b = new Vector3(center.x + half, y, center.z - half);
        Vector3 d = new Vector3(center.x + half, y, center.z + half);
        Vector3 e = new Vector3(center.x - half, y, center.z + half);
        Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, d); Gizmos.DrawLine(d, e); Gizmos.DrawLine(e, a);
    }
}
