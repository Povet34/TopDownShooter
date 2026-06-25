using UnityEngine;
using TDS.Core;

/// <summary>
/// 수송선 탈출 존(글루). 플레이어가 반경 안에 <see cref="boardTime"/>초 머물면 탈출 완료(IsExtracted) →
/// 휴대 전리품 반출(Bank) + 승리. 진행도는 순수 <see cref="ExtractionProgress"/>(EditMode 테스트).
/// 플레이어 감지는 근접 반경(트리거 물리 비의존 — looter 픽업과 동일 패턴).
/// </summary>
[DisallowMultipleComponent]
public class ExtractionZone : MonoBehaviour
{
    [SerializeField] private float radius = 6f;
    [SerializeField] private float boardTime = 3f;
    [SerializeField] private bool resetOnLeave = false;

    private ExtractionProgress progress;
    private Transform player;

    public bool IsExtracted { get; private set; }
    public bool PlayerInZone { get; private set; }
    public float Progress01 => progress != null ? progress.Progress01 : 0f;
    public int BankedOnExtract { get; private set; }

    public void Configure(float zoneRadius, float seconds)
    {
        radius = Mathf.Max(0.5f, zoneRadius);
        boardTime = Mathf.Max(0f, seconds);
        progress = new ExtractionProgress(boardTime, resetOnLeave);
    }

    private void Awake()
    {
        if (progress == null) progress = new ExtractionProgress(boardTime, resetOnLeave);
    }

    private void Update()
    {
        if (IsExtracted) return;
        if (player == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p == null) return;
            player = p.transform;
        }

        Vector3 d = player.position - transform.position; d.y = 0f;
        PlayerInZone = d.sqrMagnitude <= radius * radius;
        progress.Tick(Time.deltaTime, PlayerInZone);

        if (progress.IsComplete)
        {
            IsExtracted = true;
            var loot = player.GetComponent<PlayerLoot>();
            BankedOnExtract = loot != null ? loot.Wallet.Bank() : 0; // 전리품 반출
        }
    }
}
