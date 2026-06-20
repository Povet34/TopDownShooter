using UnityEngine;

namespace TDS.Core
{
    /// <summary>
    /// 탑다운 조준 회전 계산(순수, 테스트 가능). 조준점이 거의 자기 위치와 같을 때
    /// <c>Quaternion.LookRotation(zero)</c>가 내뱉는 "Look rotation viewing vector is zero" 경고를
    /// 피하려고, 방향이 0에 가까우면 현재 회전을 유지한다.
    /// </summary>
    public static class AimRotation
    {
        /// <summary>from에서 aimPoint를 향하는 XZ 평면 바라보기 회전. 방향이 ~0이면 current 유지.</summary>
        public static Quaternion FaceHorizontal(Vector3 from, Vector3 aimPoint, Quaternion current, float epsilon = 1e-4f)
        {
            Vector3 dir = aimPoint - from;
            dir.y = 0f;

            if (dir.sqrMagnitude < epsilon)
                return current;

            return Quaternion.LookRotation(dir.normalized);
        }
    }
}
