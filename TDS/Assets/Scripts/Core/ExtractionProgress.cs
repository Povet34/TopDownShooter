using UnityEngine;

namespace TDS.Core
{
    /// <summary>
    /// 수송선 탑승(탈출) 진행도(순수, 테스트 가능). 플레이어가 탈출 존 안에 머무는 동안 시간을 쌓아
    /// requiredSeconds에 도달하면 탈출 완료. resetOnLeave면 벗어날 때 0으로(긴장감), 아니면 유지(관대).
    /// </summary>
    public class ExtractionProgress
    {
        private readonly float required;
        private readonly bool resetOnLeave;

        public float Elapsed { get; private set; }
        public bool IsComplete => Elapsed >= required;
        public float Progress01 => required <= 0f ? 1f : Mathf.Clamp01(Elapsed / required);

        public ExtractionProgress(float requiredSeconds, bool resetOnLeave = false)
        {
            required = Mathf.Max(0f, requiredSeconds);
            this.resetOnLeave = resetOnLeave;
        }

        public void Tick(float dt, bool inZone)
        {
            if (IsComplete) return;
            if (inZone) Elapsed += Mathf.Max(0f, dt);
            else if (resetOnLeave) Elapsed = 0f;
        }
    }
}
