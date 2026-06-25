using UnityEngine;

/// <summary>
/// 떨어진 전리품(글루). 플레이어가 트리거 반경에 들어오면 <see cref="PlayerLoot"/> 지갑에 더하고 사라진다.
/// 픽업 로직은 <see cref="Collect"/>로 분리(테스트가 물리 시뮬 없이 직접 호출).
/// </summary>
[DisallowMultipleComponent]
public class LootPickup : MonoBehaviour
{
    [SerializeField] private int value = 1;
    [Tooltip("이 반경 안에 플레이어가 들어오면 자동 획득(트리거 물리에 의존 안 함 — 탑다운 looter 표준)")]
    [SerializeField] private float pickupRadius = 1.3f;
    private bool collected;
    private Transform spin;
    private Transform player;

    public int Value => value;
    public void Configure(int v) => value = Mathf.Max(1, v);

    private void Update()
    {
        if (spin != null) spin.Rotate(0f, 120f * Time.deltaTime, 0f); // 코인 회전(시각)
        if (collected) return;

        // 근접 획득(트리거 물리 비의존) — 플레이어가 반경 안이면 줍는다.
        if (player == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p == null) return;
            player = p.transform;
        }
        Vector3 d = player.position - transform.position; d.y = 0f;
        if (d.sqrMagnitude <= pickupRadius * pickupRadius)
            Collect(player.gameObject);
    }

    /// <summary>플레이어가 획득. 그리드 인벤토리에 수납 + 지갑에 더하고 자신을 제거. 인벤토리 가득이면 보류.</summary>
    public void Collect(GameObject player)
    {
        if (collected || player == null) return;

        // 그리드 인벤토리에 수납(타르코프식). 가득 차면 못 주움 — 자리 비우고 다시.
        var inv = PlayerInventory.Ensure(player);
        if (inv != null && !inv.TryStore(PlayerInventory.LootItem(value)))
            return;

        var loot = PlayerLoot.Ensure(player);
        if (loot == null) return;
        loot.Wallet.Add(value);
        collected = true;
        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) Collect(other.gameObject);
    }

    /// <summary>프리미티브 전리품(금색 코인) 생성 — 프리팹 없이 자족. 넓은 트리거 반경.</summary>
    public static LootPickup CreatePrimitive(Vector3 pos, int value)
    {
        var root = new GameObject("Loot");
        root.transform.position = pos;
        var col = root.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.3f; // 픽업 반경(걸어서 줍기 쉽게)

        var pickup = root.AddComponent<LootPickup>();
        pickup.Configure(value);

        // 시각 코인(콜라이더 제거, 회전)
        var coin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        coin.name = "Coin";
        coin.transform.SetParent(root.transform, false);
        coin.transform.localScale = new Vector3(0.55f, 0.06f, 0.55f); // 납작한 디스크
        var cc = coin.GetComponent<Collider>();
        if (cc != null) Destroy(cc);
        var rend = coin.GetComponent<Renderer>();
        if (rend != null) rend.material.color = new Color(1f, 0.82f, 0.2f);
        pickup.spin = coin.transform;
        return pickup;
    }
}
