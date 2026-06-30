using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;
using TDS.Core;

/// <summary>
/// 맵 씬 자족형 미니 HUD(TDS.Game). 플레이어 체력 / 현재 무기 탄약 / SpawnDirector 웨이브를 표시하고,
/// 승리(모든 웨이브 클리어)·패배(체력 0)를 판정해 종료 패널 + R 재시작을 제공한다.
/// 캔버스/텍스트를 코드로 생성 — UI 프리팹·기존 UI 싱글톤에 의존하지 않아 맵 단독 동작.
/// </summary>
[DisallowMultipleComponent]
public class MapHUD : MonoBehaviour
{
    private HealthController playerHealth;
    private Player_WeaponController playerWeapon;
    private PlayerLoot playerLoot;
    private PlayerStatus playerStatus;
    private SpawnDirector director;
    private ExtractionZone extraction;

    private TextMeshProUGUI healthText, ammoText, waveText, lootText, stashText, debuffText, extractText, carText, endText;
    private GameObject endPanel;
    private bool ended;

    private Car_Controller activeCar;
    private float carScanTimer;

    private void Start()
    {
        BuildUI();
    }

    private void Update()
    {
        EnsureRefs();

        if (playerHealth != null)
            healthText.text = $"HP  {Mathf.Max(0, playerHealth.currentHealth)} / {playerHealth.maxHealth}";

        if (playerWeapon != null)
        {
            var w = playerWeapon.CurrentWeapon();
            if (w != null)
                ammoText.text = $"{w.weaponType}\n<size=120%>{w.bulletsInMagazine}</size> / {w.totalReserveAmmo}";
        }

        if (lootText != null)
            lootText.text = $"SALVAGE  {(playerLoot != null ? playerLoot.Wallet.Carried : 0)}";

        if (stashText != null)
            stashText.text = MetaStashController.Instance != null
                ? $"<size=80%>STASH  {MetaStashController.Instance.Summary()}</size>" : "";

        if (debuffText != null)
            debuffText.text = DebuffLabel();

        UpdateCarReadout();

        if (director != null)
            waveText.text = director.IsRoaming
                ? $"enemies: {director.AliveCount}"
                : director.Finished
                    ? "ALL WAVES CLEARED"
                    : $"WAVE  {director.CurrentWaveNumber} / {director.TotalWaves}     enemies: {director.AliveCount}";

        // 탈출 진행 프롬프트(존 안에 있을 때)
        if (extractText != null)
        {
            bool boarding = extraction != null && extraction.PlayerInZone && !extraction.IsExtracted;
            extractText.text = boarding ? $"EXTRACTING…  {Mathf.RoundToInt(extraction.Progress01 * 100f)}%" : "";
        }

        if (!ended)
        {
            // 판정은 순수 GameOutcome(EditMode 테스트). 플레이어/디렉터 미존재 시 진행 중으로 간주.
            int hp = playerHealth != null ? playerHealth.currentHealth : 1;
            bool finished = director != null && director.Finished;
            int alive = director != null ? director.AliveCount : 1;
            bool extracted = extraction != null && extraction.IsExtracted;

            switch (GameOutcome.Evaluate(hp, finished, alive, extracted))
            {
                case MatchState.Defeat:
                    EndGame("DEFEATED", new Color(0.85f, 0.27f, 0.27f));
                    break;
                case MatchState.Victory:
                    if (extracted)
                        EndGame($"EXTRACTED\n<size=40%>{extraction.BankedOnExtract} salvage secured</size>", new Color(0.35f, 0.8f, 0.45f));
                    else
                        EndGame("VICTORY", new Color(0.35f, 0.8f, 0.45f));
                    break;
            }
        }
        else if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    private void EnsureRefs()
    {
        if (playerHealth == null || playerWeapon == null || playerLoot == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null)
            {
                playerHealth = p.GetComponentInChildren<HealthController>();
                playerWeapon = p.GetComponentInChildren<Player_WeaponController>();
                playerLoot = p.GetComponent<PlayerLoot>(); // 첫 픽업 전엔 null → 0 표시
                playerStatus = p.GetComponent<PlayerStatus>();
            }
        }
        if (director == null)
            director = FindObjectOfType<SpawnDirector>();
        if (extraction == null)
            extraction = FindObjectOfType<ExtractionZone>();
    }

    // 활성 디버프를 색칠된 한 줄로(없으면 빈 문자열).
    private string DebuffLabel()
    {
        if (playerStatus == null || !playerStatus.Effects.Any) return "";
        var sb = new System.Text.StringBuilder();
        foreach (var k in playerStatus.Effects.Active)
        {
            if (sb.Length > 0) sb.Append("   ");
            switch (k)
            {
                case TDS.Core.StatusKind.Bleed: sb.Append("<color=#E5484D>● BLEEDING</color>"); break;
                case TDS.Core.StatusKind.Slow:  sb.Append("<color=#4FC3F7>● SLOWED</color>"); break;
                case TDS.Core.StatusKind.Stun:  sb.Append("<color=#FFD54F>● STUNNED</color>"); break;
            }
        }
        return sb.ToString();
    }

    // 차량 탑승 중이면 속도 + 차 HP 표시(주차 차량은 비활성이라 안 뜸). 스캔은 스로틀.
    private void UpdateCarReadout()
    {
        if (carText == null) return;

        carScanTimer -= Time.deltaTime;
        if (carScanTimer <= 0f || activeCar == null || !activeCar.carActive)
        {
            carScanTimer = 0.25f;
            activeCar = null;
            foreach (var c in FindObjectsByType<Car_Controller>(FindObjectsSortMode.None))
                if (c != null && c.carActive) { activeCar = c; break; }
        }

        if (activeCar != null && activeCar.carActive)
        {
            var hp = activeCar.GetComponent<Car_HealthController>();
            string hpStr = hp != null ? $"     CAR  {Mathf.Max(0, hp.currentHealth)} / {hp.maxHealth}" : "";
            carText.text = $"<size=130%>{Mathf.RoundToInt(activeCar.Speed * 5)}</size> km/h{hpStr}";
        }
        else carText.text = "";
    }

    private void EndGame(string msg, Color color)
    {
        ended = true;
        endPanel.SetActive(true);
        endText.text = $"{msg}\n<size=35%>press  R  to restart</size>";
        endText.color = color;
    }

    // ---------- 코드로 UI 생성 ----------

    private void BuildUI()
    {
        var canvasGo = new GameObject("HUD_Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        healthText = MakeText(canvas.transform, "Health", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 24f), TextAlignmentOptions.BottomLeft, 34f);
        ammoText = MakeText(canvas.transform, "Ammo", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 24f), TextAlignmentOptions.BottomRight, 34f);
        waveText = MakeText(canvas.transform, "Wave", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), TextAlignmentOptions.Top, 30f);
        lootText = MakeText(canvas.transform, "Loot", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -22f), TextAlignmentOptions.TopLeft, 30f);
        lootText.color = new Color(1f, 0.82f, 0.2f); // 금색
        stashText = MakeText(canvas.transform, "Stash", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -58f), TextAlignmentOptions.TopLeft, 26f);
        stashText.color = new Color(0.6f, 0.85f, 1f); // 반출 스태시 = 하늘색
        extractText = MakeText(canvas.transform, "Extract", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), TextAlignmentOptions.Center, 44f);
        debuffText = MakeText(canvas.transform, "Debuff", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 75f), TextAlignmentOptions.Center, 26f);
        extractText.color = new Color(0.3f, 0.85f, 0.95f); // 시안
        carText = MakeText(canvas.transform, "Car", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), TextAlignmentOptions.Bottom, 30f);

        // 종료 패널 (반투명 + 중앙 메시지)
        endPanel = new GameObject("EndPanel");
        endPanel.transform.SetParent(canvas.transform, false);
        var prt = endPanel.AddComponent<RectTransform>();
        prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
        prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
        var img = endPanel.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.72f);
        endText = MakeText(endPanel.transform, "EndText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, TextAlignmentOptions.Center, 90f);
        endPanel.SetActive(false);
    }

    private TextMeshProUGUI MakeText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, TextAlignmentOptions align, float fontSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = anchorMin; // 코너에 고정
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(900f, 240f);

        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = Color.white;
        t.fontStyle = FontStyles.Bold;
        t.raycastTarget = false;
        t.text = "";
        return t;
    }
}
