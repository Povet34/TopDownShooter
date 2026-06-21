using UnityEngine;

/// <summary>
/// FoV 비주얼 모드 선택 — 게임 시작 전 Inspector에서 고른다. Awake에서 선택된 버전만 동적으로 붙인다.
/// Off = 시야 끔(적 항상 보임), Realistic = CPU(사실적·느림), Fast = GPU(빠름·일부 샘플링 이슈).
/// </summary>
[RequireComponent(typeof(FieldOfView))]
public class FovController : MonoBehaviour
{
    public enum Mode { Off, Realistic, Fast }

    [Tooltip("FoV 모드 — 게임 시작 전에 선택. Off=끔, Realistic=CPU(사실적·느림), Fast=GPU(빠름)")]
    [SerializeField] private Mode mode = Mode.Off;

    public Mode CurrentMode => mode;

    private void Awake()
    {
        var fov = GetComponent<FieldOfView>();

        switch (mode)
        {
            case Mode.Off:
                if (fov != null) fov.enabled = false; // 적 숨김도 끔(항상 보임)
                break;

            case Mode.Realistic:
                if (fov != null) fov.enabled = true;
                if (GetComponent<VisionMaskCpu>() == null) gameObject.AddComponent<VisionMaskCpu>();
                break;

            case Mode.Fast:
                if (fov != null) fov.enabled = true;
                if (GetComponent<VisionMask>() == null) gameObject.AddComponent<VisionMask>();
                break;
        }
    }
}
