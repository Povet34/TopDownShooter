using UnityEngine;

/// <summary>
/// 플레이어 FoV 구동: 시야 방향을 조준 방향으로 매 프레임 세팅하고, 발사 시 시야를 잠깐 확대(Reveal).
/// 가시성/적 숨김 로직은 <see cref="FieldOfView"/>가 처리.
/// </summary>
[RequireComponent(typeof(FieldOfView))]
public class Player_FieldOfView : MonoBehaviour
{
    private Player player;
    private FieldOfView fov;
    private PlayerControls controls;

    private void Awake()
    {
        player = GetComponent<Player>();
        fov = GetComponent<FieldOfView>();
    }

    private void Start()
    {
        controls = player != null ? player.controls : null;
        if (controls != null)
            controls.Character.Fire.performed += OnFire;
    }

    private void OnDestroy()
    {
        if (controls != null)
            controls.Character.Fire.performed -= OnFire;
    }

    private void OnFire(UnityEngine.InputSystem.InputAction.CallbackContext _) => fov.Reveal();

    private void Update()
    {
        if (player == null || player.aim == null) return;

        Vector3 dir = player.aim.Aim().position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 1e-4f)
            fov.OverrideForward = dir;
    }
}
