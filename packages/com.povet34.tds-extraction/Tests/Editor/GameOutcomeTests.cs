using NUnit.Framework;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class GameOutcomeTests
    {
        [Test]
        public void Playing_while_alive_and_waves_ongoing()
        {
            Assert.AreEqual(MatchState.Playing, GameOutcome.Evaluate(100, false, 4));
        }

        [Test]
        public void Defeat_when_health_zero()
        {
            Assert.AreEqual(MatchState.Defeat, GameOutcome.Evaluate(0, false, 4));
        }

        [Test]
        public void Defeat_when_health_negative()
        {
            Assert.AreEqual(MatchState.Defeat, GameOutcome.Evaluate(-50, false, 0));
        }

        [Test]
        public void Defeat_takes_priority_over_victory()
        {
            // 마지막 웨이브를 깬 프레임에 같이 죽어도 패배 우선
            Assert.AreEqual(MatchState.Defeat, GameOutcome.Evaluate(0, true, 0));
        }

        [Test]
        public void Victory_when_all_waves_finished_and_no_enemies()
        {
            Assert.AreEqual(MatchState.Victory, GameOutcome.Evaluate(60, true, 0));
        }

        [Test]
        public void Not_victory_while_enemies_remain_even_if_finished_flag_set()
        {
            Assert.AreEqual(MatchState.Playing, GameOutcome.Evaluate(60, true, 2));
        }

        [Test]
        public void Not_victory_until_waves_finished()
        {
            Assert.AreEqual(MatchState.Playing, GameOutcome.Evaluate(60, false, 0));
        }
    }
}
