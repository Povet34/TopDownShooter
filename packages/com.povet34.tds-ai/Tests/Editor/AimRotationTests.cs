using NUnit.Framework;
using UnityEngine;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class AimRotationTests
    {
        [Test]
        public void Zero_direction_keeps_current_rotation()
        {
            var current = Quaternion.Euler(0f, 123f, 0f);
            // from == aimPoint (높이만 다름) → XZ 방향 0 → current 유지 (LookRotation 경고 회피)
            var r = AimRotation.FaceHorizontal(new Vector3(1f, 0f, 1f), new Vector3(1f, 9f, 1f), current);
            Assert.AreEqual(current, r);
        }

        [Test]
        public void Points_toward_target_on_xz()
        {
            var r = AimRotation.FaceHorizontal(Vector3.zero, new Vector3(0f, 0f, 5f), Quaternion.identity);
            Assert.Less((r * Vector3.forward - new Vector3(0f, 0f, 1f)).magnitude, 1e-3f);
        }

        [Test]
        public void Ignores_y_component()
        {
            var r = AimRotation.FaceHorizontal(Vector3.zero, new Vector3(5f, 100f, 0f), Quaternion.identity);
            var fwd = r * Vector3.forward;
            Assert.Less((fwd - new Vector3(1f, 0f, 0f)).magnitude, 1e-3f, "y를 무시하고 +x를 바라봐야 함");
        }
    }
}
