using NUnit.Framework;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class EvasionPlannerTests
    {
        private static EvasionAbilities All => new EvasionAbilities { canStrafe = true, canBackstep = true, canFlee = true };
        private static EvasionAbilities StrafeOnly => new EvasionAbilities { canStrafe = true };

        [Test]
        public void Not_threatened_and_healthy_holds()
        {
            Assert.AreEqual(EvasionAction.Hold,
                EvasionPlanner.Decide(false, 1f, All, distToTarget: 10f, preferredDistance: 8f));
        }

        [Test]
        public void Low_health_flee_flagged_flees_even_if_calm()
        {
            Assert.AreEqual(EvasionAction.Flee,
                EvasionPlanner.Decide(false, 0.2f, All, distToTarget: 10f, preferredDistance: 8f));
        }

        [Test]
        public void Low_health_without_flee_ability_does_not_flee()
        {
            Assert.AreNotEqual(EvasionAction.Flee,
                EvasionPlanner.Decide(true, 0.1f, StrafeOnly, distToTarget: 10f, preferredDistance: 8f));
        }

        [Test]
        public void Threatened_and_too_close_backsteps()
        {
            // dist 4 < 8*0.6=4.8 → 너무 가까움
            Assert.AreEqual(EvasionAction.Backstep,
                EvasionPlanner.Decide(true, 1f, All, distToTarget: 4f, preferredDistance: 8f));
        }

        [Test]
        public void Threatened_at_range_strafes()
        {
            Assert.AreEqual(EvasionAction.Strafe,
                EvasionPlanner.Decide(true, 1f, All, distToTarget: 8f, preferredDistance: 8f));
        }

        [Test]
        public void Flee_takes_priority_over_backstep()
        {
            // 저체력 + 너무 가까움 → 도주 우선
            Assert.AreEqual(EvasionAction.Flee,
                EvasionPlanner.Decide(true, 0.1f, All, distToTarget: 3f, preferredDistance: 8f));
        }

        [Test]
        public void No_abilities_holds_even_when_threatened()
        {
            Assert.AreEqual(EvasionAction.Hold,
                EvasionPlanner.Decide(true, 1f, new EvasionAbilities(), distToTarget: 3f, preferredDistance: 8f));
        }

        [Test]
        public void Target_distance_orders_strafe_backstep_flee()
        {
            float p = 8f;
            Assert.AreEqual(p, EvasionPlanner.TargetDistance(EvasionAction.Strafe, p), 1e-4f);
            Assert.Greater(EvasionPlanner.TargetDistance(EvasionAction.Backstep, p), p);
            Assert.Greater(EvasionPlanner.TargetDistance(EvasionAction.Flee, p),
                           EvasionPlanner.TargetDistance(EvasionAction.Backstep, p));
        }
    }
}
