using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TDS.Core;

public class Player_Health : HealthController
{
    private Player player;

    public bool isDead { get; private set; }

    /// <summary>영구 업그레이드(Padding) 피해 경감률 0~1. 외부 피격에만 적용(출혈 DoT엔 미적용). StashUpgrades가 설정.</summary>
    public float DamageResist { get; set; }

    protected override void Awake()
    {
        base.Awake();

        player = GetComponent<Player>();
    }

    public override void ReduceHealth(int damage)
    {
        // 차량 탑승 중(플레이어가 차에 parent됨)이면 데미지를 차로 돌린다 — 몬스터가 차를 공격하는 효과.
        var car = GetComponentInParent<Car_HealthController>();
        if (car != null)
        {
            car.TakeDamage(damage);
            return;
        }

        int dealt = Mathf.Max(0, Mathf.RoundToInt(damage * (1f - DamageResist))); // Padding 경감
        ApplyDamage(dealt);
        GetComponent<PlayerStatus>()?.OnHit(dealt); // 외부 피격(도보) → 확률적 출혈 디버프
    }

    /// <summary>상태이상(출혈 등) DoT — 차 리다이렉트/추가 출혈 유발 없이 곧장 체력만 깎는다.</summary>
    public void TakeStatusDamage(int damage) => ApplyDamage(damage);

    private void ApplyDamage(int damage)
    {
        base.ReduceHealth(damage);

        GameServices.Registry.Resolve<ICombatFeedbackService>()?.ReportHit(transform.position + Vector3.up, 1.4f);

        if (ShouldDie())
            Die();

        if (UI.instance != null) // UI 없는 컨텍스트(맵 단독/테스트)에서도 안전
            UI.instance.inGameUI.UpdateHealthUI(currentHealth, maxHealth);
    }

    private void Die()
    {
        if (isDead)
            return;



        Debug.Log("Player was killed at " + Time.time);
        isDead = true;
        player.anim.enabled = false;
        player.ragdoll.RagdollActive(true);

        GameManager.instance.GameOver();
    }
}
