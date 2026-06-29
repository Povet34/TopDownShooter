using UnityEngine;

namespace TDS.Core
{
    /// <summary>
    /// 적이 플레이어를 바라보며(facingForward) 이동할 때, 월드 속도를 facing 기준 로컬로 변환해
    /// 2D strafe 블렌드 파라미터 (x=우측, y=전방)을 만든다. 정지/미세이동/퇴화 facing은 (0,0).
    /// </summary>
    public static class StrafeBlend
    {
        public static Vector2 Compute(Vector3 velocity, Vector3 facingForward, float deadzone = 0.05f)
        {
            Vector3 v = velocity; v.y = 0f;
            Vector3 f = facingForward; f.y = 0f;

            if (v.magnitude < deadzone || f.sqrMagnitude < 1e-6f)
                return Vector2.zero;

            f.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, f); // y-up 좌표계에서 facing의 우측

            Vector2 local = new Vector2(Vector3.Dot(v, right), Vector3.Dot(v, f));
            return local.normalized; // 방향만(속도 크기는 블렌드와 무관)
        }
    }
}
