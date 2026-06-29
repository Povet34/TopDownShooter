using UnityEngine;

namespace TDS.Core
{
    /// <summary>
    /// 전투 피드백(카메라 셰이크 · 히트스톱 · 피격 FX) 서비스. <c>CombatFeedback</c>이 구현·등록한다.
    /// 적/플레이어 등 피해 처리부에서 무기 비의존으로 호출한다.
    /// </summary>
    public interface ICombatFeedbackService
    {
        /// <summary>비치명 피격. 약한 셰이크 + 피격 FX.</summary>
        void ReportHit(Vector3 position, float intensity);

        /// <summary>사망/처치. 강한 셰이크 + 히트스톱 + FX.</summary>
        void ReportKill(Vector3 position);

        /// <summary>폭발. 거리 강도로 스케일된 강한 셰이크(가까울수록 큼). 자체 FX가 있어 피격 FX는 안 띄움.</summary>
        void ReportExplosion(Vector3 position, float intensity);
    }
}
