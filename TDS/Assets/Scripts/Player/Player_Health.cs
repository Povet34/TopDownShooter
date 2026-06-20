using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
        base.ReduceHealth(damage);

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
