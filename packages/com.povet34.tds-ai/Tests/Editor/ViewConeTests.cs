using NUnit.Framework;
using UnityEngine;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class ViewConeTests
    {
        // 보는 사람: 원점, +z 바라봄, 반각 45°, 사거리 10
        private static bool V(Vector3 target, float half = 45f, float range = 10f)
            => ViewCone.InView(Vector3.zero, Vector3.forward, target, half, range);

        [Test]
        public void Directly_ahead_in_range_is_visible()
        {
            Assert.IsTrue(V(new Vector3(0, 0, 5)));
        }

        [Test]
        public void Behind_is_not_visible()
        {
            Assert.IsFalse(V(new Vector3(0, 0, -5)));
        }

        [Test]
        public void Beyond_range_is_not_visible()
        {
            Assert.IsFalse(V(new Vector3(0, 0, 12)));
        }

        [Test]
        public void Within_half_angle_is_visible()
        {
            // +z에서 ~30° (반각 45° 안)
            Assert.IsTrue(V(new Vector3(3f, 0, 5f)));
        }

        [Test]
        public void Outside_half_angle_is_not_visible()
        {
            // 옆(+x, 90°) — 반각 45° 밖
            Assert.IsFalse(V(new Vector3(5f, 0, 0f)));
        }

        [Test]
        public void Edge_of_cone_is_visible()
        {
            // 정확히 45°: (sin45, 0, cos45)*dist
            Vector3 edge = new Vector3(Mathf.Sin(45f * Mathf.Deg2Rad), 0, Mathf.Cos(45f * Mathf.Deg2Rad)) * 5f;
            Assert.IsTrue(V(edge));
        }

        [Test]
        public void Vertical_offset_is_ignored()
        {
            // 대상이 위/아래에 있어도 수평 콘 판정
            Assert.IsTrue(V(new Vector3(0, 9, 5)));
        }

        [Test]
        public void On_viewer_is_visible()
        {
            Assert.IsTrue(V(Vector3.zero));
        }

        [Test]
        public void Degenerate_forward_is_not_visible()
        {
            Assert.IsFalse(ViewCone.InView(Vector3.zero, Vector3.zero, new Vector3(0, 0, 5), 45f, 10f));
        }
    }
}
