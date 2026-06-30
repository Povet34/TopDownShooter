using UnityEngine;
using UnityEngine.InputSystem;
using TDS.Core;

/// <summary>
/// 수송선 탈출 존(글루). 흐름: <b>호출(C)</b> → 하늘에서 <b>강하·착륙</b> → 착륙 후 반경 안에서
/// <see cref="boardTime"/>초 <b>탑승</b> → 탈출 완료(IsExtracted) + 휴대 전리품 반출(Bank) + 승리.
/// 진행도는 순수 <see cref="ExtractionProgress"/>(EditMode 테스트). 호출 전엔 패드 상공에 대기(랜드마크).
/// </summary>
[DisallowMultipleComponent]
public class ExtractionZone : MonoBehaviour
{
    public enum Stage { Hovering, Descending, Landed }

    [SerializeField] private float radius = 6f;
    [SerializeField] private float boardTime = 3f;
    [SerializeField] private bool resetOnLeave = false;
    [SerializeField] private float skyHeight = 32f;
    [SerializeField] private Vector3 approachOffset = new Vector3(0f, 0f, -42f); // 상공 + 남쪽에서 진입
    [SerializeField] private float descendTime = 6f;

    private ExtractionProgress progress;
    private Transform player;
    private Vector3 landedPos, parkedPos;
    private float descendT;

    public Stage CurrentStage { get; private set; } = Stage.Hovering;
    public bool IsExtracted { get; private set; }
    public bool PlayerInZone { get; private set; }
    public bool Called => CurrentStage != Stage.Hovering;
    public bool IsLanded => CurrentStage == Stage.Landed;
    public float Progress01 => progress != null ? progress.Progress01 : 0f;
    public int BankedOnExtract { get; private set; }
    public Vector3 LandedPosition => landedPos;
    public float DescendTime { get => descendTime; set => descendTime = Mathf.Max(0f, value); }

    public void Configure(float zoneRadius, float seconds)
    {
        radius = Mathf.Max(0.5f, zoneRadius);
        boardTime = Mathf.Max(0f, seconds);
        progress = new ExtractionProgress(boardTime, resetOnLeave);
        landedPos = transform.position;
        parkedPos = landedPos + Vector3.up * skyHeight + approachOffset;
        transform.position = parkedPos; // 호출 전엔 상공 대기
        CurrentStage = Stage.Hovering;
    }

    private void Awake()
    {
        if (progress == null)
        {
            progress = new ExtractionProgress(boardTime, resetOnLeave);
            landedPos = transform.position;
            parkedPos = landedPos;
        }
    }

    /// <summary>수송선 호출 — 착륙 시퀀스 시작(이미 호출됐으면 무시).</summary>
    public void Call()
    {
        if (CurrentStage == Stage.Hovering) { CurrentStage = Stage.Descending; descendT = 0f; }
    }

    private void Update()
    {
        if (IsExtracted) return;

        if (player == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) player = p.transform;
        }

        if (CurrentStage == Stage.Hovering)
        {
            if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame) Call();
            return;
        }

        if (CurrentStage == Stage.Descending)
        {
            descendT += Time.deltaTime;
            float t = descendTime > 0f ? Mathf.Clamp01(descendT / descendTime) : 1f;
            float eased = 1f - (1f - t) * (1f - t); // ease-out 강하
            transform.position = Vector3.Lerp(parkedPos, landedPos, eased);
            if (t >= 1f) { transform.position = landedPos; CurrentStage = Stage.Landed; }
            return;
        }

        // Landed — 반경 안에 머물면 탑승 진행.
        if (player == null) return;
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
