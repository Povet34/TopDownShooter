using UnityEngine;
using UnityEngine.UI;
using TDS.Core;

/// <summary>
/// 저체력 빨간 화면 비네트(TDS.Game, 자족형). 플레이어 체력이 낮아지면 화면 가장자리가
/// 빨갛게 맥동한다 — 생존 긴장감. 강도는 순수 <see cref="HealthVignette"/>(EditMode 테스트),
/// 비네트 텍스처/캔버스는 코드로 생성. 레이더(90)와 HUD 종료패널(100) 사이(95)에 그린다.
/// </summary>
[DisallowMultipleComponent]
public class LowHealthVignette : MonoBehaviour
{
    [SerializeField] private float startRatio = 0.35f;
    [SerializeField] private float pulseSpeed = 6f;
    [SerializeField] private Color tint = new Color(0.7f, 0.05f, 0.05f, 1f);

    private HealthController playerHealth;
    private Image vignette;

    private void Start() => BuildUI();

    private void Update()
    {
        if (vignette == null) return;

        if (playerHealth == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) playerHealth = p.GetComponentInChildren<HealthController>();
        }

        float intensity = playerHealth != null
            ? HealthVignette.Intensity(playerHealth.currentHealth, playerHealth.maxHealth, startRatio)
            : 0f;

        float a = intensity > 0f ? intensity * HealthVignette.Pulse(Time.time, pulseSpeed) : 0f;
        var c = tint; c.a = a;
        vignette.color = c;
        vignette.enabled = a > 0.001f;
    }

    private void BuildUI()
    {
        var canvasGo = new GameObject("Vignette_Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95; // 레이더(90) 위, 종료 패널(100) 아래

        var go = new GameObject("Vignette");
        go.transform.SetParent(canvasGo.transform, false);
        vignette = go.AddComponent<Image>();
        vignette.sprite = VignetteSprite();
        vignette.type = Image.Type.Simple;
        vignette.raycastTarget = false;
        vignette.enabled = false;
        var rt = vignette.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    // 중앙 투명 → 가장자리 불투명(방사형). 풀스크린에 늘려 가장자리 비네트.
    private static Sprite vignetteSprite;
    private static Sprite VignetteSprite()
    {
        if (vignetteSprite != null) return vignetteSprite;
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var cols = new Color32[size * size];
        var c = new Vector2(size * 0.5f, size * 0.5f);
        float maxD = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / maxD; // 0 중심..1 모서리
                float a = Mathf.Clamp01((d - 0.45f) / 0.55f); // 0.45 안쪽 투명, 바깥으로 차오름
                a = a * a; // 가장자리로 더 몰리게
                cols[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(cols);
        tex.Apply();
        vignetteSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return vignetteSprite;
    }
}
