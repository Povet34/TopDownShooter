using NUnit.Framework;
using UnityEngine;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class CarExitTests
    {
        private static Vector3[] Pts(params float[] xs)
        {
            var a = new Vector3[xs.Length];
            for (int i = 0; i < xs.Length; i++) a[i] = new Vector3(xs[i], 0, 0);
            return a;
        }

        [Test]
        public void Picks_first_when_all_clear()
        {
            bool ok = CarExit.TryPickClearPoint(Pts(1, 2, 3), _ => true, out var r);
            Assert.IsTrue(ok);
            Assert.AreEqual(1f, r.x);
        }

        [Test]
        public void Skips_blocked_picks_first_clear()
        {
            // x<=2 막힘, x>2 비어있음 → 3 선택
            bool ok = CarExit.TryPickClearPoint(Pts(1, 2, 3), p => p.x > 2f, out var r);
            Assert.IsTrue(ok);
            Assert.AreEqual(3f, r.x);
        }

        [Test]
        public void All_blocked_falls_back_to_first()
        {
            bool ok = CarExit.TryPickClearPoint(Pts(5, 6, 7), _ => false, out var r);
            Assert.IsTrue(ok);
            Assert.AreEqual(5f, r.x);
        }

        [Test]
        public void Empty_returns_false()
        {
            Assert.IsFalse(CarExit.TryPickClearPoint(new Vector3[0], _ => true, out _));
            Assert.IsFalse(CarExit.TryPickClearPoint(null, _ => true, out _));
        }
    }
}
