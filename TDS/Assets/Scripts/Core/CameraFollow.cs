using UnityEngine;

namespace TDS.Core
{
    /// <summary>
    /// 대상(기본 태그 "Player")을 부드럽게 추적하는 카메라(Phase 0.2.4).
    /// 대상을 못 찾으면 매 프레임 재탐색 → 스폰된 플레이어에 자동 연결된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private string targetTag = "Player";
        [SerializeField] private Vector3 offset = new Vector3(0f, 13f, -7f);
        [SerializeField] private float smooth = 6f;
        [SerializeField] private float lookAtHeight = 1f;

        private Transform target;

        private void LateUpdate()
        {
            if (target == null)
            {
                var go = GameObject.FindWithTag(targetTag);
                if (go == null) return;
                target = go.transform;
            }

            transform.position = FollowPosition.Resolve(target.position, offset, transform.position, smooth, Time.deltaTime);
            transform.LookAt(target.position + Vector3.up * lookAtHeight);
        }
    }
}
