using UnityEngine;
using TDS.Core;

/// <summary>
/// 캐릭터 상태이상(디버프) 글루(TDS.Game). 순수 <see cref="StatusEffects"/>를 들고 매 프레임 진행시켜
/// 출혈 DoT를 <see cref="Player_Health.TakeStatusDamage"/>로 적용하고, 슬로우/스턴을
/// <see cref="Player_Movement.ExternalSpeedMultiplier"/>로 이동에 반영한다. 도보 피격 시 확률적 출혈.
/// </summary>
[DisallowMultipleComponent]
public class PlayerStatus : MonoBehaviour
{
    [SerializeField] private float bleedChanceOnHit = 0.25f;
    [SerializeField] private float bleedDuration = 5f;
    [SerializeField] private float bleedDps = 4f;

    public StatusEffects Effects { get; } = new StatusEffects();

    private Player_Health health;
    private Player_Movement movement;
    private float bleedAccum;

    public static PlayerStatus Ensure(GameObject player)
    {
        if (player == null) return null;
        return player.GetComponent<PlayerStatus>() ?? player.AddComponent<PlayerStatus>();
    }

    private void Awake()
    {
        health = GetComponent<Player_Health>();
        movement = GetComponent<Player_Movement>();
    }

    private void Update()
    {
        float bleed = Effects.Tick(Time.deltaTime);
        if (bleed > 0f && health != null)
        {
            bleedAccum += bleed;
            int whole = Mathf.FloorToInt(bleedAccum);
            if (whole > 0) { bleedAccum -= whole; health.TakeStatusDamage(whole); }
        }

        if (movement != null)
            movement.ExternalSpeedMultiplier = Effects.SpeedMultiplier;
    }

    /// <summary>도보 외부 피격 시 호출 — 큰 피해일수록 출혈 확률이 올라간다.</summary>
    public void OnHit(int damage)
    {
        if (damage <= 0) return;
        float chance = bleedChanceOnHit * Mathf.Clamp(damage / 20f, 0.5f, 2f);
        if (Random.value < chance)
            Effects.Apply(StatusKind.Bleed, bleedDuration, bleedDps);
    }

    public void Apply(StatusKind kind, float duration, float magnitude) => Effects.Apply(kind, duration, magnitude);
}
