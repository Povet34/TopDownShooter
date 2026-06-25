using NUnit.Framework;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class ExtractionTests
    {
        [Test]
        public void Not_complete_before_required_time()
        {
            var p = new ExtractionProgress(3f);
            p.Tick(1f, true);
            Assert.IsFalse(p.IsComplete);
            Assert.AreEqual(1f / 3f, p.Progress01, 0.001f);
        }

        [Test]
        public void Completes_after_dwelling_required_time()
        {
            var p = new ExtractionProgress(3f);
            p.Tick(1.5f, true);
            p.Tick(1.5f, true);
            Assert.IsTrue(p.IsComplete);
            Assert.AreEqual(1f, p.Progress01, 0.001f);
        }

        [Test]
        public void Outside_zone_does_not_accumulate()
        {
            var p = new ExtractionProgress(3f);
            p.Tick(2f, false);
            Assert.AreEqual(0f, p.Elapsed, 0.001f);
        }

        [Test]
        public void Reset_on_leave_clears_progress()
        {
            var p = new ExtractionProgress(3f, resetOnLeave: true);
            p.Tick(2f, true);
            p.Tick(0.1f, false); // 벗어남 → 리셋
            Assert.AreEqual(0f, p.Elapsed, 0.001f);
        }

        [Test]
        public void Hold_progress_when_not_resetting()
        {
            var p = new ExtractionProgress(3f, resetOnLeave: false);
            p.Tick(2f, true);
            p.Tick(5f, false); // 벗어나도 유지
            Assert.AreEqual(2f, p.Elapsed, 0.001f);
        }

        // GameOutcome 탈출 승리
        [Test]
        public void Extraction_is_victory()
            => Assert.AreEqual(MatchState.Victory, GameOutcome.Evaluate(100, false, 5, extracted: true));

        [Test]
        public void Death_beats_extraction()
            => Assert.AreEqual(MatchState.Defeat, GameOutcome.Evaluate(0, false, 5, extracted: true));

        [Test]
        public void No_extraction_still_plays()
            => Assert.AreEqual(MatchState.Playing, GameOutcome.Evaluate(100, false, 5, extracted: false));
    }
}
