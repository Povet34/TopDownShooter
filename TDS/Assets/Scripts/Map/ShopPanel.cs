using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// 업그레이드 상점 패널(글루, TDS.Game). <b>U</b>키로 토글하고, 반출 스태시 통화로 영구 업그레이드를 산다.
/// 열린 동안 <b>숫자키(1..)</b>로 구매. 구매/적용/영속은 <see cref="StashUpgradesController"/>. 열려 있는
/// 동안 게임 입력은 막는다(DevConsole 패턴). UI는 코드 생성(MapHUD/콘솔과 동일).
/// </summary>
[DisallowMultipleComponent]
public class ShopPanel : MonoBehaviour
{
    private GameObject canvasGo;
    private RectTransform panel;
    private TextMeshProUGUI body;
    private bool open;
    private bool wasCar;

    public static ShopPanel Ensure()
    {
        if (FindObjectOfType<ShopPanel>() != null) return null;
        return new GameObject("ShopPanel").AddComponent<ShopPanel>();
    }

    private void Awake()
    {
        BuildUI();
        SetOpen(false);
    }

    private void OnDestroy() { if (canvasGo != null) Destroy(canvasGo); }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.uKey.wasPressedThisFrame) { SetOpen(!open); return; }
        if (!open) return;
        if (kb.escapeKey.wasPressedThisFrame) { SetOpen(false); return; }

        var defs = StashUpgradesController.Ensure().Upgrades.Defs;
        for (int i = 0; i < defs.Count && i < 9; i++)
        {
            if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame)
            {
                StashUpgradesController.Ensure().Buy(defs[i].Id);
                Refresh();
            }
        }
    }

    private void SetOpen(bool value)
    {
        open = value;
        if (panel != null) panel.gameObject.SetActive(open);

        var cm = ControlsManager.instance;
        if (cm != null && cm.controls != null)
        {
            if (open)
            {
                wasCar = cm.controls.Car.enabled;
                cm.controls.Character.Disable();
                cm.controls.Car.Disable();
            }
            else if (wasCar) cm.SwitchToCarControls();
            else cm.SwitchToCharacterControls();
        }

        if (open) Refresh();
    }

    public void Refresh()
    {
        if (body == null) return;
        var c = StashUpgradesController.Ensure();
        int salvage = MetaStashController.Instance != null ? MetaStashController.Instance.Stash.Currency : 0;

        var sb = new StringBuilder();
        sb.Append($"<size=130%>UPGRADES</size>     <color=#9CD2FF>{salvage} salvage</color>\n");
        sb.Append("<size=68%>press the number to buy · U / Esc to close</size>\n\n");

        var defs = c.Upgrades.Defs;
        for (int i = 0; i < defs.Count; i++)
        {
            var d = defs[i];
            bool maxed = c.Upgrades.IsMaxed(d.Id);
            int cost = c.Upgrades.CostOf(d.Id);
            string costStr = maxed
                ? "<color=#7CFC9A>MAX</color>"
                : (salvage >= cost ? $"<color=#9CD2FF>{cost} salvage</color>" : $"<color=#E5847C>{cost} salvage</color>");
            sb.Append($"<b>[{i + 1}]  {d.Name}</b>    Lv {c.Upgrades.LevelOf(d.Id)}/{d.MaxLevel}    {costStr}\n");
            sb.Append($"<size=68%>       +{d.PerLevel} {d.Unit} per level</size>\n\n");
        }
        body.text = sb.ToString();
    }

    private void BuildUI()
    {
        canvasGo = new GameObject("Shop_Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 190;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        panel = panelGo.AddComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.5f, 0.5f); panel.anchorMax = new Vector2(0.5f, 0.5f); panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(640f, 540f);
        panel.anchoredPosition = new Vector2(0f, 40f);
        var bg = panelGo.AddComponent<Image>();
        bg.color = new Color(0.03f, 0.05f, 0.08f, 0.94f);

        var go = new GameObject("Body");
        go.transform.SetParent(panel, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(30f, 26f); rt.offsetMax = new Vector2(-30f, -26f);
        body = go.AddComponent<TextMeshProUGUI>();
        body.fontSize = 30f; body.alignment = TextAlignmentOptions.TopLeft; body.color = Color.white;
        body.raycastTarget = false; body.enableWordWrapping = true;
    }
}
