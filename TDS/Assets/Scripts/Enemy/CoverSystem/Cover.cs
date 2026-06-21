using System.Collections.Generic;
using UnityEngine;

public class Cover : MonoBehaviour
{
    private Transform playerTransform;

    [Header("Cover points")]
    [SerializeField] private GameObject coverPointPrefab;
    [SerializeField] private List<CoverPoint> coverPoints = new List<CoverPoint>();
    [SerializeField] private float xOffset = 1;
    [SerializeField] private float yOffset = .2f;
    [SerializeField] private float zOffset = 1;

    private bool generated;

    /// <summary>cover 오브젝트의 월드 높이(콜라이더 bounds). range 적합도 판정용.</summary>
    public float CoverHeight { get; private set; }

    /// <summary>낮은 단상(≤0.8)이라 뒤에 서서 그 위로 사격 가능한가. 높으면 은폐 전용.</summary>
    public bool IsShootable => TDS.Core.CoverEvaluation.IsShootable(CoverHeight);

    /// <summary>navmesh로 스냅돼 실제로 도달 가능한 엄폐 지점 수(0이면 쓸 수 없는 cover).</summary>
    public int CoverPointCount => coverPoints.Count;

    /// <summary>이 cover가 range 적에게 적당한지 감사(검증 툴/테스트용). 높이 + 도달 가능 지점으로 판정.</summary>
    public TDS.Core.CoverSuitability AuditForRange()
        => TDS.Core.CoverEvaluation.Evaluate(CoverHeight, coverPoints.Count > 0);

    /// <summary>
    /// 플레이어는 절차적 맵보다 늦게 스폰될 수 있으므로 Start에서 캐싱하지 않고 필요 시 지연 조회한다.
    /// </summary>
    private Transform PlayerTransform
    {
        get
        {
            if (playerTransform == null)
            {
                var p = FindObjectOfType<Player>();
                if (p != null)
                    playerTransform = p.transform;
            }
            return playerTransform;
        }
    }

    private void Start()
    {
        ComputeHeight();
        TagRole();
        GenerateCoverPoints();
    }

    private void TagRole()
    {
        var mo = GetComponent<MapObject>();
        if (mo == null) mo = gameObject.AddComponent<MapObject>();

        bool breakable = GetComponent<Breakable>() != null;
        bool movable = GetComponent<Movable>() != null;
        mo.role = TDS.Core.MapObjectClassifier.Classify(CoverHeight, isCover: true, breakable, movable);
    }

    private void ComputeHeight()
    {
        Physics.SyncTransforms(); // 막 스케일/배치된 cover의 bounds가 최신이도록(미동기 시 unit-cube로 오측정)

        var rends = GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
                b.Encapsulate(rends[i].bounds);
            CoverHeight = b.size.y;
            return;
        }

        var col = GetComponentInChildren<Collider>();
        CoverHeight = col != null ? col.bounds.size.y : 0f;
    }

    /// <summary>
    /// 절차적 맵(MapGenerator)이 프리팹 없이 런타임으로 엄폐물에 붙일 때 사용. Start(생성) 전에 호출.
    /// </summary>
    public void Configure(GameObject coverPointPrefab, float xOffset, float zOffset)
    {
        if (coverPointPrefab != null)
            this.coverPointPrefab = coverPointPrefab;
        this.xOffset = xOffset;
        this.zOffset = zOffset;
    }

    private void GenerateCoverPoints()
    {
        if (generated)
            return;

        if (coverPointPrefab == null)
        {
            Debug.LogWarning($"[Cover] '{name}' coverPointPrefab 미할당 — 엄폐 지점 생성 생략.");
            return;
        }

        Vector3[] localCoverPoints = {
            new Vector3 (0, yOffset, zOffset),  //Front
            new Vector3 (0, yOffset, -zOffset), // Back
            new Vector3(xOffset, yOffset,0),    // Right
            new Vector3(-xOffset,yOffset,0)     // Left
        };

        foreach (Vector3 localPoint in localCoverPoints)
        {
            Vector3 worldPoint = transform.TransformPoint(localPoint);

            // navmesh로 스냅 — 도달 가능한 지점만 만든다(엄폐물 안쪽/맵 밖이라 못 닿아 비비는 버그 방지).
            if (UnityEngine.AI.NavMesh.SamplePosition(worldPoint, out var navHit, 2f, UnityEngine.AI.NavMesh.AllAreas))
                worldPoint = navHit.position;
            else
                continue;

            CoverPoint coverPoint =
                Instantiate(coverPointPrefab, worldPoint, Quaternion.identity, transform).GetComponent<CoverPoint>();

            coverPoints.Add(coverPoint);
        }

        generated = true;
    }

    public List<CoverPoint> GetValidCoverPoints(Transform enemy)
    {
        List<CoverPoint> validCoverPoints = new List<CoverPoint>();

        // 플레이어가 아직 없으면 유효 엄폐를 판정할 수 없음(NullRef 방지).
        if (PlayerTransform == null)
            return validCoverPoints;

        foreach (CoverPoint coverPoint in coverPoints)
        {
            if (IsValidCoverPoint(coverPoint, enemy))
                validCoverPoints.Add(coverPoint);
        }

        return validCoverPoints;
    }

    private bool IsValidCoverPoint(CoverPoint coverPoint, Transform enemy)
    {
        if (coverPoint.occupied)
            return false;

        if (IsFutherestFromPlayer(coverPoint) == false)
            return false;

        if (IsCoverCloseToPlayer(coverPoint))
            return false;

        if (IsCoverBehindPlayer(coverPoint, enemy))
            return false;

        if (IsCoverCloseToLastCover(coverPoint, enemy))
            return false;

        return true;
    }

    private bool IsFutherestFromPlayer(CoverPoint coverPoint)
    {
        CoverPoint futherestPoint = null;
        float futherestDistance = 0;

        foreach (CoverPoint point in coverPoints)
        {
            float distance = Vector3.Distance(point.transform.position, PlayerTransform.position);
            if (distance > futherestDistance)
            {
                futherestDistance = distance;
                futherestPoint = point;
            }
        }

        return futherestPoint == coverPoint;
    }

    private bool IsCoverBehindPlayer(CoverPoint coverPoint, Transform enemy)
    {
        float distanceToPlayer = Vector3.Distance(coverPoint.transform.position, PlayerTransform.position);
        float distanceToEnemy = Vector3.Distance(coverPoint.transform.position, enemy.position);

        return distanceToPlayer < distanceToEnemy;
    }

    private bool IsCoverCloseToPlayer(CoverPoint coverPoint)
    {
        return Vector3.Distance(coverPoint.transform.position, PlayerTransform.position) < 2;
    }

    private bool IsCoverCloseToLastCover(CoverPoint coverPoint, Transform enemy)
    {
        CoverPoint lastCover = enemy.GetComponent<Enemy_Range>().currentCover;
        return lastCover != null &&
            Vector3.Distance(coverPoint.transform.position, lastCover.transform.position) < 3;
    }
}
