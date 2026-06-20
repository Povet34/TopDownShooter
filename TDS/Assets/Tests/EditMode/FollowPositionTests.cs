using NUnit.Framework;
using UnityEngine;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class FollowPositionTests
    {
        [Test]
        public void Snaps_to_target_plus_offset_when_smooth_zero()
        {
            var p = FollowPosition.Resolve(new Vector3(5f, 0f, 5f), new Vector3(0f, 10f, -3f), Vector3.zero, 0f, 0.016f);
            Assert.Less((p - new Vector3(5f, 10f, 2f)).magnitude, 1e-4f);
        }

        [Test]
        public void Moves_toward_desired_without_overshoot()
        {
            var target = new Vector3(10f, 0f, 0f);
            var offset = new Vector3(0f, 5f, 0f);
            var current = Vector3.zero;
            var desired = target + offset;

            var p = FollowPosition.Resolve(target, offset, current, 6f, 0.016f);

            Assert.Greater((p - current).magnitude, 0f, "이동해야 함");
            Assert.Less((p - desired).magnitude, (current - desired).magnitude, "목표를 넘지 않아야 함");
        }
    }
}
