using UnityEngine;

namespace TDS.Core
{
    /// <summary>
    /// 이동 중 사격 페널티(순수, 기획 2026-06-21). 이동하면서 쏘면 ① 이동속도 감소 ② 탄퍼짐 증가 →
    /// 정조준하려면 멈춰야 한다(킬존 압박). 글루(Player_Movement·Weapon)가 이 시임을 호출만 한다.
    /// </summary>
    public static class MovingSpread
    {
        /// <summary>
        /// 이동 정도에 따른 탄퍼짐 배수. 정지(speed 0)=1, 전속(speed≥maxSpeed)=1+maxPenalty. 선형 보간.
        /// maxSpeed≤0이면 1(페널티 없음).
        /// </summary>
        public static float SpreadMultiplier(float moveSpeed, float maxSpeed, float maxPenalty)
        {
            if (maxSpeed <= 0f)
                return 1f;
            float frac = Mathf.Clamp01(moveSpeed / maxSpeed);
            return 1f + Mathf.Max(0f, maxPenalty) * frac;
        }

        /// <summary>
        /// 사격 중 이동속도 배수(정조준하려면 멈춰야 함). 사격 중이면 slowFactor(0~1), 아니면 1.
        /// </summary>
        public static float MoveSpeedFactor(bool isShooting, float shootingSlowFactor)
            => isShooting ? Mathf.Clamp01(shootingSlowFactor) : 1f;
    }
}
