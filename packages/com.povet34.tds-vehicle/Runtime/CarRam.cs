using UnityEngine;

namespace TDS.Core
{
    /// <summary>
    /// 차량 들이받기 데미지 계산(순수, 테스트 가능). 최소 속도 이상일 때만, 속도에 비례해 데미지가
    /// 커지고 상한에서 멈춘다(차의 '힘'에 비례). 충돌 감지/넉백 적용은 글루(Car_Controller)가 담당.
    /// </summary>
    public static class CarRam
    {
        /// <summary>이 속도로 들이받으면 데미지를 주는가.</summary>
        public static bool CanDamage(float speed, float minSpeed) => speed >= minSpeed && speed > 0f;

        /// <summary>
        /// 최소속도 미만이면 0. 이상이면 base×(speed/minSpeed)을 [base, max]로 클램프
        /// (minSpeed에서 base, 빠를수록 ↑, max에서 포화).
        /// </summary>
        public static int DamageAt(float speed, float minSpeed, int baseDamage, int maxDamage)
        {
            if (minSpeed <= 0f) minSpeed = 0.01f;
            if (speed < minSpeed) return 0;
            int scaled = Mathf.RoundToInt(baseDamage * (speed / minSpeed));
            return Mathf.Clamp(scaled, baseDamage, Mathf.Max(baseDamage, maxDamage));
        }
    }
}
