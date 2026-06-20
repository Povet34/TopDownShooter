using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TDS.Core;

public class ControlsManager : MonoBehaviour, IControlsService
{
    public static ControlsManager instance;
    public PlayerControls controls { get; private set; }
    private Player player;

    private void Awake()
    {
        instance = this;
        controls = new PlayerControls();
        GameServices.Registry.Register<IControlsService>(this);
    }


    private void Start()
    {
        // 단독 부트(플레이어/UI 없는 씬)에서도 안전하도록 guard.
        // 실제 "플레이어 준비 시 컨트롤 활성화" 재배선은 Phase 0.2(컴포지션 루트)에서.
        if (GameManager.instance != null)
            player = GameManager.instance.player;

        if (player != null && UI.instance != null)
            SwitchToCharacterControls();
    }

    public void SwitchToCharacterControls()
    {
        controls.Character.Enable();

        controls.Car.Disable();
        controls.UI.Disable();

        player.SetControlsEnabledTo(true);
        UI.instance.inGameUI.SwitchToCharcaterUI();
    }

    public void SwitchToUIControls()
    {
        controls.UI.Enable();

        controls.Car.Disable();
        controls.Character.Disable();
        player.SetControlsEnabledTo(false);
    }

    public void SwitchToCarControls()
    {
        controls.Car.Enable();

        controls.UI.Disable();
        controls.Character.Disable();

        player.SetControlsEnabledTo(false);
        UI.instance.inGameUI.SwitchToCarUI();
    }


}
