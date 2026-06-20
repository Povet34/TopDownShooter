using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TDS.Core;

namespace TDS.Tests.PlayMode
{
    /// <summary>
    /// Player 통합 테스트 (TDS.Game asmdef 참조로 게임 타입 직접 사용 가능).
    /// 스폰 → 서브시스템 배선 확인 → 무기 장착 → 실제 사격(총알 스폰)까지.
    /// </summary>
    public class PlayerCombatTests
    {
        private Player SpawnPlayer()
        {
            GameBootstrap.EnsureSystems(); // ControlsManager 등 보장
            var prefab = Resources.Load<GameObject>("Player");
            Assert.IsNotNull(prefab, "Resources/Player 로드 실패");
            var go = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            return go.GetComponent<Player>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var p in Object.FindObjectsByType<Player>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(p.gameObject);
            if (GameBootstrap.Instance != null)
                Object.DestroyImmediate(GameBootstrap.Instance.gameObject);
            GameServices.ResetForTests();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Player_spawns_with_subsystems_and_controls_wired()
        {
            GameServices.ResetForTests();
            var player = SpawnPlayer();
            yield return null;

            Assert.IsNotNull(player.movement, "movement 미배선");
            Assert.IsNotNull(player.weapon, "weapon 미배선");
            Assert.IsNotNull(player.aim, "aim 미배선");
            Assert.IsNotNull(player.health, "health 미배선");
            Assert.IsNotNull(player.controls, "controls 미배선(ControlsManager에서)");
        }

        [UnityTest]
        public IEnumerator Player_equips_default_weapon_and_fires_a_bullet()
        {
            GameServices.ResetForTests();
            var player = SpawnPlayer();
            yield return null;

            // 프리팹에 직렬화된 기본 무기로 장착
            var wc = player.weapon;
            var dwdField = typeof(Player_WeaponController).GetField("defaultWeaponData", BindingFlags.NonPublic | BindingFlags.Instance);
            var defaults = dwdField.GetValue(wc) as List<Weapon_Data>;
            Assert.IsTrue(defaults != null && defaults.Count > 0, "프리팹 기본 무기 없음");
            wc.SetDefaultWeapon(defaults);

            // 장착/무기 모델 활성화 대기
            for (int i = 0; i < 5; i++) yield return null;

            Assert.IsNotNull(wc.CurrentWeapon(), "currentWeapon null");
            wc.SetWeaponReady(true); // 평소엔 장착 애니메이션 이벤트가 설정 — 테스트선 강제

            // 실제 사격: FireSingleBullet 호출 → 총알 스폰 확인
            int before = Object.FindObjectsByType<Bullet>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            var fire = typeof(Player_WeaponController).GetMethod("FireSingleBullet", BindingFlags.NonPublic | BindingFlags.Instance);
            fire.Invoke(wc, null);
            int after = Object.FindObjectsByType<Bullet>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

            Assert.Greater(after, before, "사격 시 총알이 스폰되지 않음");
        }
    }
}
