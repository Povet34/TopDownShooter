using NUnit.Framework;
using UnityEngine;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class StrafeBlendTests
    {
        // 적은 플레이어를 바라보며(facingForward) 이동하므로, 월드 속도를 facing 기준 로컬로 변환해
        // (x=우측, y=전방) 블렌드 파라미터로 만든다. 2D 블렌드 트리(전/후/좌/우/대각)에 사용.

        [Test]
        public void Moving_in_facing_direction_is_forward()
        {
            Vector2 b = StrafeBlend.Compute(new Vector3(0, 0, 2), new Vector3(0, 0, 1));
            Assert.AreEqual(0f, b.x, 1e-4f);
            Assert.AreEqual(1f, b.y, 1e-4f);
        }

        [Test]
        public void Moving_to_the_right_of_facing_is_positive_x()
        {
            Vector2 b = StrafeBlend.Compute(new Vector3(2, 0, 0), new Vector3(0, 0, 1));
            Assert.AreEqual(1f, b.x, 1e-4f);
            Assert.AreEqual(0f, b.y, 1e-4f);
        }

        [Test]
        public void Moving_backward_is_negative_y()
        {
            Vector2 b = StrafeBlend.Compute(new Vector3(0, 0, -2), new Vector3(0, 0, 1));
            Assert.AreEqual(0f, b.x, 1e-4f);
            Assert.AreEqual(-1f, b.y, 1e-4f);
        }

        [Test]
        public void Moving_left_is_negative_x()
        {
            Vector2 b = StrafeBlend.Compute(new Vector3(-2, 0, 0), new Vector3(0, 0, 1));
            Assert.AreEqual(-1f, b.x, 1e-4f);
            Assert.AreEqual(0f, b.y, 1e-4f);
        }

        [Test]
        public void Diagonal_forward_right_is_balanced()
        {
            Vector2 b = StrafeBlend.Compute(new Vector3(2, 0, 2), new Vector3(0, 0, 1));
            Assert.AreEqual(0.7071f, b.x, 1e-3f);
            Assert.AreEqual(0.7071f, b.y, 1e-3f);
        }

        [Test]
        public void Result_is_relative_to_facing_not_world()
        {
            // 동쪽을 바라보며 남쪽으로 이동 = facing 기준 '우측' → (1,0)
            Vector2 b = StrafeBlend.Compute(new Vector3(0, 0, -1), new Vector3(1, 0, 0));
            Assert.AreEqual(1f, b.x, 1e-4f);
            Assert.AreEqual(0f, b.y, 1e-4f);
        }

        [Test]
        public void Vertical_velocity_is_ignored()
        {
            Vector2 b = StrafeBlend.Compute(new Vector3(0, 5, 2), new Vector3(0, 0, 1));
            Assert.AreEqual(0f, b.x, 1e-4f);
            Assert.AreEqual(1f, b.y, 1e-4f);
        }

        [Test]
        public void Stationary_is_zero()
        {
            Vector2 b = StrafeBlend.Compute(Vector3.zero, new Vector3(0, 0, 1));
            Assert.AreEqual(Vector2.zero, b);
        }

        [Test]
        public void Below_deadzone_is_zero()
        {
            Vector2 b = StrafeBlend.Compute(new Vector3(0.01f, 0, 0.01f), new Vector3(0, 0, 1));
            Assert.AreEqual(Vector2.zero, b);
        }

        [Test]
        public void Degenerate_facing_is_zero()
        {
            Vector2 b = StrafeBlend.Compute(new Vector3(0, 0, 2), Vector3.zero);
            Assert.AreEqual(Vector2.zero, b);
        }
    }
}
