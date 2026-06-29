using NUnit.Framework;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    // 이동 중 사격 페널티: 빠를수록 탄퍼짐↑, 사격 중엔 이동속도↓.
    public class MovingSpreadTests
    {
        [Test]
        public void Stationary_has_no_spread_penalty()
        {
            Assert.AreEqual(1f, MovingSpread.SpreadMultiplier(0f, 6f, 2f), 1e-4f);
        }

        [Test]
        public void Full_speed_applies_full_penalty()
        {
            // 전속(speed==maxSpeed) → 1 + maxPenalty
            Assert.AreEqual(3f, MovingSpread.SpreadMultiplier(6f, 6f, 2f), 1e-4f);
        }

        [Test]
        public void Half_speed_applies_half_penalty()
        {
            Assert.AreEqual(2f, MovingSpread.SpreadMultiplier(3f, 6f, 2f), 1e-4f);
        }

        [Test]
        public void Above_max_speed_is_clamped()
        {
            Assert.AreEqual(3f, MovingSpread.SpreadMultiplier(99f, 6f, 2f), 1e-4f);
        }

        [Test]
        public void Zero_max_speed_is_safe()
        {
            Assert.AreEqual(1f, MovingSpread.SpreadMultiplier(5f, 0f, 2f), 1e-4f);
        }

        [Test]
        public void Move_speed_factor_slows_only_while_shooting()
        {
            Assert.AreEqual(0.5f, MovingSpread.MoveSpeedFactor(true, 0.5f), 1e-4f);
            Assert.AreEqual(1f, MovingSpread.MoveSpeedFactor(false, 0.5f), 1e-4f);
        }

        [Test]
        public void Move_speed_factor_clamps_out_of_range()
        {
            Assert.AreEqual(1f, MovingSpread.MoveSpeedFactor(true, 5f), 1e-4f);
            Assert.AreEqual(0f, MovingSpread.MoveSpeedFactor(true, -1f), 1e-4f);
        }
    }
}
