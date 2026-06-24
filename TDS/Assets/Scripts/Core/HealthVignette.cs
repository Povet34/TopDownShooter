using UnityEngine;

namespace TDS.Core
{
    /// <summary>
    /// 저체력 화면 비네트 강도(순수, 테스트 가능). 체력 비율이 <paramref name="startRatio"/> 이하로
    /// 떨어지면 0→1로 차오른다. 글루(LowHealthVignette)가 펄스와 곱해 빨간 가장자리 알파에 적용.
    /// </summary>
    public static class HealthVignette
    {
        public static float Intensity(int currentHp, int maxHp, float startRatio = 0.35f)
        {
            if (maxHp <= 0 || startRatio <= 0f) return 0f;
            float ratio = Mathf.Clamp01((float)currentHp / maxHp);
            if (ratio >= startRatio) return 0f;
            return Mathf.Clamp01(1f - ratio / startRatio); // startRatio에서 0, 0체력에서 1
        }

        /// <summary>심장박동 펄스(0..1 사인). min~1 사이로 매핑해 강도와 곱한다.</summary>
        public static float Pulse(float time, float speed, float min = 0.6f)
        {
            float s = Mathf.Sin(time * speed) * 0.5f + 0.5f; // 0..1
            return Mathf.Lerp(min, 1f, s);
        }
    }
}
