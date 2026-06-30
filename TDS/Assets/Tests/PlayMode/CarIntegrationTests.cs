using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TDS.Core;

namespace TDS.Tests.PlayMode
{
    /// <summary>
    /// 차량 재통합 디커플링 회귀 가드. 맵 컨텍스트(UI/GameManager 싱글톤 없음)에서
    /// 컨트롤 전환(Car↔Character)이 태그로 플레이어를 해석해 NullRef 없이 동작하고
    /// controlsEnabled를 올바르게 토글하는지 검증. (탑승/하차 전체 흐름은 in-game 검증.)
    /// </summary>
    public class CarIntegrationTests
    {
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
        public IEnumerator Controls_switch_car_and_character_without_singletons()
        {
            GameServices.ResetForTests();
            GameBootstrap.EnsureSystems(); // ControlsManager 보장
            var prefab = Resources.Load<GameObject>("Player");
            Assert.IsNotNull(prefab, "Resources/Player 로드 실패");
            var player = Object.Instantiate(prefab).GetComponent<Player>(); // 프리팹 태그 "Player"
            yield return null;

            var ctrls = Object.FindObjectOfType<ControlsManager>();
            Assert.IsNotNull(ctrls, "ControlsManager(Systems) 없음");

            // 맵엔 UI/GameManager 싱글톤이 없음 → 태그로 플레이어 해석 + UI 가드로 NullRef 0이어야.
            Assert.DoesNotThrow(() => ctrls.SwitchToCarControls(), "차량 컨트롤 전환에서 예외");
            yield return null;
            Assert.IsFalse(player.controlsEnabled, "차량 전환 시 캐릭터 컨트롤이 꺼져야");

            Assert.DoesNotThrow(() => ctrls.SwitchToCharacterControls(), "캐릭터 컨트롤 전환에서 예외");
            yield return null;
            Assert.IsTrue(player.controlsEnabled, "캐릭터 전환 시 켜져야");
        }

        /// <summary>
        /// F 키가 Character.Interaction(탑승)·Car.CarExit(하차)에 동시 바인딩돼 있어, 두 액션맵이 같이
        /// 켜져 있으면 한 번 눌러 보드 직후 즉시 하차한다. 도보/탑승 스킴은 상호 배타여야(한쪽만 활성).
        /// </summary>
        [UnityTest]
        public IEnumerator Control_schemes_are_mutually_exclusive_so_shared_F_key_is_safe()
        {
            GameServices.ResetForTests();
            GameBootstrap.EnsureSystems();
            var prefab = Resources.Load<GameObject>("Player");
            var player = Object.Instantiate(prefab).GetComponent<Player>();
            yield return null;

            var ctrls = Object.FindObjectOfType<ControlsManager>();
            var c = ctrls.controls;

            // 버그 사전조건 재현: Player.OnEnable의 controls.Enable()처럼 모든 맵 활성.
            c.Enable();
            Assert.IsTrue(c.Character.enabled && c.Car.enabled, "사전조건: 모든 맵 활성");

            // 도보 스킴 → Car 맵이 꺼져야 F가 CarExit를 같이 울리지 않는다.
            ctrls.SwitchToCharacterControls();
            yield return null;
            Assert.IsTrue(c.Character.enabled, "도보: Character 활성");
            Assert.IsFalse(c.Car.enabled, "도보: Car 비활성이어야(아니면 F가 보드+즉시하차)");

            // 탑승 스킴 → Character 맵이 꺼져야.
            ctrls.SwitchToCarControls();
            yield return null;
            Assert.IsTrue(c.Car.enabled, "탑승: Car 활성");
            Assert.IsFalse(c.Character.enabled, "탑승: Character 비활성이어야");
        }

        /// <summary>탑승 중(플레이어가 차에 parent됨)엔 플레이어가 받을 데미지가 차로 돌아간다 — 몬스터가 차를 공격.</summary>
        [UnityTest]
        public IEnumerator Driver_damage_redirects_to_the_car()
        {
            GameServices.ResetForTests();
            GameBootstrap.EnsureSystems();
            var player = Object.Instantiate(Resources.Load<GameObject>("Player")).GetComponent<Player>();
            yield return null;
            var ph = player.GetComponent<Player_Health>();
            int playerBefore = ph.currentHealth;

            var carGo = new GameObject("Car");
            var carHealth = carGo.AddComponent<Car_HealthController>();
            yield return null; // Start: currentHealth=maxHealth(0)
            carHealth.maxHealth = 500; carHealth.currentHealth = 500;
            player.transform.SetParent(carGo.transform); // 탑승 모사

            ph.ReduceHealth(40);
            yield return null;

            Assert.AreEqual(460, carHealth.currentHealth, "차가 데미지를 안 받음(리다이렉트 실패)");
            Assert.AreEqual(playerBefore, ph.currentHealth, "운전자가 데미지를 받음(차로 안 돌아감)");

            Object.DestroyImmediate(carGo); // 자식 플레이어도 함께 정리
        }
    }
}
