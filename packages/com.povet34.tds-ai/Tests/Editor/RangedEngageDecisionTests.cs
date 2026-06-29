using NUnit.Framework;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class RangedEngageDecisionTests
    {
        [Test]
        public void Threatened_with_cover_takes_cover()
        {
            // 피격 + 엄폐 가능 + 엄폐 있음 → 숨는다
            Assert.AreEqual(RangedEngageAction.TakeCover,
                RangedEngageDecision.Decide(threatened: true, inDanger: false, coverAllowed: true, coverAvailable: true));
        }

        [Test]
        public void Threatened_without_cover_repositions()
        {
            // 피격인데 엄폐가 없음 → melee식 재배치(폴백)
            Assert.AreEqual(RangedEngageAction.Reposition,
                RangedEngageDecision.Decide(threatened: true, inDanger: false, coverAllowed: true, coverAvailable: false));
        }

        [Test]
        public void Threatened_when_cover_not_allowed_repositions()
        {
            // 엄폐 perk 없음(Unavailable 등) + 피격 → 재배치
            Assert.AreEqual(RangedEngageAction.Reposition,
                RangedEngageDecision.Decide(threatened: true, inDanger: true, coverAllowed: false, coverAvailable: true));
        }

        [Test]
        public void In_danger_only_takes_cover_when_available()
        {
            // 피격은 아니지만 시야/근접 위험 → 엄폐 있으면 숨음
            Assert.AreEqual(RangedEngageAction.TakeCover,
                RangedEngageDecision.Decide(threatened: false, inDanger: true, coverAllowed: true, coverAvailable: true));
        }

        [Test]
        public void In_danger_without_cover_holds()
        {
            // 위험하지만 피격은 아니고 엄폐도 없음 → 제자리 사격(불필요한 이동 안 함)
            Assert.AreEqual(RangedEngageAction.Hold,
                RangedEngageDecision.Decide(threatened: false, inDanger: true, coverAllowed: true, coverAvailable: false));
        }

        [Test]
        public void Calm_holds()
        {
            // 안전(피격X·위험X) → 제자리
            Assert.AreEqual(RangedEngageAction.Hold,
                RangedEngageDecision.Decide(threatened: false, inDanger: false, coverAllowed: true, coverAvailable: true));
        }
    }
}
