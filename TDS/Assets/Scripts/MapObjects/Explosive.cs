using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 부서질 때 폭발하는 오브젝트(배럴 등). 같은 GameObject의 <see cref="Breakable"/>가 파괴되는 순간
/// 범위 피해 + 폭발 소음(§6.2.1 Explosion, 90m) + FX를 낸다. 범위 피해가 옆 배럴의 Breakable을 깨면
/// 그 배럴도 폭발 → 자연 연쇄. 피해량은 순수 <see cref="TDS.Core.ExplosionModel"/>로 거리 falloff.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Breakable))]
public class Explosive : MonoBehaviour
{
    [SerializeField] private float radius = 6f;
    [SerializeField] private int maxDamage = 80;
    [Tooltip("폭발 시각 FX(있으면 스폰). 비우면 Breakable 파편만)")]
    [SerializeField] private GameObject explosionFX;

    /// <summary>폭발 FX 프리팹(런타임 생성 배럴에 맵 생성기가 주입).</summary>
    public GameObject ExplosionFX { get => explosionFX; set => explosionFX = value; }

    private bool exploded;

    private void Awake()
    {
        var breakable = GetComponent<Breakable>();
        if (breakable != null)
            breakable.Broken += Explode;
    }

    public void Explode()
    {
        if (exploded)
            return;
        exploded = true;

        Vector3 center = transform.position;

        var hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore);
        var hitTargets = new HashSet<IDamagable>();
        foreach (var col in hits)
        {
            var dmg = col.GetComponentInParent<IDamagable>();
            if (dmg == null || hitTargets.Contains(dmg))
                continue; // 자기 배럴의 Breakable은 이미 broken 상태라 TakeDamage가 무시됨
            hitTargets.Add(dmg);

            float dist = Vector3.Distance(center, col.ClosestPoint(center));
            int dealt = Mathf.RoundToInt(TDS.Core.ExplosionModel.DamageAt(dist, radius, maxDamage));
            if (dealt > 0)
                dmg.TakeDamage(dealt); // 옆 Explosive 배럴이면 Break→Explode로 연쇄
        }

        // §6.2.1: 폭발음(90m) — 들은 적은 플레이어 위치를 알게 됨(폭발=플레이어가 자기 위치 광고).
        var player = GameObject.FindWithTag("Player");
        Vector3 source = player != null ? player.transform.position : center;
        NoisePing.EmitExplosion(center, source);

        if (explosionFX != null)
        {
            var fx = Instantiate(explosionFX, center, Quaternion.identity);
            Destroy(fx, 3f);
        }

        // 카메라 셰이크(가까울수록 강하게) — 자체 FX가 있어 피격 FX는 안 띄움.
        var feedback = TDS.Core.GameServices.Registry?.Resolve<TDS.Core.ICombatFeedbackService>();
        if (feedback != null)
        {
            const float shakeRange = 45f;
            float dist = player != null ? Vector3.Distance(center, player.transform.position) : 0f;
            float intensity = Mathf.Clamp01(1f - dist / shakeRange);
            feedback.ReportExplosion(center, intensity);
        }
    }
}
