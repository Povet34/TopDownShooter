using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;

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
    private SpawnDirector director;

    private TextMeshProUGUI healthText, ammoText, waveText, endText;
    private GameObject endPanel;
    private bool ended;

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

        if (director != null)
            waveText.text = director.Finished
                ? "ALL WAVES CLEARED"
                : $"WAVE  {director.CurrentWaveNumber} / {director.TotalWaves}     enemies: {director.AliveCount}";

        if (!ended)
        {
            if (playerHealth != null && playerHealth.currentHealth <= 0)
                EndGame("DEFEATED", new Color(0.85f, 0.27f, 0.27f));
            else if (director != null && director.Finished && director.AliveCount <= 0)
                EndGame("VICTORY", new Color(0.35f, 0.8f, 0.45f));
        }
        else if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    private void EnsureRefs()
    {
        if (playerHealth == null || playerWeapon == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null)
            {
                playerHealth = p.GetComponentInChildren<HealthController>();
                playerWeapon = p.GetComponentInChildren<Player_WeaponController>();
            }
        }
        if (director == null)
            director = FindObjectOfType<SpawnDirector>();
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
