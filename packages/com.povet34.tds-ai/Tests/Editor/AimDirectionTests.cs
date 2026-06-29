using NUnit.Framework;
using UnityEngine;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class AimDirectionTests
    {
        [Test]
        public void Normal_returns_unit_direction_to_aim()
        {
            Vector3 d = AimDirection.Resolve(Vector3.zero, new Vector3(0, 0, 5), Vector3.right);
            Assert.AreEqual(Vector3.forward, d);
        }

        [Test]
        public void Result_is_normalized()
        {
            Vector3 d = AimDirection.Resolve(Vector3.zero, new Vector3(3, 4, 0), Vector3.right);
            Assert.AreEqual(1f, d.magnitude, 1e-4f);
        }

        [Test]
        public void Degenerate_aim_uses_fallback()
        {
            // aim이 from과 거의 같은 위치(0.1 < 임계 0.2) → fallback 방향
            Vector3 d = AimDirection.Resolve(Vector3.zero, new Vector3(0, 0.1f, 0), Vector3.right);
            Assert.AreEqual(Vector3.right, d);
        }

        [Test]
        public void Degenerate_with_zero_fallback_returns_forward()
        {
            Vector3 d = AimDirection.Resolve(Vector3.zero, Vector3.zero, Vector3.zero);
            Assert.AreEqual(Vector3.forward, d);
        }

        [Test]
        public void Just_outside_threshold_uses_real_direction()
        {
            // 0.3 > 임계 0.2 → 실제 방향 사용(fallback 아님)
            Vector3 d = AimDirection.Resolve(Vector3.zero, new Vector3(0.3f, 0, 0), Vector3.forward);
            Assert.AreEqual(Vector3.right, d);
        }

        [Test]
        public void Fallback_is_normalized_too()
        {
            Vector3 d = AimDirection.Resolve(Vector3.zero, Vector3.zero, new Vector3(0, 0, 5));
            Assert.AreEqual(Vector3.forward, d); // (0,0,5) 정규화 → (0,0,1)
        }

        [Test]
        public void Horizontal_ignores_y_difference()
        {
            // 조준점이 아래(y=-5)에 있어도 수평 방향만(탑다운)
            Vector3 d = AimDirection.ResolveHorizontal(Vector3.zero, new Vector3(0, -5, 3), Vector3.right);
            Assert.AreEqual(Vector3.forward, d);
            Assert.AreEqual(0f, d.y, 1e-6f);
        }

        [Test]
        public void Horizontal_straight_below_uses_fallback()
        {
            // 총구 바로 아래(발밑) → 수평 성분 0 → fallback(수평 forward)
            Vector3 d = AimDirection.ResolveHorizontal(new Vector3(0, 1.4f, 0), Vector3.zero, Vector3.forward);
            Assert.AreEqual(Vector3.forward, d);
        }

        [Test]
        public void Horizontal_result_is_flat_and_unit()
        {
            Vector3 d = AimDirection.ResolveHorizontal(Vector3.zero, new Vector3(3, 9, 4), Vector3.right);
            Assert.AreEqual(0f, d.y, 1e-6f);
            Assert.AreEqual(1f, d.magnitude, 1e-4f);
        }

        [Test]
        public void ClampMinDistance_keeps_far_aim_unchanged()
        {
            Vector3 aim = new Vector3(0, 1, 10);
            Assert.AreEqual(aim, AimDirection.ClampMinDistance(Vector3.zero, aim, 3f, Vector3.forward));
        }

        [Test]
        public void ClampMinDistance_pushes_near_aim_out_to_min()
        {
            Vector3 c = AimDirection.ClampMinDistance(Vector3.zero, new Vector3(0, 1, 1), 3f, Vector3.forward);
            Vector3 flat = c; flat.y = 0;
            Assert.AreEqual(3f, flat.magnitude, 1e-3f, "최소 거리로 밀려야");
            Assert.AreEqual(1f, c.y, 1e-4f, "y는 유지");
        }

        [Test]
        public void ClampMinDistance_at_player_uses_fallback_forward()
        {
            Vector3 c = AimDirection.ClampMinDistance(Vector3.zero, new Vector3(0, 1, 0), 3f, Vector3.forward);
            Assert.AreEqual(new Vector3(0, 1, 3), c);
        }
    }
}
