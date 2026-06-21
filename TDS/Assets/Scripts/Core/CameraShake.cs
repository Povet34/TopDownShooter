using UnityEngine;

namespace TDS.Core
{
    /// <summary>
    /// 트라우마 기반 카메라 셰이크 순수 모델(유니티 수학만 사용, 씬 비의존).
    /// 이벤트가 trauma를 누적, 시간이 감쇠. 흔들림은 trauma²에 비례(약하면 거의 0, 강하면 큼).
    /// 글루(CameraFollow)가 매 프레임 Tick 오프셋을 카메라 위치에 더한다.
    /// </summary>
    public class CameraShake
    {
        public float maxOffset = 0.5f;      // 위치 흔들림 진폭(월드 단위)
        public float maxAngle = 3f;         // 롤 흔들림(도)
        public float decayPerSecond = 1.6f; // trauma 감쇠율
        public float frequency = 22f;       // 노이즈 주파수

        private float trauma;
        private float time;

        public float Trauma => trauma;

        /// <summary>피해/사망/폭발 등에서 trauma 누적. [0,1]로 clamp.</summary>
        public void AddTrauma(float amount) => trauma = Mathf.Clamp01(trauma + amount);

        /// <summary>dt(권장: unscaled)로 갱신하고 위치 오프셋을 반환. trauma는 감쇠.</summary>
        public Vector3 Tick(float dt)
        {
            time += dt;
            trauma = Mathf.Max(0f, trauma - decayPerSecond * dt);

            float shake = trauma * trauma;
            float ox = Noise(0) * maxOffset * shake;
            float oz = Noise(1) * maxOffset * shake;
            return new Vector3(ox, 0f, oz);
        }

        /// <summary>현재 롤(Z 회전, 도). Tick 이후 같은 프레임에 조회.</summary>
        public float RotationZ()
        {
            float shake = trauma * trauma;
            return Noise(2) * maxAngle * shake;
        }

        // 결정적 [-1,1] 노이즈 (채널별 다른 시드)
        private float Noise(int channel) => Mathf.PerlinNoise(channel * 100f, time * frequency) * 2f - 1f;
    }
}
