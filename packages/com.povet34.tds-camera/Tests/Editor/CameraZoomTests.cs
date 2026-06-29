using NUnit.Framework;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class CameraZoomTests
    {
        [Test]
        public void Scroll_up_zooms_in_decreases_zoom()
        {
            float z = CameraZoom.Step(1f, 100f, 0.001f, 0.5f, 1.8f);
            Assert.Less(z, 1f);
        }

        [Test]
        public void Scroll_down_zooms_out_increases_zoom()
        {
            float z = CameraZoom.Step(1f, -100f, 0.001f, 0.5f, 1.8f);
            Assert.Greater(z, 1f);
        }

        [Test]
        public void Clamped_to_min()
        {
            Assert.AreEqual(0.5f, CameraZoom.Step(0.6f, 1000f, 0.001f, 0.5f, 1.8f), 1e-4f);
        }

        [Test]
        public void Clamped_to_max()
        {
            Assert.AreEqual(1.8f, CameraZoom.Step(1.7f, -1000f, 0.001f, 0.5f, 1.8f), 1e-4f);
        }

        [Test]
        public void Zero_scroll_keeps_zoom()
        {
            Assert.AreEqual(1.2f, CameraZoom.Step(1.2f, 0f, 0.001f, 0.5f, 1.8f), 1e-4f);
        }
    }
}
