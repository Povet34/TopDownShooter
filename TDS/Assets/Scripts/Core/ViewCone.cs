using UnityEngine;

namespace TDS.Core
{
    /// <summary>
    /// 시야 콘 판정(순수). 대상이 보는 사람의 전방 콘(반각 halfAngleDeg) + 사거리 range 안인가.
    /// 수평(XZ)만 본다. 가림(occlusion)은 별도 레이캐스트.
    /// </summary>
    public static class ViewCone
    {
        public static bool InView(Vector3 viewerPos, Vector3 viewerForward, Vector3 targetPos,
                                  float halfAngleDeg, float range)
        {
            Vector3 to = targetPos - viewerPos; to.y = 0f;
            Vector3 fwd = viewerForward; fwd.y = 0f;

            float dist = to.magnitude;
            if (dist > range)
                return false;
            if (dist < 1e-4f)
                return true; // 같은 위치
            if (fwd.sqrMagnitude < 1e-6f)
                return false; // 방향 불명

            to /= dist;
            fwd.Normalize();

            float cosA = Vector3.Dot(fwd, to);
            return cosA >= Mathf.Cos(halfAngleDeg * Mathf.Deg2Rad) - 1e-5f;
        }
    }
}
