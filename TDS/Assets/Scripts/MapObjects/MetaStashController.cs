using UnityEngine;
using TDS.Core;

/// <summary>
/// 반출 전리품 스태시 글루(TDS.Game). 런 사이에 유지되는 순수 <see cref="MetaStash"/>를 들고
/// PlayerPrefs로 영속화한다. 탈출(<see cref="ExtractionZone.IsExtracted"/>) 시 휴대 통화(BankedOnExtract)
/// + 인벤토리 아이템을 스태시에 넣고 저장 → 익스트랙션 루프의 보상이 실제로 쌓인다.
/// </summary>
[DisallowMultipleComponent]
public class MetaStashController : MonoBehaviour
{
    private const string PrefKey = "tds.metastash";

    public static MetaStashController Instance { get; private set; }
    public MetaStash Stash { get; private set; }

    private ExtractionZone lastZone;
    private bool deposited;
    private float scanTimer;

    public static MetaStashController Ensure()
    {
        if (Instance != null) return Instance;
        var existing = FindObjectOfType<MetaStashController>();
        if (existing != null) return Instance = existing;
        return new GameObject("MetaStash").AddComponent<MetaStashController>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Stash = MetaStash.Deserialize(PlayerPrefs.GetString(PrefKey, ""));
    }

    private void Update()
    {
        scanTimer -= Time.deltaTime;
        if (scanTimer > 0f) return;
        scanTimer = 0.3f;

        var zone = FindObjectOfType<ExtractionZone>();
        if (zone == null) return;
        if (zone != lastZone) { lastZone = zone; deposited = false; } // 새 런 = 새 존
        if (deposited || !zone.IsExtracted) return;

        DepositRun(zone.BankedOnExtract);
        deposited = true;
    }

    /// <summary>이번 런의 통화 + 휴대 인벤토리 아이템을 스태시에 넣고 저장.</summary>
    public void DepositRun(int currency)
    {
        Stash.AddCurrency(currency);

        var player = GameObject.FindWithTag("Player");
        var inv = player != null ? player.GetComponent<PlayerInventory>() : null;
        if (inv != null && inv.Grid != null)
            foreach (var it in inv.Grid.Items)
                Stash.AddItem(it.Item.Id);

        Save();
    }

    public void Save() => PlayerPrefs.SetString(PrefKey, Stash.Serialize());

    public void ClearStash() { Stash.Clear(); Save(); }

    /// <summary>HUD/콘솔 표시용 한 줄 요약.</summary>
    public string Summary() => $"{Stash.Currency} salvage · {Stash.TotalItemCount} items";
}
