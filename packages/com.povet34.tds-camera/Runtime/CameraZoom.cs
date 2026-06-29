using UnityEngine;

namespace TDS.Core
{
    /// <summary>
    /// 카메라 줌 레벨 순수 로직. 휠 입력을 누적·clamp한다. 1=기본, 작을수록 가까이(zoom in), 클수록 멀리(zoom out).
    /// CameraFollow가 offset에 이 배수를 곱해 적용한다.
    /// </summary>
    public static class CameraZoom
    {
        /// <summary>휠 위로(+scrollDelta) = 줌 인(zoom 감소). 결과를 [min,max]로 clamp.</summary>
        public static float Step(float current, float scrollDelta, float sensitivity, float min, float max)
        {
            return Mathf.Clamp(current - scrollDelta * sensitivity, min, max);
        }
    }
}
