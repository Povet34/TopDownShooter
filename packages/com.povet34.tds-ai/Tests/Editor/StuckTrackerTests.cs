using NUnit.Framework;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class StuckTrackerTests
    {
        [Test]
        public void Moving_every_tick_never_stuck()
        {
            var t = new StuckTracker();
            for (int i = 0; i < 100; i++)
                Assert.IsFalse(t.Tick(movedEnough: true, dt: 0.1f, stuckSeconds: 1f));
        }

        [Test]
        public void Not_moving_long_enough_is_stuck()
        {
            var t = new StuckTracker();
            bool stuck = false;
            // 1.5s 동안 정지(0.1s * 15) → 1.0s 임계 초과
            for (int i = 0; i < 15; i++)
                stuck |= t.Tick(movedEnough: false, dt: 0.1f, stuckSeconds: 1f);
            Assert.IsTrue(stuck, "오래 정지했는데 stuck으로 감지 안 됨");
        }

        [Test]
        public void Brief_stall_under_threshold_is_not_stuck()
        {
            var t = new StuckTracker();
            bool stuck = false;
            for (int i = 0; i < 9; i++) // 0.9s < 1.0s
                stuck |= t.Tick(false, 0.1f, 1f);
            Assert.IsFalse(stuck);
        }

        [Test]
        public void A_move_resets_the_timer()
        {
            var t = new StuckTracker();
            for (int i = 0; i < 9; i++) t.Tick(false, 0.1f, 1f); // 0.9s 정지
            Assert.IsFalse(t.Tick(true, 0.1f, 1f));               // 움직임 → 리셋
            bool stuck = false;
            for (int i = 0; i < 9; i++) stuck |= t.Tick(false, 0.1f, 1f); // 다시 0.9s
            Assert.IsFalse(stuck, "이동으로 리셋됐어야 함");
        }

        [Test]
        public void Resets_after_firing_so_it_does_not_spam()
        {
            var t = new StuckTracker();
            int fires = 0;
            for (int i = 0; i < 25; i++) // 2.5s 정지 → 1s마다 한 번씩만
                if (t.Tick(false, 0.1f, 1f)) fires++;
            Assert.AreEqual(2, fires, "복구 후 타이머 리셋이 안 돼 과발동");
        }
    }
}
