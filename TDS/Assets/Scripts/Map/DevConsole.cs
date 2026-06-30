using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using TDS.Core;

/// <summary>
/// 개발용 콘솔(글루, TDS.Game). <b>백틱(`)</b>으로 채팅창 같은 입력창을 토글하고, 명령을 입력하면
/// 차 스폰·아이템 지급·텔레포트 등을 실행한다. 명령 등록/파싱은 순수 <see cref="ConsoleRegistry"/>
/// (dev-console 패키지). UI는 코드 생성(MapHUD 패턴). 열려 있는 동안 게임 입력은 막는다.
/// </summary>
[DisallowMultipleComponent]
public class DevConsole : MonoBehaviour
{
    private ConsoleRegistry registry;
    private bool open;
    private bool wasCar;
    private string input = "";
    private readonly List<string> log = new List<string>();

    private GameObject canvasGo;
    private RectTransform panel;
    private TextMeshProUGUI logText, inputText;

    public static DevConsole Ensure()
    {
        if (FindObjectOfType<DevConsole>() != null) return null;
        return new GameObject("DevConsole").AddComponent<DevConsole>();
    }

    private void Awake()
    {
        registry = new ConsoleRegistry();
        RegisterCommands();
        BuildUI();
        SetOpen(false);
        if (Keyboard.current != null) Keyboard.current.onTextInput += OnTextInput;
    }

    private void OnDestroy()
    {
        if (Keyboard.current != null) Keyboard.current.onTextInput -= OnTextInput;
        if (canvasGo != null) Destroy(canvasGo);
    }

    private void OnTextInput(char ch)
    {
        if (!open || ch == '`' || char.IsControl(ch)) return;
        input += ch;
        UpdateInputText();
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.backquoteKey.wasPressedThisFrame) { SetOpen(!open); return; }
        if (!open) return;

        if (kb.escapeKey.wasPressedThisFrame) { SetOpen(false); return; }
        if (kb.backspaceKey.wasPressedThisFrame && input.Length > 0) { input = input[..^1]; UpdateInputText(); }
        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Submit();
    }

    private void Submit()
    {
        string cmd = input.Trim();
        input = ""; UpdateInputText();
        if (cmd.Length == 0) return;
        Append("> " + cmd);
        string outp = registry.Execute(cmd);
        if (!string.IsNullOrEmpty(outp)) Append(outp);
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

        if (open) { input = ""; UpdateInputText(); if (log.Count == 0) Append("dev console — type 'help'"); }
    }

    private void Append(string line)
    {
        log.Add(line);
        while (log.Count > 16) log.RemoveAt(0);
        if (logText != null) logText.text = string.Join("\n", log);
    }

    private void UpdateInputText()
    {
        if (inputText != null) inputText.text = "> " + input + "<alpha=#80>_";
    }

    // ---------- 게임 명령 등록 ----------

    private void RegisterCommands()
    {
        registry.Register("help", "list commands", _ =>
        {
            var sb = new StringBuilder("commands:");
            foreach (var c in registry.Commands)
                sb.Append("\n  ").Append(c.Name).Append(string.IsNullOrEmpty(c.Help) ? "" : "  — " + c.Help);
            return sb.ToString();
        });

        registry.Register("tp", "tp <x> <z> — teleport player", a =>
        {
            var p = GameObject.FindWithTag("Player");
            if (p == null) return "no player";
            if (a.Length < 2 || !float.TryParse(a[0], out var x) || !float.TryParse(a[1], out var z))
                return "usage: tp <x> <z>";
            p.transform.position = new Vector3(x, 1f, z);
            return $"teleported to ({x}, {z})";
        });

        registry.Register("heal", "heal [amount] — heal player (default full)", a =>
        {
            var p = GameObject.FindWithTag("Player");
            var ph = p != null ? p.GetComponent<Player_Health>() : null;
            if (ph == null) return "no player health";
            int amt = (a.Length > 0 && int.TryParse(a[0], out var v)) ? v : ph.maxHealth;
            ph.currentHealth = Mathf.Min(ph.maxHealth, ph.currentHealth + amt);
            return $"healed -> {ph.currentHealth}/{ph.maxHealth}";
        });

        registry.Register("killall", "kill all enemies", _ =>
        {
            int n = 0;
            foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
                if (e.health != null && e.health.currentHealth > 0) { e.GetHit(999999); n++; }
            return $"killed {n} enemies";
        });

        registry.Register("give", "give [w] [h] — add a w×h inventory item", a =>
        {
            var p = GameObject.FindWithTag("Player");
            var inv = p != null ? PlayerInventory.Ensure(p) : null;
            if (inv == null) return "no inventory";
            int w = (a.Length > 0 && int.TryParse(a[0], out var ww)) ? Mathf.Clamp(ww, 1, 6) : 1;
            int h = (a.Length > 1 && int.TryParse(a[1], out var hh)) ? Mathf.Clamp(hh, 1, 6) : 1;
            return inv.TryStore(new InventoryItem("Item", w, h, $"{w}x{h}")) ? $"gave {w}x{h} item" : "inventory full";
        });

        registry.Register("spawn_loot", "spawn_loot [value] — drop loot at player", a =>
        {
            var p = GameObject.FindWithTag("Player");
            if (p == null) return "no player";
            int v = (a.Length > 0 && int.TryParse(a[0], out var vv)) ? vv : 1;
            LootPickup.CreatePrimitive(p.transform.position + p.transform.forward * 2f + Vector3.up * 0.5f, v);
            return $"spawned loot (value {v})";
        });

        registry.Register("spawn_car", "spawn a car next to the player", _ =>
        {
            var p = GameObject.FindWithTag("Player");
            var mg = FindObjectOfType<MapGenerator>();
            if (p == null || mg == null) return "no player/map";
            var car = mg.DebugSpawnCar(p.transform.position + p.transform.forward * 4f + Vector3.up * 0.6f);
            return car != null ? "spawned car" : "no car prefab in MapConfig";
        });

        registry.Register("stash", "show the persistent extraction stash", _ =>
        {
            var m = MetaStashController.Instance;
            if (m == null) return "no stash";
            var sb = new StringBuilder("stash: ").Append(m.Stash.Currency).Append(" salvage");
            foreach (var kv in m.Stash.Items) sb.Append("\n  ").Append(kv.Key).Append(" x").Append(kv.Value);
            return sb.ToString();
        });

        registry.Register("stash_clear", "wipe the stash (dev)", _ =>
        {
            var m = MetaStashController.Instance;
            if (m == null) return "no stash";
            m.ClearStash();
            return "stash cleared";
        });

        registry.Register("debuff", "apply a debuff: debuff <bleed|slow|stun> [seconds]", args =>
        {
            var p = GameObject.FindWithTag("Player");
            var st = p != null ? p.GetComponent<PlayerStatus>() : null;
            if (st == null) return "no player status";
            string which = args.Length > 0 ? args[0].ToLowerInvariant() : "";
            float dur = (args.Length > 1 && float.TryParse(args[1], out var d)) ? d : 5f;
            switch (which)
            {
                case "bleed": st.Apply(StatusKind.Bleed, dur, 5f); return $"bleeding {dur}s";
                case "slow": st.Apply(StatusKind.Slow, dur, 0.5f); return $"slowed {dur}s";
                case "stun": st.Apply(StatusKind.Stun, dur, 1f); return $"stunned {dur}s";
                default: return "usage: debuff <bleed|slow|stun> [seconds]";
            }
        });
    }

    // ---------- 코드로 UI 생성 ----------

    private void BuildUI()
    {
        canvasGo = new GameObject("DevConsole_Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200; // 최상단
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        panel = panelGo.AddComponent<RectTransform>();
        panel.anchorMin = new Vector2(0f, 1f); panel.anchorMax = new Vector2(1f, 1f); panel.pivot = new Vector2(0.5f, 1f);
        panel.sizeDelta = new Vector2(0f, 360f);
        panel.anchoredPosition = Vector2.zero;
        var bg = panelGo.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.03f, 0.05f, 0.92f);

        logText = MakeText(panel, "Log", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -12f),
            new Vector2(-32f, 300f), TextAlignmentOptions.TopLeft, 22f);
        inputText = MakeText(panel, "Input", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(16f, 12f),
            new Vector2(-32f, 40f), TextAlignmentOptions.BottomLeft, 26f);
        inputText.color = new Color(0.6f, 1f, 0.6f);
    }

    private TextMeshProUGUI MakeText(Transform parent, string name, Vector2 aMin, Vector2 aMax,
        Vector2 anchoredPos, Vector2 size, TextAlignmentOptions align, float fontSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = aMin;
        rt.anchoredPosition = anchoredPos; rt.sizeDelta = size;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = fontSize; t.alignment = align; t.color = Color.white; t.raycastTarget = false; t.text = "";
        t.enableWordWrapping = true;
        return t;
    }
}
