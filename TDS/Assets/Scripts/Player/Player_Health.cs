using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TDS.Core;

public class Player_Health : HealthController
{
    private Player player;

    public bool isDead { get; private set; }

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
