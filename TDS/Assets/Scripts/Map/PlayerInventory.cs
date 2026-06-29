using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using TDS.Core;

/// <summary>
/// 플레이어 그리드 인벤토리(글루, TDS.Game). 순수 <see cref="InventoryGrid"/>를 들고, <b>I 키</b>로 패널을
/// 토글하며, 루트 픽업을 <see cref="TryStore"/>로 수납한다(타르코프/디아블로식 멀티셀). UI는 코드로 생성
/// (MapHUD 패턴) — 프리팹/씬/기존 UI 싱글톤 의존 없음. 캔버스는 별도 루트 오브젝트(플레이어 스케일 영향 차단).
/// </summary>
[DisallowMultipleComponent]
public class PlayerInventory : MonoBehaviour
{
    [SerializeField] private int columns = 10;
    [SerializeField] private int rows = 6;

    public InventoryGrid Grid { get; private set; }
    public bool IsOpen { get; private set; }
    public bool IsFull => Grid != null && Grid.FreeCellCount == 0;

    // UI
    private const float CellPx = 56f;
    private const float Pad = 16f;
    private const float TitleH = 44f;
    private GameObject canvasGo;
    private RectTransform panel;
    private RectTransform cellsRoot;
    private TextMeshProUGUI title;
    private readonly List<GameObject> itemViews = new List<GameObject>();

    // 드래그(자유 배치) 상태
    private PlacedItem dragItem;        // 드래그 중인 아이템(null=없음)
    private bool dragRotated;
    private GameObject dragGhost;

    /// <summary>플레이어에 PlayerInventory 보장(없으면 추가). 프리팹 수정 없이 런타임 부착.</summary>
    public static PlayerInventory Ensure(GameObject player)
    {
        if (player == null) return null;
        return player.GetComponent<PlayerInventory>() ?? player.AddComponent<PlayerInventory>();
    }

    private void Awake()
    {
        Grid = new InventoryGrid(columns, rows);
        BuildUI();
        SetOpen(false);
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.iKey.wasPressedThisFrame)
            SetOpen(!IsOpen);

        if (IsOpen) UpdateDrag(kb);
        else if (dragItem != null) EndDrag(); // 패널 닫히면 드래그 취소
    }

    // ---------- 자유 배치: 드래그 & 드롭 + R 회전 ----------

    private void UpdateDrag(Keyboard kb)
    {
        var mouse = Mouse.current;
        if (mouse == null) return;
        Vector2 mp = mouse.position.ReadValue();

        if (dragItem == null)
        {
            if (mouse.leftButton.wasPressedThisFrame && ScreenToCell(mp, out int cx, out int cy)
                && Grid.InBounds(cx, cy))
            {
                var p = Grid.ItemAt(cx, cy);
                if (p != null) BeginDrag(p);
            }
            return;
        }

        // 회전(정사각형이 아닌 것만)
        if (kb != null && kb.rKey.wasPressedThisFrame && dragItem.Item.Width != dragItem.Item.Height)
            dragRotated = !dragRotated;

        int tw = dragRotated ? dragItem.Item.Height : dragItem.Item.Width;
        int th = dragRotated ? dragItem.Item.Width : dragItem.Item.Height;
        ScreenToCell(mp, out int curX, out int curY);
        int ax = curX - (tw - 1) / 2; // 커서를 아이템 중심에 맞춤
        int ay = curY - (th - 1) / 2;
        bool valid = Grid.CanPlaceIgnoring(dragItem.Item, ax, ay, dragRotated, dragItem);

        UpdateGhost(ax, ay, tw, th, valid);

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            if (valid)
                Grid.TryMove(dragItem, ax, ay, dragRotated);
            else if (Grid.InBounds(curX, curY)) // 빈자리 아니면, 커서 아래 아이템과 스왑 시도
            {
                var b = Grid.ItemAt(curX, curY);
                if (b != null && b != dragItem) Grid.TrySwap(dragItem, b);
            }
            EndDrag();
            Redraw();
        }
    }

    private void BeginDrag(PlacedItem p)
    {
        dragItem = p;
        dragRotated = p.Rotated;
        dragGhost = NewRect("DragGhost", cellsRoot, p.X, p.Y, p.Width, p.Height);
        dragGhost.transform.SetAsLastSibling();
        var label = MakeText(dragGhost.GetComponent<RectTransform>(), "GhostLabel",
            Vector2.zero, Vector2.one, Vector2.zero, TextAlignmentOptions.Center, 18f);
        label.text = p.Item.DisplayName;
    }

    private void UpdateGhost(int ax, int ay, int tw, int th, bool valid)
    {
        if (dragGhost == null) return;
        var rt = dragGhost.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(ax * CellPx + 1f, -(ay * CellPx + 1f));
        rt.sizeDelta = new Vector2(tw * CellPx - 2f, th * CellPx - 2f);
        dragGhost.GetComponent<Image>().color = valid
            ? new Color(0.3f, 0.9f, 0.4f, 0.55f)
            : new Color(0.9f, 0.3f, 0.3f, 0.55f);
    }

    private void EndDrag()
    {
        if (dragGhost != null) Destroy(dragGhost);
        dragGhost = null;
        dragItem = null;
    }

    // 화면 좌표 → 셀 좌표(좌상단 원점, x→오른쪽 / y→아래). 범위 밖 셀도 그대로 반환(검사는 CanPlace가).
    private bool ScreenToCell(Vector2 screen, out int cx, out int cy)
    {
        cx = cy = 0;
        if (cellsRoot == null) return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(cellsRoot, screen, null, out var local))
            return false;
        cx = Mathf.FloorToInt(local.x / CellPx);
        cy = Mathf.FloorToInt(-local.y / CellPx);
        return true;
    }

    /// <summary>테스트/외부용: 놓인 아이템을 이동(그리드 TryMove + 열려있으면 다시 그림).</summary>
    public bool MoveItem(PlacedItem placed, int x, int y, bool rotated)
    {
        bool ok = Grid != null && Grid.TryMove(placed, x, y, rotated);
        if (ok && IsOpen) Redraw();
        return ok;
    }

    private void OnDestroy()
    {
        if (canvasGo != null) Destroy(canvasGo);
    }

    /// <summary>아이템 수납 시도(자동 배치, 필요시 회전). 성공 true. 열려 있으면 다시 그린다.</summary>
    public bool TryStore(InventoryItem item)
    {
        if (Grid == null || item == null) return false;
        bool ok = Grid.TryAutoPlace(item, out _);
        if (ok && IsOpen) Redraw();
        return ok;
    }

    /// <summary>루트 value → 인벤토리 아이템(풋프린트 매핑). 같은 종류는 같은 id/색.</summary>
    public static InventoryItem LootItem(int value)
    {
        if (value >= 3) return new InventoryItem("Salvage", 2, 2, "Salvage");
        if (value == 2) return new InventoryItem("Parts", 2, 1, "Parts");
        return new InventoryItem("Scrap", 1, 1, "Scrap");
    }

    public void Toggle() => SetOpen(!IsOpen);

    private void SetOpen(bool value)
    {
        IsOpen = value;
        if (panel != null) panel.gameObject.SetActive(IsOpen);
        if (IsOpen) Redraw();
        else if (dragItem != null) EndDrag();
        SuppressFire(IsOpen);
    }

    // 패널 열린 동안 사격 억제 — 드래그용 좌클릭이 무기 발사(Character.Fire)도 되는 것 방지.
    // 도보일 때만(Character 맵 활성). 차량 탑승 중엔 안 건드린다.
    private void SuppressFire(bool suppress)
    {
        var cm = ControlsManager.instance;
        if (cm == null || cm.controls == null) return;
        var character = cm.controls.Character;
        if (!character.enabled) return;
        if (suppress) character.Fire.Disable();
        else character.Fire.Enable();
    }

    // ---------- 코드로 UI 생성 ----------

    private void BuildUI()
    {
        canvasGo = new GameObject("Inventory_Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110; // HUD(100) 위
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        float gridW = columns * CellPx;
        float gridH = rows * CellPx;

        // 중앙 패널
        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        panel = panelGo.AddComponent<RectTransform>();
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(gridW + Pad * 2f, gridH + Pad * 2f + TitleH);
        var pbg = panelGo.AddComponent<Image>();
        pbg.color = new Color(0.05f, 0.06f, 0.08f, 0.92f);

        // 제목
        title = MakeText(panel, "Title", new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -TitleH * 0.5f), TextAlignmentOptions.Center, 26f);
        title.fontStyle = FontStyles.Bold;

        // 셀 루트(좌상단 기준)
        var cr = new GameObject("Cells");
        cr.transform.SetParent(panel, false);
        cellsRoot = cr.AddComponent<RectTransform>();
        cellsRoot.anchorMin = cellsRoot.anchorMax = cellsRoot.pivot = new Vector2(0f, 1f);
        cellsRoot.anchoredPosition = new Vector2(Pad, -(TitleH + Pad * 0.5f));
        cellsRoot.sizeDelta = new Vector2(gridW, gridH);

        // 셀 배경(2px 간격 → 격자선)
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
            {
                var cell = NewRect("Cell", cellsRoot, x, y, 1, 1);
                cell.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);
            }
    }

    private void Redraw()
    {
        if (Grid == null) return;
        title.text = $"INVENTORY    <size=70%>{Grid.FreeCellCount}/{columns * rows} free    drag to move · R rotate · I close</size>";

        foreach (var v in itemViews) Destroy(v);
        itemViews.Clear();

        foreach (var placed in Grid.Items)
        {
            var view = NewRect("Item", cellsRoot, placed.X, placed.Y, placed.Width, placed.Height);
            view.GetComponent<Image>().color = ColorFor(placed.Item.Id);
            var label = MakeText(view.GetComponent<RectTransform>(), "Label",
                Vector2.zero, Vector2.one, Vector2.zero, TextAlignmentOptions.Center, 18f);
            label.text = placed.Item.DisplayName;
            itemViews.Add(view);
        }
    }

    // 셀 좌표(좌상단 원점, x→오른쪽 / y→아래) → UI 사각형.
    private GameObject NewRect(string name, Transform parent, int x, int y, int w, int h)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x * CellPx + 1f, -(y * CellPx + 1f));
        rt.sizeDelta = new Vector2(w * CellPx - 2f, h * CellPx - 2f);
        var img = go.AddComponent<Image>();
        img.color = Color.white;
        img.raycastTarget = false;
        return go;
    }

    private static Color ColorFor(string id)
    {
        if (id == "Salvage") return new Color(0.95f, 0.75f, 0.2f, 0.95f); // 금
        if (id == "Parts") return new Color(0.3f, 0.6f, 0.9f, 0.95f);     // 청
        if (id == "Scrap") return new Color(0.6f, 0.62f, 0.65f, 0.95f);   // 회
        int hash = id != null ? id.GetHashCode() : 0;
        return new Color((hash & 0xFF) / 255f, ((hash >> 8) & 0xFF) / 255f, ((hash >> 16) & 0xFF) / 255f, 0.95f);
    }

    private TextMeshProUGUI MakeText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, TextAlignmentOptions align, float fontSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.anchoredPosition = anchoredPos;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.text = "";
        return t;
    }
}
