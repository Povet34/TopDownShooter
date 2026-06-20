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
            // 사격 테스트가 남긴 총알(풀에서 꺼내져 parent=null 루트) 정리 — 다음 테스트 오염 방지
            foreach (var b in Object.FindObjectsByType<Bullet>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(b.gameObject);
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
            Assert.IsNotNull(player.interaction, "interaction 미배선");
            Assert.IsNotNull(player.controls, "controls 미배선(ControlsManager에서)");
        }

        [UnityTest]
        public IEnumerator Player_controls_have_expected_actions()
        {
            GameServices.ResetForTests();
            var player = SpawnPlayer();
            yield return null;

            var c = player.controls;
            Assert.IsNotNull(c, "controls null");
            Assert.IsNotNull(c.Character.Movement, "Movement 액션 없음");
            Assert.IsNotNull(c.Character.Fire, "Fire 액션 없음");
            Assert.IsNotNull(c.Character.Reload, "Reload 액션 없음");
            Assert.IsNotNull(c.Character.Aim, "Aim 액션 없음");
            Assert.IsNotNull(c.Character.Run, "Run 액션 없음");
            Assert.IsNotNull(c.Character.Interaction, "Interaction 액션 없음");
        }

        [UnityTest]
        public IEnumerator Player_switches_to_second_weapon_slot()
        {
            GameServices.ResetForTests();
            var player = SpawnPlayer();
            yield return null;

            var wc = player.weapon;
            var defaults = typeof(Player_WeaponController)
                .GetField("defaultWeaponData", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(wc) as List<Weapon_Data>;
            Assert.IsTrue(defaults != null && defaults.Count >= 2, "기본 무기 2개 이상 필요");
            wc.SetDefaultWeapon(defaults);
            for (int i = 0; i < 3; i++) yield return null;

            var first = wc.CurrentWeapon().weaponType;
            typeof(Player_WeaponController)
                .GetMethod("EquipWeapon", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(wc, new object[] { 1 });
            yield return null;

            var second = wc.CurrentWeapon().weaponType;
            Assert.AreNotEqual(first, second, "2번 슬롯으로 무기 전환 안됨");
        }

        [UnityTest]
        public IEnumerator Player_takes_damage_reduces_health()
        {
            GameServices.ResetForTests();
            var player = SpawnPlayer();
            yield return null;

            var hp = player.health;
            Assert.Greater(hp.maxHealth, 1, "maxHealth 미설정(프리팹)");
            int before = hp.currentHealth;

            hp.ReduceHealth(1);
            yield return null;

            Assert.AreEqual(before - 1, hp.currentHealth, "체력이 줄지 않음");
            Assert.IsFalse(hp.isDead, "이 피해로 죽으면 안됨");
        }

        [UnityTest]
        public IEnumerator Player_movement_input_moves_player()
        {
            GameServices.ResetForTests();
            var player = SpawnPlayer();
            yield return null;

            var mv = player.movement;
            var moveField = typeof(Player_Movement).GetField("<moveInput>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(moveField, "moveInput 백킹필드 못찾음");

            float startX = player.transform.position.x;
            for (int i = 0; i < 12; i++)
            {
                moveField.SetValue(mv, new Vector2(1f, 0f)); // 입력 홀드 시뮬
                yield return null;
            }

            Assert.Greater(player.transform.position.x, startX + 0.05f, "입력해도 이동하지 않음");
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
