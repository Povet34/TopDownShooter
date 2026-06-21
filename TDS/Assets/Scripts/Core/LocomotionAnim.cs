using UnityEngine;

namespace TDS.Core
{
    /// <summary>
    /// 이동 애니메이션 재생속도 매핑(순수). 실제 평면 속도를 기준 속도로 나눈 비율로 클립 재생속도를 정한다.
    /// navmesh가 위치를 구동하므로(root motion off) 위치엔 영향 없고 다리 속도만 바뀌어
    /// 느려질 때의 발 미끄러짐/제자리걸음을 줄인다.
    /// </summary>
    public static class LocomotionAnim
    {
        /// <param name="planarSpeed">실제 평면(XZ) 이동 속도.</param>
        /// <param name="referenceSpeed">해당 이동 상태의 기준 속도(agent.speed).</param>
        public static float PlaybackSpeed(float planarSpeed, float referenceSpeed, float min = 0.15f, float max = 1.3f)
        {
            if (referenceSpeed <= 0.001f)
                return 1f;

            return Mathf.Clamp(planarSpeed / referenceSpeed, min, max);
        }
    }
}
