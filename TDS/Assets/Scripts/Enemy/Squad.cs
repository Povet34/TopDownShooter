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
    private Transform player;

    public IReadOnlyList<Enemy> Members => members;
    public bool Engaged { get; private set; }

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
            if (roaming && TryDespawnAtEdge()) // 순찰 상태로 반대편 가장자리 도달 → 디스폰
                return;
            AdvancePatrol(); // 비교전 시 분대 함께 로밍(앵커 전진 → 멤버는 GetPatrolDestination으로 따라옴)
            return;
        }

        // ForceEngage는 시야 상실 타이머를 리셋하므로, 트리거가 유지되는 한 전원 교전을 유지한다.
        // 트리거가 사라지면(아무도 안 보이고 피격도 오래됨) 각 적이 제 lose-sight 타이머로 개별 이탈.
        for (int i = 0; i < members.Count; i++)
            if (members[i] != null)
                members[i].SquadEngage();
    }

    // 분대 앵커를 천천히 전진시킨다(분대 중심이 앵커에 가까워지면 다음 지점으로). 멤버는 앵커 주변 대형으로 따라온다.
    private void AdvancePatrol()
    {
        if (!patrolInit)
        {
            patrolAnchor = Centroid();
            Vector2 r = Random.insideUnitCircle.normalized;
            patrolDir = new Vector3(r.x, 0f, r.y);
            if (patrolDir.sqrMagnitude < 1e-4f) patrolDir = Vector3.forward;
            patrolInit = true;
        }

        // 가장 뒤처진 멤버까지 앵커 근처에 모였을 때만 전진(낙오 방지 → 뭉침 유지)
        if (!SquadFormation.AllGathered(MemberPositions(), patrolAnchor, patrolFormationRadius))
            return; // 아직 모이는 중 — 앵커 정지(뒤처진 멤버 대기)

        // 로밍이면 플레이어 쪽으로 대략 전진(좌우 jitter), 아니면 기존 랜덤 워크.
        if (roaming && PlayerPos(out var pp))
            patrolDir = SquadRoam.AdvanceDirectionToward(patrolAnchor, pp, Random.Range(-35f, 35f));
        else
            patrolDir = Quaternion.AngleAxis(Random.Range(-35f, 35f), Vector3.up) * patrolDir;

        Vector3 next = patrolAnchor + patrolDir * patrolAdvance;
        if (NavMesh.SamplePosition(next, out var hit, 5f, NavMesh.AllAreas))
            patrolAnchor = hit.position;
        else
            patrolDir = -patrolDir;
    }

    // 순찰 상태로 맵 가장자리(스폰 후 한 번 벗어났던)에서 디스폰. 디스폰했으면 true.
    private bool TryDespawnAtEdge()
    {
        bool atEdge = SquadRoam.IsAtEdge(Centroid(), mapCenter, mapHalfExtent, despawnMargin);
        if (!atEdge)
            hasLeftEdge = true; // 안쪽으로 들어옴 — 이제부터 가장자리 도달 시 디스폰 가능
        else if (hasLeftEdge && SquadRoam.ShouldDespawn(patrolling: true, atEdge: true))
        {
            Despawn();
            return true;
        }
        return false;
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
}
