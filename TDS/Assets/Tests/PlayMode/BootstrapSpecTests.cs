using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using TDS.Core;

namespace TDS.Tests.PlayMode
{
    /// <summary>
    /// Phase 0 통합 명세 (PlayMode). 부트스트랩 + 서비스 등록 + 영속성 + 씬 단독 진입을 검증한다.
    /// </summary>
    public class BootstrapSpecTests
    {
        private interface IPing { int Value { get; } }
        private class Ping : IPing { public int Value => 42; }

        // 각 테스트 후 영속 Systems와 정적 레지스트리를 정리(테스트 간 오염 방지).
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameBootstrap.Instance != null)
                Object.DestroyImmediate(GameBootstrap.Instance.gameObject);
            GameServices.ResetForTests();
            yield return null;
        }

        // PlayMode 하니스 + ServiceRegistry 런타임 동작.
        [UnityTest]
        public IEnumerator ServiceRegistry_resolves_across_a_frame()
        {
            var reg = new ServiceRegistry();
            reg.Register<IPing>(new Ping());
            yield return null;
            Assert.IsTrue(reg.TryResolve<IPing>(out var p));
            Assert.AreEqual(42, p.Value);
        }

        // 0.1a: EnsureSystems가 Systems 프리팹을 띄우면 담긴 전역 매니저가 자기 등록한다.
        [UnityTest]
        public IEnumerator Bootstrap_registers_global_services()
        {
            GameServices.ResetForTests();
            var systems = GameBootstrap.EnsureSystems();
            Assert.IsNotNull(systems, "Resources/Systems 프리팹 로드 실패");
            yield return null;

            Assert.IsTrue(GameServices.Registry.IsRegistered<IClockService>(), "IClockService 미등록");
            Assert.IsTrue(GameServices.Registry.IsRegistered<IMissionService>(), "IMissionService 미등록");
            Assert.IsTrue(GameServices.Registry.IsRegistered<IGameStateService>(), "IGameStateService 미등록");
            Assert.IsTrue(GameServices.Registry.IsRegistered<IObjectPoolService>(), "IObjectPoolService 미등록");
            Assert.IsTrue(GameServices.Registry.IsRegistered<IControlsService>(), "IControlsService 미등록");
        }

        [UnityTest]
        public IEnumerator EnsureSystems_is_idempotent()
        {
            GameServices.ResetForTests();
            var first = GameBootstrap.EnsureSystems();
            var second = GameBootstrap.EnsureSystems();
            yield return null;
            Assert.IsNotNull(first);
            Assert.AreSame(first, second, "EnsureSystems가 중복 생성됨");
        }

        // 0.1b: Systems는 DontDestroyOnLoad라 씬을 바꿔도 유지된다.
        [UnityTest]
        public IEnumerator Services_persist_after_map_scene_swap()
        {
            GameServices.ResetForTests();
            var systems = GameBootstrap.EnsureSystems();
            yield return null;

            Assert.AreEqual("DontDestroyOnLoad", systems.gameObject.scene.name, "Systems가 영속 씬에 없음");
            Assert.IsTrue(GameServices.Registry.IsRegistered<IClockService>());

            var temp = SceneManager.CreateScene("TempSwap_" + System.Guid.NewGuid().ToString("N"));
            yield return null;
            Assert.IsNotNull(GameBootstrap.Instance, "씬 생성 후 Systems 소실");
            Assert.IsTrue(GameServices.Registry.IsRegistered<IClockService>());

            yield return SceneManager.UnloadSceneAsync(temp);
        }

        // 0.1c: 씬에 SceneEntryPoint만 있으면 EnsureSystems가 자동 호출된다(맵 씬 단독 진입, NullRef 0).
        [UnityTest]
        public IEnumerator SceneEntryPoint_boots_systems_standalone()
        {
            GameServices.ResetForTests();
            var entry = new GameObject("EntryPoint", typeof(SceneEntryPoint));
            yield return null;

            Assert.IsNotNull(GameBootstrap.Instance, "SceneEntryPoint가 Systems를 부트하지 않음");
            Assert.IsTrue(GameServices.Registry.IsRegistered<IClockService>());
            Assert.IsTrue(GameServices.Registry.IsRegistered<IGameStateService>());

            Object.DestroyImmediate(entry);
        }
    }
}
