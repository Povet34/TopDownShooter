using NUnit.Framework;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class CoverApproachTests
    {
        [Test]
        public void Within_arrive_radius_engages()
        {
            Assert.IsTrue(CoverApproach.ShouldEngage(0.5f, 0f));
        }

        [Test]
        public void At_arrive_radius_boundary_engages()
        {
            Assert.IsTrue(CoverApproach.ShouldEngage(CoverApproach.ArriveRadius, 0f));
        }

        [Test]
        public void Far_but_still_progressing_keeps_running()
        {
            Assert.IsFalse(CoverApproach.ShouldEngage(3f, 0.5f));
        }

        [Test]
        public void Far_but_stalled_gives_up_and_engages()
        {
            // cover point에 못 닿고 비비는 상황 → 진전 없음 1s → 도착 간주
            Assert.IsTrue(CoverApproach.ShouldEngage(3f, CoverApproach.StallGiveUpSeconds));
        }

        [Test]
        public void Far_and_just_under_stall_threshold_keeps_running()
        {
            Assert.IsFalse(CoverApproach.ShouldEngage(5f, CoverApproach.StallGiveUpSeconds - 0.01f));
        }
    }
}
