using NUnit.Framework;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class CoverEvaluationTests
    {
        [Test]
        public void Low_reachable_cover_is_shoot_from()
        {
            Assert.AreEqual(CoverSuitability.ShootFrom, CoverEvaluation.Evaluate(0.5f, true));
        }

        [Test]
        public void High_reachable_cover_is_hide_only()
        {
            Assert.AreEqual(CoverSuitability.HideOnly, CoverEvaluation.Evaluate(1.5f, true));
        }

        [Test]
        public void Unreachable_cover_is_unusable_regardless_of_height()
        {
            Assert.AreEqual(CoverSuitability.Unusable, CoverEvaluation.Evaluate(0.5f, false));
            Assert.AreEqual(CoverSuitability.Unusable, CoverEvaluation.Evaluate(1.5f, false));
        }

        [Test]
        public void Boundary_height_080_is_still_shootable()
        {
            Assert.IsTrue(CoverEvaluation.IsShootable(0.8f));
            Assert.AreEqual(CoverSuitability.ShootFrom, CoverEvaluation.Evaluate(0.8f, true));
        }

        [Test]
        public void Just_above_080_is_not_shootable()
        {
            Assert.IsFalse(CoverEvaluation.IsShootable(0.81f));
            Assert.AreEqual(CoverSuitability.HideOnly, CoverEvaluation.Evaluate(0.81f, true));
        }

        [Test]
        public void Engage_score_prefers_lower_cover()
        {
            Assert.Greater(CoverEvaluation.EngageScore(0.2f), CoverEvaluation.EngageScore(0.6f));
            Assert.AreEqual(1f, CoverEvaluation.EngageScore(0f), 1e-4f);
            Assert.AreEqual(0f, CoverEvaluation.EngageScore(0.8f), 1e-4f);
        }

        [Test]
        public void Engage_score_is_zero_above_shootable()
        {
            Assert.AreEqual(0f, CoverEvaluation.EngageScore(1.5f));
        }
    }
}
