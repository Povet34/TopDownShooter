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
    }
}
