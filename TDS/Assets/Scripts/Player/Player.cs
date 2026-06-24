using UnityEngine;

public class Player : MonoBehaviour
{
    public Transform playerBody;

    public PlayerControls controls { get; private set; }
    public Player_AimController aim { get; private set; }
    public Player_Movement movement { get; private set; }
    public Player_WeaponController weapon { get; private set; }
    public Player_WeaponVisuals weaponVisuals { get; private set; }
    public Player_Interaction interaction { get; private set; }
    public Player_Health health { get; private set; }
    public Ragdoll ragdoll { get; private set; }

    public Animator anim { get; private set; }
    public Player_SoundFX sound { get; private set; }

    public bool controlsEnabled { get; private set; }

    private void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        ragdoll = GetComponent<Ragdoll>();
        health = GetComponent<Player_Health>();
        aim = GetComponent<Player_AimController>();
        movement = GetComponent<Player_Movement>();
        weapon = GetComponent<Player_WeaponController>();
        weaponVisuals = GetComponent<Player_WeaponVisuals>();
        interaction = GetComponent<Player_Interaction>();
        sound = GetComponent<Player_SoundFX>();
        controls = ControlsManager.instance.controls;

        // 탑다운: 플레이어 CharacterController가 적 몸(래그돌 콜라이더)을 타고 위로 솟구치거나 올라타지
        // 않도록 Player↔Enemy 레이어 충돌을 무시한다. 총알(Bullet 레이어)·근접(오버랩 판정)엔 영향 없음.
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
            Physics.IgnoreLayerCollision(gameObject.layer, enemyLayer, true);
    }


    private void OnEnable()
    {
        controls.Enable();
        controls.Character.UIMissionToolTipSwitch.performed += ctx => { if (UI.instance != null) UI.instance.inGameUI.SwitchMissionTooltip(); };
        controls.Character.UIPause.performed += ctx => { if (UI.instance != null) UI.instance.PauseSwitch(); };
    }
    private void OnDisable()
    {
        controls.Disable();
    }

    public void SetControlsEnabledTo(bool enabled)
    {
        controlsEnabled = enabled;
        ragdoll.CollidersActive(enabled);
        aim.EnableAimLaer(enabled);
    }
}
