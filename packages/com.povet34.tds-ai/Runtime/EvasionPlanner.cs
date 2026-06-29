namespace TDS.Core
{
    public enum EvasionAction { Hold, Strafe, Backstep, Flee }

    /// <summary>적이 가진 회피 능력(능력 게이트). 없으면 그 행동을 못 한다.</summary>
    public struct EvasionAbilities
    {
        public bool canStrafe;
        public bool canBackstep;
        public bool canFlee;
    }

    /// <summary>
    /// §12 2차: 위협(피격) 시 회피 행동 선택(순수, 능력 게이트). 선호순 = 시야 회피(스코어링이 담당) →
    /// 스트레이프(좌우 유지) → 백스텝(너무 가까우면 뒤로) → 저체력 도주(도주 플래그 적만).
    /// </summary>
    public static class EvasionPlanner
    {
        public static EvasionAction Decide(
            bool threatened, float healthFraction, EvasionAbilities ab,
            float distToTarget, float preferredDistance,
            float fleeHealthThreshold = 0.25f, float tooCloseFactor = 0.6f)
        {
            // 저체력 + 도주 가능 → 도주(최우선)
            if (ab.canFlee && healthFraction <= fleeHealthThreshold)
                return EvasionAction.Flee;

            if (!threatened)
                return EvasionAction.Hold;

            // 너무 가까우면 백스텝(거리 벌리기)
            if (ab.canBackstep && distToTarget < preferredDistance * tooCloseFactor)
                return EvasionAction.Backstep;

            if (ab.canStrafe)
                return EvasionAction.Strafe;

            return EvasionAction.Hold;
        }

        /// <summary>행동별 목표 교전거리(스트레이프=유지, 백스텝=멀리, 도주=훨씬 멀리).</summary>
        public static float TargetDistance(EvasionAction action, float preferredDistance)
        {
            switch (action)
            {
                case EvasionAction.Backstep: return preferredDistance * 1.6f;
                case EvasionAction.Flee:     return preferredDistance * 3f;
                default:                     return preferredDistance; // Strafe / Hold
            }
        }
    }
}
