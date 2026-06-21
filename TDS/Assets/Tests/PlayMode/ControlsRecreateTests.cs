using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TDS.Core;

namespace TDS.Tests.PlayMode
{
    /// <summary>
    /// 입력 누수 수정 회귀: <see cref="ControlsManager.RecreateControls"/>가 컨트롤을 새 인스턴스로 교체해야 한다.
    /// (영속 ControlsManager는 리로드돼도 Awake가 안 돌아 옛 플레이어의 람다 구독이 누적됐던 버그.)
    /// </summary>
    public class ControlsRecreateTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameBootstrap.Instance != null)
                Object.DestroyImmediate(GameBootstrap.Instance.gameObject);
            GameServices.ResetForTests();
            yield return null;
        }

        [UnityTest]
        public IEnumerator RecreateControls_swaps_to_a_new_instance()
        {
            GameServices.ResetForTests();
            GameBootstrap.EnsureSystems();
            yield return null; // ControlsManager.Awake → controls 생성 + 서비스 등록

            Assert.IsNotNull(ControlsManager.instance, "ControlsManager 미존재");
            var before = ControlsManager.instance.controls;
            Assert.IsNotNull(before);

            // 서비스 인터페이스 경로로 호출(실제 PlayerSpawner가 쓰는 경로)
            var svc = GameServices.Registry.Resolve<IControlsService>();
            Assert.IsNotNull(svc, "IControlsService 미등록");
            svc.RecreateControls();

            var after = ControlsManager.instance.controls;
            Assert.IsNotNull(after);
            Assert.AreNotSame(before, after, "RecreateControls 후에도 같은 인스턴스 — 옛 구독이 안 버려짐");
        }
    }
}
