using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// F10: 게임뷰 전체화면 토글. 에디터에선 Game View 창을 최대화/복원, 빌드에선 Screen.fullScreen 토글.
/// </summary>
public class FullscreenToggle : MonoBehaviour
{
    [SerializeField] private Key toggleKey = Key.F10;

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb[toggleKey].wasPressedThisFrame)
            return;

#if UNITY_EDITOR
        ToggleEditorGameViewMaximized();
#else
        Screen.fullScreen = !Screen.fullScreen;
#endif
    }

#if UNITY_EDITOR
    private static void ToggleEditorGameViewMaximized()
    {
        var gameViewType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
        if (gameViewType == null)
            return;

        var gv = EditorWindow.GetWindow(gameViewType, false, null, false);
        if (gv != null)
            gv.maximized = !gv.maximized;
    }
#endif
}
