using NUnit.Framework;
using UnityEngine;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class HealthVignetteTests
    {
        [Test]
        public void Full_health_is_zero()
            => Assert.AreEqual(0f, HealthVignette.Intensity(100, 100, 0.35f), 0.0001f);

        [Test]
        public void Above_threshold_is_zero()
            => Assert.AreEqual(0f, HealthVignette.Intensity(50, 100, 0.35f), 0.0001f);

        [Test]
        public void At_threshold_is_zero()
            => Assert.AreEqual(0f, HealthVignette.Intensity(35, 100, 0.35f), 0.0001f);

        [Test]
        public void Zero_health_is_full()
            => Assert.AreEqual(1f, HealthVignette.Intensity(0, 100, 0.35f), 0.0001f);

        [Test]
        public void Half_into_threshold_is_half()
        {
            // ratio = 0.175 = startRatio/2 → intensity 0.5
            Assert.AreEqual(0.5f, HealthVignette.Intensity(175, 1000, 0.35f), 0.001f);
        }

        [Test]
        public void Ramps_monotonically_as_health_drops()
        {
            float a = HealthVignette.Intensity(30, 100, 0.35f);
            float b = HealthVignette.Intensity(20, 100, 0.35f);
            float c = HealthVignette.Intensity(10, 100, 0.35f);
            Assert.Less(a, b);
            Assert.Less(b, c);
        }

        [Test]
        public void Degenerate_inputs_are_safe()
        {
            Assert.AreEqual(0f, HealthVignette.Intensity(10, 0, 0.35f), 0.0001f);
            Assert.AreEqual(0f, HealthVignette.Intensity(10, 100, 0f), 0.0001f);
        }

        [Test]
        public void Pulse_stays_in_range()
        {
            for (int i = 0; i < 50; i++)
            {
                float t = i * 0.1f;
                float p = HealthVignette.Pulse(t, 6f, 0.6f);
                Assert.GreaterOrEqual(p, 0.6f - 0.0001f);
                Assert.LessOrEqual(p, 1f + 0.0001f);
            }
        }
    }
}
