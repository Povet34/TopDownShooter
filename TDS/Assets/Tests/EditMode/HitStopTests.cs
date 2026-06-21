using NUnit.Framework;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class HitStopTests
    {
        [Test]
        public void Inactive_returns_normal_scale()
        {
            var hs = new HitStop();
            Assert.IsFalse(hs.IsActive);
            Assert.AreEqual(1f, hs.Tick(0.1f));
        }

        [Test]
        public void Frozen_while_active()
        {
            var hs = new HitStop();
            hs.Trigger(0.1f);
            Assert.IsTrue(hs.IsActive);
            Assert.AreEqual(0f, hs.Tick(0.05f), "정지 중엔 timeScale 0");
            Assert.IsTrue(hs.IsActive);
        }

        [Test]
        public void Restores_when_duration_elapses()
        {
            var hs = new HitStop();
            hs.Trigger(0.1f);
            hs.Tick(0.05f);
            Assert.AreEqual(1f, hs.Tick(0.06f), "시간 지나면 normalScale 복귀");
            Assert.IsFalse(hs.IsActive);
        }

        [Test]
        public void Trigger_extends_but_never_shortens()
        {
            var hs = new HitStop();
            hs.Trigger(0.1f);
            hs.Trigger(0.02f); // 더 짧은 요청은 무시
            Assert.AreEqual(0f, hs.Tick(0.05f), "여전히 정지(0.1초가 유지)");
            Assert.IsTrue(hs.IsActive);
        }

        [Test]
        public void Custom_normal_scale_is_returned_when_inactive()
        {
            var hs = new HitStop();
            Assert.AreEqual(0.5f, hs.Tick(0.1f, 0.5f));
        }
    }
}
