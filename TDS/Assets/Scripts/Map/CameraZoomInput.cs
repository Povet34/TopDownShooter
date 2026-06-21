using UnityEngine;
using UnityEngine.InputSystem;
using TDS.Core;

/// <summary>
/// 마우스 휠 입력을 읽어 <see cref="CameraFollow"/>에 전달(글루). TDS.Core는 입력 비의존이라 입력 읽기는 여기(TDS.Game)서.
/// 카메라(또는 맵 씬 오브젝트)에 붙인다.
/// </summary>
[DisallowMultipleComponent]
public class CameraZoomInput : MonoBehaviour
{
    private CameraFollow cam;

    private void Update()
    {
        if (cam == null)
        {
            cam = GetComponent<CameraFollow>();
            if (cam == null) cam = Object.FindFirstObjectByType<CameraFollow>();
        }
        if (cam == null || Mouse.current == null)
            return;

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
            cam.AddScroll(scroll);
    }
}
