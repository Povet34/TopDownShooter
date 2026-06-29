using UnityEngine;

namespace TDS.Core
{
    /// <summary>
    /// 조준 방향 순수 로직. from→aim 방향을 정규화해 돌려주되, 둘이 거의 같은 위치면(degenerate)
    /// fallback 방향으로 대체한다. 무기 LookAt / 총알 방향이 0벡터일 때 마구 회전·랜덤 발사하는 것을 막는다.
    /// </summary>
    public static class AimDirection
    {
        /// <param name="from">총구/무기 위치.</param>
        /// <param name="aim">조준점.</param>
        /// <param name="fallback">degenerate일 때 쓸 방향(보통 총구 forward).</param>
        /// <param name="minSqr">from↔aim 거리² 임계값(이하면 degenerate).</param>
        public static Vector3 Resolve(Vector3 from, Vector3 aim, Vector3 fallback, float minSqr = 0.04f)
        {
            Vector3 d = aim - from;
            if (d.sqrMagnitude < minSqr)
                return fallback.sqrMagnitude > 1e-6f ? fallback.normalized : Vector3.forward;

            return d.normalized;
        }

        /// <summary>
        /// 수평(XZ) 조준 방향. 탑다운에선 총이 땅(조준점 y)을 향해 기울면 거의 수직이 되어 LookRotation이
        /// up-모호성으로 마구 회전한다 → y를 무시하고 수평 방향만. 수평 성분이 거의 0이면(발밑) fallback.
        /// </summary>
        public static Vector3 ResolveHorizontal(Vector3 from, Vector3 aim, Vector3 fallback, float minSqr = 0.04f)
        {
            Vector3 d = aim - from; d.y = 0f;
            if (d.sqrMagnitude < minSqr)
            {
                Vector3 fb = fallback; fb.y = 0f;
                return fb.sqrMagnitude > 1e-6f ? fb.normalized : Vector3.forward;
            }
            return d.normalized;
        }

        /// <summary>
        /// 조준 타깃이 from(총/플레이어)에 너무 붙으면(발밑 조준) 리그 Aim 제약(Gun_Aim 등)이 0벡터로
        /// 마구 돌아 팔·무기가 빙글빙글 돈다 → 수평 거리를 최소 minDist로 밀어내 안정화. y는 유지.
        /// </summary>
        public static Vector3 ClampMinDistance(Vector3 from, Vector3 aim, float minDist, Vector3 fallbackForward)
        {
            Vector3 flat = aim - from; flat.y = 0f;
            float d = flat.magnitude;
            if (d >= minDist)
                return aim;

            Vector3 dir;
            if (d > 1e-4f)
                dir = flat / d;
            else
            {
                dir = fallbackForward; dir.y = 0f;
                dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;
            }

            Vector3 clamped = from + dir * minDist;
            clamped.y = aim.y;
            return clamped;
        }
    }
}
