namespace TDS.Core
{
    public enum RangedEngageAction { Hold, TakeCover, Reposition }

    /// <summary>
    /// 원거리 적의 교전 중 행동 결정(순수). 피격당하거나(threatened) 위험(시야/근접, inDanger)하면
    /// 가능하고 적절한 엄폐가 있으면 엄폐를, 없는데 피격 중이면 재배치(BattleMover)를, 그 외엔 제자리 사격.
    /// </summary>
    public static class RangedEngageDecision
    {
        public static RangedEngageAction Decide(bool threatened, bool inDanger, bool coverAllowed, bool coverAvailable)
        {
            bool wantsCover = threatened || inDanger;

            if (wantsCover && coverAllowed && coverAvailable)
                return RangedEngageAction.TakeCover;

            if (threatened)
                return RangedEngageAction.Reposition;

            return RangedEngageAction.Hold;
        }
    }
}
