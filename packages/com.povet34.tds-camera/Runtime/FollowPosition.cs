using UnityEngine;

namespace TDS.Core
{
    /// <summary>
    /// 카메라 추적 위치 계산(순수, 테스트 가능). 프레임률 독립 지수 감쇠 보간.
    /// </summary>
    public static class FollowPosition
    {
        public static Vector3 Resolve(Vector3 targetPos, Vector3 offset, Vector3 current, float smooth, float dt)
        {
            Vector3 desired = targetPos + offset;
            if (smooth <= 0f || dt <= 0f)
                return desired;

            float t = 1f - Mathf.Exp(-smooth * dt);
            return Vector3.Lerp(current, desired, t);
        }
    }
}
