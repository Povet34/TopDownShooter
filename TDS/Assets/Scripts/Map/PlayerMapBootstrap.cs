using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 맵 씬에서 스폰된 플레이어를 전투 가능 상태로 배선한다(빈 맵에서도 사격 가능).
/// UI/GameManager 흐름 없이: 컨트롤 활성화 + 기본 무기 부여. 태그 "Player"로 한 번만 연결.
/// (Phase 0.2 — UI/무기선택 분리. 향후 정식 게임 흐름이 생기면 대체.)
/// </summary>
public class PlayerMapBootstrap : MonoBehaviour
{
    [SerializeField] private List<Weapon_Data> defaultWeapons = new List<Weapon_Data>();

    private bool wired;

    private void Update()
    {
        if (wired)
            return;

        GameObject pgo = GameObject.FindWithTag("Player");
        if (pgo == null)
            return;

        Player player = pgo.GetComponent<Player>();
        if (player == null || player.weapon == null)
            return;

        wired = true;

        player.SetControlsEnabledTo(true);

        // 도보 컨트롤 스킴 확정 — Car/UI 액션맵을 끈다. 안 그러면 Player.OnEnable의 controls.Enable()이
        // 모든 맵을 켠 채라 Car 맵도 활성 → F가 Character.Interaction(탑승) + Car.CarExit(하차)에 동시
        // 바인딩돼 한 번 눌러 보드 직후 즉시 하차한다(맵 씬엔 SwitchTo* 최초 호출이 없었음).
        if (ControlsManager.instance != null)
            ControlsManager.instance.SwitchToCharacterControls();

        if (defaultWeapons != null && defaultWeapons.Count > 0)
            player.weapon.SetDefaultWeapon(defaultWeapons);

        PlayerInventory.Ensure(pgo); // I키 그리드 인벤토리(처음부터 열람 가능)
        DevConsole.Ensure();         // 백틱(`) 개발 콘솔
    }
}
