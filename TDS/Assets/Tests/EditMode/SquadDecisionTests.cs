using NUnit.Framework;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    // §6.3 분대 의사결정: 교전 > 디스폰 > 순찰.
    public class SquadDecisionTests
    {
        [Test]
        public void Engage_trigger_wins_over_everything()
        {
            // 교전 트리거면 가장자리/로밍이어도 교전.
            Assert.AreEqual(SquadIntent.Engaging,
                SquadDecision.Resolve(engageTrigger: true, roaming: true, hasLeftEdge: true, atEdge: true));
        }

        [Test]
        public void Patrols_when_calm_and_not_at_edge()
        {
            Assert.AreEqual(SquadIntent.Patrolling,
                SquadDecision.Resolve(false, roaming: true, hasLeftEdge: true, atEdge: false));
        }

        [Test]
        public void Despawns_when_roaming_left_edge_and_back_at_edge()
        {
            Assert.AreEqual(SquadIntent.Despawning,
                SquadDecision.Resolve(false, roaming: true, hasLeftEdge: true, atEdge: true));
        }

        [Test]
        public void Does_not_despawn_at_spawn_edge_before_leaving()
        {
            // 스폰 직후(아직 가장자리 안 벗어남) 가장자리에 있어도 디스폰 금지 → 순찰.
            Assert.AreEqual(SquadIntent.Patrolling,
                SquadDecision.Resolve(false, roaming: true, hasLeftEdge: false, atEdge: true));
        }

        [Test]
        public void Non_roaming_squad_never_despawns_at_edge()
        {
            // 웨이브(비로밍) 분대는 가장자리여도 디스폰하지 않음.
            Assert.AreEqual(SquadIntent.Patrolling,
                SquadDecision.Resolve(false, roaming: false, hasLeftEdge: true, atEdge: true));
        }
    }
}
