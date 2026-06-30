using NUnit.Framework;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class StatusEffectsTests
    {
        [Test]
        public void Bleed_deals_dot_over_time_then_expires()
        {
            var s = new StatusEffects();
            s.Apply(StatusKind.Bleed, 2f, 10f); // 10 dmg/sec for 2s
            Assert.AreEqual(10f, s.Tick(1f), 0.001f);
            Assert.IsTrue(s.Has(StatusKind.Bleed));
            Assert.AreEqual(10f, s.Tick(1f), 0.001f);
            Assert.IsFalse(s.Has(StatusKind.Bleed)); // 만료
            Assert.AreEqual(0f, s.Tick(1f), 0.001f);
        }

        [Test]
        public void Slow_reduces_speed_multiplier()
        {
            var s = new StatusEffects();
            Assert.AreEqual(1f, s.SpeedMultiplier, 0.001f);
            s.Apply(StatusKind.Slow, 5f, 0.4f); // 40% 감속
            Assert.AreEqual(0.6f, s.SpeedMultiplier, 0.001f);
        }

        [Test]
        public void Stun_zeroes_speed_and_flags()
        {
            var s = new StatusEffects();
            s.Apply(StatusKind.Stun, 1f, 1f);
            Assert.IsTrue(s.IsStunned);
            Assert.AreEqual(0f, s.SpeedMultiplier, 0.001f);
        }

        [Test]
        public void Reapply_refreshes_to_longer_and_stronger()
        {
            var s = new StatusEffects();
            s.Apply(StatusKind.Slow, 1f, 0.2f);
            s.Apply(StatusKind.Slow, 5f, 0.5f); // 더 길고 강하게 갱신
            Assert.AreEqual(0.5f, s.SpeedMultiplier, 0.001f);
            s.Tick(2f); // 1s였으면 만료됐을 것 — 갱신됐으니 살아있음
            Assert.IsTrue(s.Has(StatusKind.Slow));
        }

        [Test]
        public void Non_positive_duration_is_ignored()
        {
            var s = new StatusEffects();
            s.Apply(StatusKind.Bleed, 0f, 10f);
            Assert.IsFalse(s.Any);
        }

        [Test]
        public void Clear_removes_all()
        {
            var s = new StatusEffects();
            s.Apply(StatusKind.Bleed, 5f, 3f);
            s.Apply(StatusKind.Slow, 5f, 0.3f);
            s.Clear();
            Assert.IsFalse(s.Any);
            Assert.AreEqual(1f, s.SpeedMultiplier, 0.001f);
        }
    }
}
