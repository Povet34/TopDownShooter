using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// 조준 시각화(TDS.Game). 시스템 커서가 숨겨져 있어(Player_AimController) 마우스/조준 위치가 안 보이는 문제 해결.
/// (1) 마우스 위치 스크린 크로스헤어, (2) 에임 타겟(마우스 월드 히트) 위치의 바닥 링 레티클.
/// 캔버스/도형을 코드로 생성 — 프리팹 의존 없음, 맵 단독 동작.
/// </summary>
[DisallowMultipleComponent]
public class AimReticle : MonoBehaviour
{
    [SerializeField] private Color color = new Color(0.6f, 1f, 0.7f, 0.9f);
    [SerializeField] private float worldRingRadius = 0.55f;

    private Player player;
    private RectTransform crosshair;
    private Transform worldReticle;

    private void Start()
    {
        BuildCrosshair();
        BuildWorldReticle();
    }

    private void Update()
    {
        // 마우스 위치 스크린 크로스헤어
        if (Mouse.current != null)
        {
            Vector2 m = Mouse.current.position.ReadValue();
            crosshair.position = new Vector3(m.x, m.y, 0f);
        }

        // 에임 타겟 월드 레티클
        EnsurePlayer();
        if (player != null && player.aim != null)
        {
            Vector3 p = player.aim.Aim().position;
            p.y = 0.05f; // 바닥 바로 위
            worldReticle.position = p;
            worldReticle.gameObject.SetActive(true);
        }
        else
        {
            worldReticle.gameObject.SetActive(false);
        }
    }

    private void EnsurePlayer()
    {
        if (player == null)
        {
            var go = GameObject.FindWithTag("Player");
            if (go != null) player = go.GetComponent<Player>();
        }
    }

    // ---------- 스크린 크로스헤어 ----------

    private void BuildCrosshair()
    {
        var canvasGo = new GameObject("Aim_Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        var rootGo = new GameObject("Crosshair");
        rootGo.transform.SetParent(canvasGo.transform, false);
        crosshair = rootGo.AddComponent<RectTransform>();

        const float gap = 7f, len = 9f, thick = 2f, dot = 3f;
        Tick(rootGo.transform, "Top", new Vector2(0f, gap + len * 0.5f), new Vector2(thick, len));
        Tick(rootGo.transform, "Bottom", new Vector2(0f, -(gap + len * 0.5f)), new Vector2(thick, len));
        Tick(rootGo.transform, "Left", new Vector2(-(gap + len * 0.5f), 0f), new Vector2(len, thick));
        Tick(rootGo.transform, "Right", new Vector2(gap + len * 0.5f, 0f), new Vector2(len, thick));
        Tick(rootGo.transform, "Dot", Vector2.zero, new Vector2(dot, dot));
    }

    private void Tick(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    // ---------- 월드 바닥 레티클(링) ----------

    private void BuildWorldReticle()
    {
        var go = new GameObject("Aim_WorldReticle");
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false; // 로컬 → transform로 위치
        lr.loop = true;
        lr.widthMultiplier = 0.05f;
        lr.numCapVertices = 2;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = color;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        const int seg = 32;
        lr.positionCount = seg;
        for (int i = 0; i < seg; i++)
        {
            float a = (float)i / seg * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * worldRingRadius, 0f, Mathf.Sin(a) * worldRingRadius));
        }
        worldReticle = go.transform;
    }
}
