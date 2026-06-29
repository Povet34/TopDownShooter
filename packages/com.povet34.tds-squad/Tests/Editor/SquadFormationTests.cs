using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class SquadFormationTests
    {
        [Test]
        public void SpiralOffset_stays_flat_on_ground()
        {
            for (int i = 0; i < 8; i++)
                Assert.AreEqual(0f, SquadFormation.SpiralOffset(i, 8, 5f).y, 1e-5f);
        }

        [Test]
        public void SpiralOffset_radius_grows_with_index()
        {
            float prev = -1f;
            for (int i = 0; i < 10; i++)
            {
                float r = SquadFormation.SpiralOffset(i, 10, 5f).magnitude;
                Assert.Greater(r, prev, "반경은 index에 따라 단조 증가해야 함");
                prev = r;
            }
        }

        [Test]
        public void SpiralOffset_all_within_radius()
        {
            const int n = 12;
            const float radius = 6f;
            for (int i = 0; i < n; i++)
                Assert.LessOrEqual(SquadFormation.SpiralOffset(i, n, radius).magnitude, radius);
        }

        [Test]
        public void SpiralOffset_members_are_spread_apart()
        {
            // 황금각 분산 → 인접 두 멤버가 같은 점에 쌓이지 않는다.
            Vector3 a = SquadFormation.SpiralOffset(0, 5, 5f);
            Vector3 b = SquadFormation.SpiralOffset(1, 5, 5f);
            Assert.Greater((a - b).magnitude, 0.5f);
        }

        [Test]
        public void SpiralOffset_clamps_bad_args()
        {
            // count 0이어도(Max(1,n)) NaN/예외 없이 유한값, index 음수는 0으로 취급.
            Vector3 zeroCount = SquadFormation.SpiralOffset(0, 0, 5f);
            Assert.IsFalse(float.IsNaN(zeroCount.x) || float.IsNaN(zeroCount.z));
            Assert.AreEqual(SquadFormation.SpiralOffset(0, 5, 5f), SquadFormation.SpiralOffset(-3, 5, 5f));
        }

        [Test]
        public void SpiralPoint_offsets_from_center()
        {
            Vector3 center = new Vector3(10f, 0f, -4f);
            Assert.AreEqual(center + SquadFormation.SpiralOffset(2, 6, 4f),
                            SquadFormation.SpiralPoint(center, 2, 6, 4f));
        }

        [Test]
        public void AllGathered_true_when_empty()
        {
            Assert.IsTrue(SquadFormation.AllGathered(new List<Vector3>(), Vector3.zero, 2.5f));
            Assert.IsTrue(SquadFormation.AllGathered(null, Vector3.zero, 2.5f));
        }

        [Test]
        public void AllGathered_true_when_all_inside_threshold()
        {
            var anchor = Vector3.zero;
            var positions = new List<Vector3>
            {
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 5f, 2f),   // y는 무시 → 평면거리 2
                new Vector3(-3f, 0f, 3f),  // 평면거리 ~4.24 < 2.5+3=5.5
            };
            Assert.IsTrue(SquadFormation.AllGathered(positions, anchor, 2.5f, 3f));
        }

        [Test]
        public void AllGathered_false_when_one_lags_behind()
        {
            var anchor = Vector3.zero;
            var positions = new List<Vector3>
            {
                new Vector3(1f, 0f, 0f),
                new Vector3(10f, 0f, 0f),  // 평면거리 10 > 5.5 → 낙오
            };
            Assert.IsFalse(SquadFormation.AllGathered(positions, anchor, 2.5f, 3f));
        }
    }
}
