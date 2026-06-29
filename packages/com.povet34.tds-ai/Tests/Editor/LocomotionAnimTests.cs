using NUnit.Framework;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class LocomotionAnimTests
    {
        [Test]
        public void Full_speed_is_normal_playback()
        {
            Assert.AreEqual(1f, LocomotionAnim.PlaybackSpeed(3f, 3f), 1e-4f);
        }

        [Test]
        public void Half_speed_is_half_playback()
        {
            Assert.AreEqual(0.5f, LocomotionAnim.PlaybackSpeed(1.5f, 3f), 1e-4f);
        }

        [Test]
        public void Stopped_clamps_to_min_not_full_marching()
        {
            // 멈췄을 때 재생속도가 1(제자리걸음)이 아니라 min으로 떨어져야 함
            Assert.AreEqual(0.15f, LocomotionAnim.PlaybackSpeed(0f, 3f), 1e-4f);
        }

        [Test]
        public void Overspeed_clamps_to_max()
        {
            Assert.AreEqual(1.3f, LocomotionAnim.PlaybackSpeed(10f, 3f), 1e-4f);
        }

        [Test]
        public void Zero_reference_speed_is_safe()
        {
            Assert.AreEqual(1f, LocomotionAnim.PlaybackSpeed(0f, 0f));
        }
    }
}
