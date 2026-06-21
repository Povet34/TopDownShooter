using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TDS.Core;

namespace TDS.Tests.PlayMode
{
    /// <summary>
    /// 전투 피드백 통합 검증: 서비스 등록 + 처치 시 히트스톱(Time.timeScale 0 → 복귀).
    /// (셰이크/FX는 순수 시임 EditMode + in-game으로 검증.)
    /// </summary>
    public class CombatFeedbackTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // 중요 상태부터 먼저 복구(아래 FX 정리가 실패해도 다음 테스트가 깨지지 않도록)
            Time.timeScale = 1f;
            if (GameBootstrap.Instance != null)
                Object.DestroyImmediate(GameBootstrap.Instance.gameObject);
            GameServices.ResetForTests();

            // CFXR FX는 자식 파티클이 있어 루트 단위로 모아서 정리(부모 파괴 후 자식 접근 방지)
            var roots = new System.Collections.Generic.HashSet<GameObject>();
            foreach (var fx in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (fx != null && fx.transform.root.name.Contains("CFXR"))
                    roots.Add(fx.transform.root.gameObject);
            foreach (var r in roots)
                if (r != null) Object.DestroyImmediate(r);

            yield return null;
        }

        [UnityTest]
        public IEnumerator Registers_service_and_kill_triggers_hitstop()
        {
            GameServices.ResetForTests();
            GameBootstrap.EnsureSystems(); // Systems 프리팹에 CombatFeedback 포함
            yield return null;

            var svc = GameServices.Registry.Resolve<ICombatFeedbackService>();
            Assert.IsNotNull(svc, "ICombatFeedbackService 미등록");

            Assert.AreEqual(1f, Time.timeScale, 1e-3f, "시작 timeScale은 1");

            svc.ReportKill(Vector3.zero); // 히트스톱 트리거
            yield return null;             // CombatFeedback.Update 1회 → timeScale 0
            Assert.AreEqual(0f, Time.timeScale, 1e-3f, "처치 시 히트스톱(timeScale 0) 미작동");

            // unscaled로 정지시간 경과 → 복귀
            float t = 0f;
            while (Time.timeScale == 0f && t < 2f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.AreEqual(1f, Time.timeScale, 1e-3f, "히트스톱 후 timeScale 복귀 안 됨");
        }

        [UnityTest]
        public IEnumerator Hit_does_not_freeze_time()
        {
            GameServices.ResetForTests();
            GameBootstrap.EnsureSystems();
            yield return null;

            var svc = GameServices.Registry.Resolve<ICombatFeedbackService>();
            svc.ReportHit(Vector3.zero, 1f); // 비치명 피격 = 셰이크/FX만, 정지 없음
            yield return null;
            Assert.AreEqual(1f, Time.timeScale, 1e-3f, "비치명 피격은 시간을 멈추면 안 됨");
        }
    }
}
