using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using TDS.Core;

namespace TDS.Tests.PlayMode
{
    /// <summary>
    /// Phase 0 통합 명세. 일부는 PlayMode 하니스 동작 확인용(green),
    /// 나머지는 0.1a~0.1c 구현 시 [Ignore]를 제거하며 green으로 만든다(spec-first).
    /// </summary>
    public class BootstrapSpecTests
    {
        private interface IPing { int Value { get; } }
        private class Ping : IPing { public int Value => 42; }

        // PlayMode 하니스가 실제로 도는지 확인 (그리고 ServiceRegistry 런타임 동작).
        [UnityTest]
        public IEnumerator ServiceRegistry_resolves_across_a_frame()
        {
            var reg = new ServiceRegistry();
            reg.Register<IPing>(new Ping());
            yield return null; // 한 프레임 진행
            Assert.IsTrue(reg.TryResolve<IPing>(out var p));
            Assert.AreEqual(42, p.Value);
        }

        // ---- Phase 0 통합 명세 (구현되면 [Ignore] 제거) ----

        [Test]
        [Ignore("Phase 0.1a: GameBootstrap 구현 후 활성화 — 부트가 필수 서비스 9종을 등록해야 함")]
        public void Bootstrap_registers_all_required_services() { }

        [Test]
        [Ignore("Phase 0.1b: Systems 씬 분리 후 활성화 — 맵 씬 교체 시 시스템/서비스가 유지돼야 함")]
        public void Services_persist_after_map_scene_swap() { }

        [Test]
        [Ignore("Phase 0.1c: Map_Generated + 플레이어가 NullRef 없이 독립 실행돼야 함")]
        public void Map_scene_runs_standalone_without_nullrefs() { }
    }
}
