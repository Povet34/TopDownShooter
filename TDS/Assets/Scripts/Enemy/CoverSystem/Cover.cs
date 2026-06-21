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
        GenerateCoverPoints();
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
