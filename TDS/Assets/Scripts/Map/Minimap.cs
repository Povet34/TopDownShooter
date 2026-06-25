using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TDS.Core;

/// <summary>
/// 맵 씬 자족형 레이더/미니맵(TDS.Game). 1024 대형 맵 항법 보조 — 플레이어 중심(북-업)으로
/// 반경 <see cref="worldRange"/>m 안의 적을 빨간 블립으로 표시, 범위 밖은 가장자리에 붙여 방향만 알려준다.
/// 좌표 변환은 순수 <see cref="MinimapProjection"/>(EditMode 테스트). 캔버스/스프라이트를 코드로 생성 —
/// UI 프리팹·싱글톤에 의존하지 않아 맵 단독 동작. M 키로 토글.
/// </summary>
[DisallowMultipleComponent]
public class Minimap : MonoBehaviour
{
    [Tooltip("레이더에 보이는 월드 반경(m). 이 거리 = 레이더 가장자리")]
    [SerializeField] private float worldRange = 90f;
    [Tooltip("레이더 반경(픽셀, 1080 기준)")]
    [SerializeField] private float radiusPixels = 105f;
    [Tooltip("블립 풀 상한")]
    [SerializeField] private int maxBlips = 80;
    [SerializeField] private float updateInterval = 0.08f;

    private Transform player;
    private RectTransform blipRoot;   // 블립 좌표계(중심=플레이어)
    private RectTransform noseMarker; // 플레이어 진행방향 표시
    private Image extractionBlip;      // 수송선(탈출 목표) — 항상 표시(가장자리 클램프로 방향 안내)
    private MapGenerator mapGen;
    private GameObject radarRoot;
    private readonly List<Image> blips = new List<Image>();
    private float timer;
    private bool visible = true;

    private static readonly Color BlipNear = new Color(0.92f, 0.27f, 0.24f, 0.95f);
    private static readonly Color BlipEdge = new Color(0.92f, 0.27f, 0.24f, 0.45f);
    private static readonly Color BlipCar = new Color(1f, 0.7f, 0.15f, 0.95f); // 차량 = 주황
    private readonly List<Image> carBlips = new List<Image>();

    private void Start() => BuildUI();

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
        {
            visible = !visible;
            if (radarRoot != null) radarRoot.SetActive(visible);
        }
        if (!visible) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = updateInterval;

        if (player == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p == null) { HideAllBlips(); return; }
            player = p.transform;
        }

        Vector2 pXZ = new Vector2(player.position.x, player.position.z);

        // 진행/조준 방향(북-업: 월드 +Z = 위)
        Vector3 fwd = player.forward;
        if (noseMarker != null && (Mathf.Abs(fwd.x) > 0.001f || Mathf.Abs(fwd.z) > 0.001f))
        {
            Vector2 dir = new Vector2(fwd.x, fwd.z).normalized;
            noseMarker.anchoredPosition = dir * (radiusPixels * 0.7f);
        }

        // 수송선(탈출 목표) — 항상 표시, 범위 밖이면 가장자리에 붙여 방향 안내.
        if (extractionBlip != null)
        {
            if (mapGen == null) mapGen = FindObjectOfType<MapGenerator>();
            if (mapGen != null && mapGen.HasExtraction)
            {
                Vector2 eXZ = new Vector2(mapGen.ExtractionPosition.x, mapGen.ExtractionPosition.z);
                extractionBlip.rectTransform.anchoredPosition = MinimapProjection.ToMinimap(eXZ, pXZ, worldRange, radiusPixels, out _);
                extractionBlip.gameObject.SetActive(true);
            }
            else extractionBlip.gameObject.SetActive(false);
        }

        // 차량 — 주황 블립(항상 표시, 컬링돼 비활성이어도 위치 유효). 찾아가기 쉽게.
        int carsUsed = 0;
        if (mapGen != null)
        {
            var cars = mapGen.CarTransforms;
            for (int i = 0; i < cars.Count; i++)
            {
                var ct = cars[i];
                if (ct == null) continue;
                Vector2 cXZ = new Vector2(ct.position.x, ct.position.z);
                var cb = GetCarBlip(carsUsed++);
                cb.rectTransform.anchoredPosition = MinimapProjection.ToMinimap(cXZ, pXZ, worldRange, radiusPixels, out _);
                cb.gameObject.SetActive(true);
            }
        }
        for (int i = carsUsed; i < carBlips.Count; i++)
            carBlips[i].gameObject.SetActive(false);

        int used = 0;
        var enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (var e in enemies)
        {
            if (e == null || !e.isActiveAndEnabled) continue;
            if (used >= maxBlips) break;
            Vector2 eXZ = new Vector2(e.transform.position.x, e.transform.position.z);
            Vector2 local = MinimapProjection.ToMinimap(eXZ, pXZ, worldRange, radiusPixels, out bool outside);

            var blip = GetBlip(used++);
            blip.rectTransform.anchoredPosition = local;
            blip.color = outside ? BlipEdge : BlipNear;
            blip.gameObject.SetActive(true);
        }
        for (int i = used; i < blips.Count; i++)
            blips[i].gameObject.SetActive(false);
    }

    private Image GetBlip(int i)
    {
        while (blips.Count <= i)
        {
            var go = new GameObject("Blip");
            go.transform.SetParent(blipRoot, false);
            var img = go.AddComponent<Image>();
            img.sprite = CircleSprite();
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(8f, 8f);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            blips.Add(img);
        }
        return blips[i];
    }

    private Image GetCarBlip(int i)
    {
        while (carBlips.Count <= i)
        {
            var go = new GameObject("CarBlip");
            go.transform.SetParent(blipRoot, false);
            var img = go.AddComponent<Image>();
            img.sprite = CircleSprite();
            img.color = BlipCar;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(10f, 10f);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            carBlips.Add(img);
        }
        return carBlips[i];
    }

    private void HideAllBlips()
    {
        foreach (var b in blips) if (b != null) b.gameObject.SetActive(false);
    }

    // ---------- 코드로 UI 생성 ----------

    // 런타임 생성 원형 스프라이트(빌트인 UI/Skin/Knob.psd가 이 유니티 버전엔 없어 직접 그림). 안티앨리어스 가장자리.
    private static Sprite circleSprite;
    private static Sprite CircleSprite()
    {
        if (circleSprite != null) return circleSprite;
        const int size = 64;
        const float r = size * 0.5f;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var cols = new Color32[size * size];
        var c = new Vector2(r, r);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                float a = Mathf.Clamp01(r - d); // 가장자리 1px 페이드
                cols[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(cols);
        tex.Apply();
        circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return circleSprite;
    }

    private void BuildUI()
    {
        var canvasGo = new GameObject("Radar_Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90; // HUD 종료 패널(100)보다 아래
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        float d = radiusPixels * 2f;

        // 레이더 루트: 우상단 코너
        radarRoot = new GameObject("Radar");
        var rrt = radarRoot.AddComponent<RectTransform>();
        rrt.SetParent(canvas.transform, false);
        rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(1f, 1f);
        rrt.anchoredPosition = new Vector2(-28f, -28f);
        rrt.sizeDelta = new Vector2(d, d);

        // 원형 배경(어두운 반투명)
        var bg = new GameObject("Bg").AddComponent<Image>();
        bg.transform.SetParent(rrt, false);
        bg.sprite = CircleSprite();
        bg.color = new Color(0.06f, 0.07f, 0.05f, 0.55f);
        bg.raycastTarget = false;
        var bgrt = bg.rectTransform;
        bgrt.anchorMin = Vector2.zero; bgrt.anchorMax = Vector2.one;
        bgrt.offsetMin = Vector2.zero; bgrt.offsetMax = Vector2.zero;

        // 블립 좌표계(중심)
        var brGo = new GameObject("BlipRoot");
        blipRoot = brGo.AddComponent<RectTransform>();
        blipRoot.SetParent(rrt, false);
        blipRoot.anchorMin = blipRoot.anchorMax = blipRoot.pivot = new Vector2(0.5f, 0.5f);
        blipRoot.anchoredPosition = Vector2.zero;
        blipRoot.sizeDelta = Vector2.zero;

        // 진행방향 노즈
        var nose = new GameObject("Nose").AddComponent<Image>();
        noseMarker = nose.rectTransform;
        noseMarker.SetParent(blipRoot, false);
        nose.sprite = CircleSprite();
        nose.color = new Color(0.7f, 0.85f, 1f, 0.7f);
        nose.raycastTarget = false;
        noseMarker.sizeDelta = new Vector2(7f, 7f);
        noseMarker.anchorMin = noseMarker.anchorMax = noseMarker.pivot = new Vector2(0.5f, 0.5f);

        // 수송선(탈출 목표) 블립 — 시안, 크게
        extractionBlip = new GameObject("ExtractionBlip").AddComponent<Image>();
        extractionBlip.transform.SetParent(blipRoot, false);
        extractionBlip.sprite = CircleSprite();
        extractionBlip.color = new Color(0.3f, 0.9f, 0.95f, 1f);
        extractionBlip.raycastTarget = false;
        extractionBlip.gameObject.SetActive(false);
        var ert = extractionBlip.rectTransform;
        ert.sizeDelta = new Vector2(13f, 13f);
        ert.anchorMin = ert.anchorMax = ert.pivot = new Vector2(0.5f, 0.5f);

        // 플레이어 중심 마커
        var pm = new GameObject("PlayerMarker").AddComponent<Image>();
        pm.transform.SetParent(blipRoot, false);
        pm.sprite = CircleSprite();
        pm.color = new Color(0.4f, 0.95f, 0.5f, 1f);
        pm.raycastTarget = false;
        var pmrt = pm.rectTransform;
        pmrt.sizeDelta = new Vector2(11f, 11f);
        pmrt.anchorMin = pmrt.anchorMax = pmrt.pivot = new Vector2(0.5f, 0.5f);
        pmrt.anchoredPosition = Vector2.zero;
    }
}
