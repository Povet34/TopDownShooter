using UnityEngine;

namespace TDS.Core
{
    /// <summary>
    /// 대상(기본 태그 "Player")을 부드럽게 추적하는 카메라(Phase 0.2.4).
    /// 대상을 못 찾으면 매 프레임 재탐색 → 스폰된 플레이어에 자동 연결된다.
    /// 추적 base는 내부에서 따로 들고, 카메라 셰이크 오프셋을 그 위에 더한다(피드백 방지).
    /// </summary>
    [DisallowMultipleComponent]
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private string targetTag = "Player";
        [SerializeField] private Vector3 offset = new Vector3(0f, 13f, -7f);
        [SerializeField] private float smooth = 6f;
        [SerializeField] private float lookAtHeight = 1f;

        private Transform target;
        private readonly CameraShake shake = new CameraShake();
        private Vector3 basePos;
        private bool hasBase;

        /// <summary>전투 피드백이 호출. 셰이크 trauma 누적.</summary>
        public void AddTrauma(float amount) => shake.AddTrauma(amount);

        private void LateUpdate()
        {
            if (target == null)
            {
                var go = GameObject.FindWithTag(targetTag);
                if (go == null) return;
                target = go.transform;
            }

            if (!hasBase)
            {
                basePos = transform.position;
                hasBase = true;
            }

            // 추적 lerp는 셰이크 없는 base에 대해 수행(셰이크가 추적에 피드백되지 않도록)
            basePos = FollowPosition.Resolve(target.position, offset, basePos, smooth, Time.deltaTime);

            // 셰이크는 히트스톱(정지) 중에도 흔들리도록 unscaled 시간 사용
            Vector3 shakeOffset = shake.Tick(Time.unscaledDeltaTime);
            transform.position = basePos + shakeOffset;
            transform.LookAt(target.position + Vector3.up * lookAtHeight);
            transform.Rotate(0f, 0f, shake.RotationZ(), Space.Self); // 롤 흔들림
        }
    }
}
