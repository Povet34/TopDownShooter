using System.Collections.Generic;
using UnityEngine;
using TDS.Core;

/// <summary>
/// 플레이어 시야(FoV) — 시야 콘(각도+사거리) 안 + 가려지지 않은 적만 보이게 하고, 나머지 적의
/// renderer를 끈다(전장의 안개의 게임플레이 절반). 비주얼(지면 밝기 마스크)은 별도.
/// 발사 시 잠깐 시야가 넓고 길어진다.
/// </summary>
public class FieldOfView : MonoBehaviour
{
    [Header("시야")]
    [SerializeField] private float halfAngle = 60f;
    [SerializeField] private float range = 18f;
    [SerializeField] private float eyeHeight = 1.2f;
    [Tooltip("이 반경 안의 적은 콘 밖이라도 항상 인지(인접 자각)")]
    [SerializeField] private float nearRadius = 2.5f;
    [Tooltip("시야를 가리는 환경 레이어(벽/장애물/엄폐물). 적/플레이어/총알은 제외")]
    [SerializeField] private LayerMask occluderMask = ~0;

    [Header("발사 시 시야 확대")]
    [SerializeField] private float revealDuration = 0.4f;
    [SerializeField] private float revealRangeBoost = 6f;
    [SerializeField] private float revealAngleBoost = 25f;

    /// <summary>시야 방향(0이면 transform.forward). 플레이어가 매 프레임 조준 방향으로 세팅.</summary>
    public Vector3 OverrideForward { get; set; }

    private float revealUntil = -999f;
    private readonly Dictionary<Enemy, Renderer[]> tracked = new Dictionary<Enemy, Renderer[]>();

    private Vector3 Forward() => OverrideForward.sqrMagnitude > 1e-4f ? OverrideForward : transform.forward;

    public float CurrentHalfAngle => halfAngle + (Time.time < revealUntil ? revealAngleBoost : 0f);
    public float CurrentRange => range + (Time.time < revealUntil ? revealRangeBoost : 0f);

    // 비주얼 마스크(VisionMask)와 단일 소스 공유
    public Vector3 ViewDirection => Forward();
    public float NearRadius => nearRadius;
    public LayerMask OccluderMask => occluderMask;
    public float EyeHeight => eyeHeight;

    /// <summary>발사 등으로 시야를 잠깐 확대.</summary>
    public void Reveal() => revealUntil = Time.time + revealDuration;

    public bool IsVisible(Vector3 targetPos)
    {
        // 아주 가까우면(인접) 콘 밖이라도 인지
        Vector3 flat = targetPos - transform.position; flat.y = 0f;
        if (flat.sqrMagnitude <= nearRadius * nearRadius)
            return true;

        if (!ViewCone.InView(transform.position, Forward(), targetPos, CurrentHalfAngle, CurrentRange))
            return false;

        // 가림: 눈높이에서 대상까지 환경에 막히면 안 보임
        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 t = targetPos + Vector3.up * eyeHeight;
        Vector3 dir = t - eye;
        float dist = dir.magnitude;
        if (dist < 0.5f)
            return true;

        return !Physics.Raycast(eye, dir / dist, dist - 0.4f, occluderMask, QueryTriggerInteraction.Ignore);
    }

    // 시야가 꺼지면(FoV Off 등) 숨겼던 적을 다시 보이게 한다.
    private void OnDisable()
    {
        foreach (var kv in tracked)
        {
            if (kv.Value == null) continue;
            foreach (var r in kv.Value)
                if (r != null) r.enabled = true;
        }
    }

    private void Update()
    {
        var enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (var e in enemies)
        {
            if (e == null) continue;

            if (!tracked.TryGetValue(e, out var rends) || rends == null)
            {
                rends = e.GetComponentsInChildren<Renderer>(true);
                tracked[e] = rends;
            }

            bool vis = IsVisible(e.transform.position);
            for (int i = 0; i < rends.Length; i++)
                if (rends[i] != null) rends[i].enabled = vis;
        }
    }
}
