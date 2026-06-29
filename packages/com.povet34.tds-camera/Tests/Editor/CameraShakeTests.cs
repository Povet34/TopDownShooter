using NUnit.Framework;
using UnityEngine;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class CameraShakeTests
    {
        [Test]
        public void Add_trauma_clamps_to_one()
        {
            var s = new CameraShake();
            s.AddTrauma(0.6f);
            s.AddTrauma(0.7f);
            Assert.AreEqual(1f, s.Trauma, 1e-4f);
        }

        [Test]
        public void Add_trauma_does_not_go_below_zero_offset()
        {
            var s = new CameraShake();
            // trauma 0이면 오프셋은 노이즈와 무관하게 정확히 0
            Assert.AreEqual(Vector3.zero, s.Tick(0.016f));
        }

        [Test]
        public void Trauma_decays_over_time()
        {
            var s = new CameraShake { decayPerSecond = 1.5f };
            s.AddTrauma(1f);
            s.Tick(0.5f); // 1 - 0.75
            Assert.AreEqual(0.25f, s.Trauma, 1e-4f);
        }

        [Test]
        public void Trauma_decays_to_zero_and_clamps()
        {
            var s = new CameraShake { decayPerSecond = 1.5f };
            s.AddTrauma(1f);
            s.Tick(1f); // 1 - 1.5 → clamp 0
            Assert.AreEqual(0f, s.Trauma, 1e-4f);
        }

        [Test]
        public void Offset_is_bounded_by_max_offset()
        {
            var s = new CameraShake { maxOffset = 0.5f };
            s.AddTrauma(1f);
            for (int i = 0; i < 20; i++)
            {
                var o = s.Tick(0.0001f); // 시간만 살짝 진행(trauma 거의 유지)
                Assert.LessOrEqual(Mathf.Abs(o.x), 0.5f + 1e-4f);
                Assert.LessOrEqual(Mathf.Abs(o.z), 0.5f + 1e-4f);
                Assert.AreEqual(0f, o.y, 1e-6f, "탑다운 — 수직 흔들림 없음");
                s.AddTrauma(0.01f); // 감쇠 상쇄해 trauma 유지
            }
        }

        [Test]
        public void Rotation_is_zero_when_no_trauma()
        {
            var s = new CameraShake();
            s.Tick(0.016f);
            Assert.AreEqual(0f, s.RotationZ(), 1e-6f);
        }
    }
}
