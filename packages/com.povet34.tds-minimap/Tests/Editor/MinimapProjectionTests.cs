using NUnit.Framework;
using UnityEngine;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class MinimapProjectionTests
    {
        [Test]
        public void Player_position_maps_to_centre()
        {
            var p = MinimapProjection.ToMinimap(new Vector2(10f, 10f), new Vector2(10f, 10f), 90f, 90f, out bool outside);
            Assert.AreEqual(Vector2.zero, p);
            Assert.IsFalse(outside);
        }

        [Test]
        public void World_z_maps_to_minimap_up()
        {
            // 플레이어 앞(z+)에 있는 대상 → 미니맵 위(y+)
            var p = MinimapProjection.ToMinimap(new Vector2(0f, 45f), Vector2.zero, 90f, 90f, out _);
            Assert.AreEqual(0f, p.x, 0.001f);
            Assert.AreEqual(45f, p.y, 0.001f); // worldRange 90의 절반 → 픽셀 반경 90의 절반
        }

        [Test]
        public void Scales_linearly_within_range()
        {
            var p = MinimapProjection.ToMinimap(new Vector2(45f, 0f), Vector2.zero, 90f, 90f, out bool outside);
            Assert.AreEqual(45f, p.x, 0.001f);
            Assert.IsFalse(outside);
        }

        [Test]
        public void Out_of_range_clamps_to_edge_keeping_direction()
        {
            var p = MinimapProjection.ToMinimap(new Vector2(180f, 0f), Vector2.zero, 90f, 90f, out bool outside);
            Assert.AreEqual(90f, p.magnitude, 0.001f, "가장자리(반경)에 붙어야");
            Assert.AreEqual(90f, p.x, 0.001f);
            Assert.IsTrue(outside);
        }

        [Test]
        public void Diagonal_out_of_range_clamps_on_circle()
        {
            var p = MinimapProjection.ToMinimap(new Vector2(1000f, 1000f), Vector2.zero, 90f, 90f, out bool outside);
            Assert.AreEqual(90f, p.magnitude, 0.001f);
            Assert.IsTrue(outside);
            Assert.AreEqual(p.x, p.y, 0.001f, "대각선 방향 유지");
        }

        [Test]
        public void In_range_check()
        {
            Assert.IsTrue(MinimapProjection.IsInRange(new Vector2(50f, 0f), Vector2.zero, 90f));
            Assert.IsFalse(MinimapProjection.IsInRange(new Vector2(100f, 0f), Vector2.zero, 90f));
        }

        [Test]
        public void Degenerate_inputs_are_safe()
        {
            Assert.AreEqual(Vector2.zero, MinimapProjection.ToMinimap(Vector2.one, Vector2.zero, 0f, 90f, out _));
            Assert.AreEqual(Vector2.zero, MinimapProjection.ToMinimap(Vector2.one, Vector2.zero, 90f, 0f, out _));
        }
    }
}
