using UnityEngine;
using TDS.Core;

/// <summary>
/// 스태시 업그레이드 글루(TDS.Game). 순수 <see cref="StashUpgrades"/>의 레벨을 PlayerPrefs로 영속화하고,
/// <see cref="MetaStash"/> 통화로 구매(<see cref="Buy"/>)한다. 구매/스폰 시 효과를 플레이어에 적용:
/// vitality→최대 체력, swiftness→이동 속도 배수, padding→피해 경감. 통화 차감은 MetaStash.TrySpend.
/// </summary>
[DisallowMultipleComponent]
public class StashUpgradesController : MonoBehaviour
{
    private const string PrefKey = "tds.upgrades";

    public static StashUpgradesController Instance { get; private set; }
    public StashUpgrades Upgrades { get; private set; }

    private GameObject appliedPlayer;
    private int basePlayerMaxHp;

    public static StashUpgradesController Ensure()
    {
        if (Instance != null) return Instance;
        var existing = FindObjectOfType<StashUpgradesController>();
        if (existing != null) return Instance = existing;
        return new GameObject("StashUpgrades").AddComponent<StashUpgradesController>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Upgrades = StashUpgrades.Default();
        Upgrades.LoadLevels(PlayerPrefs.GetString(PrefKey, ""));
    }

    /// <summary>현재 레벨의 효과를 플레이어에 반영(스폰/구매 시). 최대 체력은 prefab 기본값 기준으로 재계산.</summary>
    public void ApplyToPlayer(GameObject player)
    {
        if (player == null) return;

        var hp = player.GetComponent<HealthController>();
        if (hp != null)
        {
            if (player != appliedPlayer) { basePlayerMaxHp = hp.maxHealth; appliedPlayer = player; }
            int newMax = basePlayerMaxHp + Mathf.RoundToInt(Upgrades.TotalBonus("vitality"));
            int delta = newMax - hp.maxHealth;
            hp.maxHealth = newMax;
            hp.currentHealth = Mathf.Min(newMax, hp.currentHealth + Mathf.Max(0, delta)); // 증가분만 회복(풀힐 아님)
        }

        var mv = player.GetComponent<Player_Movement>();
        if (mv != null) mv.UpgradeSpeedMultiplier = 1f + Upgrades.TotalBonus("swiftness");

        var ph = player.GetComponent<Player_Health>();
        if (ph != null) ph.DamageResist = Mathf.Clamp01(Upgrades.TotalBonus("padding"));

        var wc = player.GetComponentInChildren<Player_WeaponController>();
        if (wc != null)
            wc.ApplyUpgradeBonuses(
                Mathf.RoundToInt(Upgrades.TotalBonus("firepower")),
                Mathf.RoundToInt(Upgrades.TotalBonus("munitions")));
    }

    /// <summary>사망 시 보험 회수율(0~1) — Insurance 레벨당 누적. 휴대 전리품의 이 비율이 스태시로 회수된다.</summary>
    public float DeathRecoveryRate => Mathf.Clamp01(Upgrades.TotalBonus("insurance"));

    /// <summary>스태시 통화로 업그레이드 구매 — 결과 메시지 반환(콘솔/UI 표시용).</summary>
    public string Buy(string id)
    {
        var msc = MetaStashController.Instance;
        if (msc == null || msc.Stash == null) return "no stash";

        var def = Upgrades.Def(id);
        if (def == null) return $"unknown upgrade: {id}";
        if (Upgrades.IsMaxed(id)) return $"{def.Name} is maxed (Lv{Upgrades.LevelOf(id)})";

        int cost = Upgrades.CostOf(id);
        if (!msc.Stash.TrySpend(cost))
            return $"need {cost} salvage (have {msc.Stash.Currency})";

        Upgrades.Purchase(id);
        Save();
        msc.Save(); // 차감된 통화 영속

        var p = GameObject.FindWithTag("Player");
        if (p != null) ApplyToPlayer(p);

        return $"bought {def.Name} Lv{Upgrades.LevelOf(id)}  (-{cost} salvage, {msc.Stash.Currency} left)";
    }

    public void Save() => PlayerPrefs.SetString(PrefKey, Upgrades.SerializeLevels());
}
