using UnityEngine;

/// <summary>
/// 총알/특정 공격에 밀리는 오브젝트. Rigidbody를 보장하고, 넘어지지 않게 좌우 회전을 고정한다.
/// 이동하면 NavMeshObstacle(carving)이 navmesh를 갱신해 적이 새 위치를 피해 가도록 한다.
/// </summary>
[DisallowMultipleComponent]
public class Movable : MonoBehaviour
{
    [SerializeField] private float mass = 12f;
    [SerializeField] private float linearDamping = 1.5f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.mass = mass;
        rb.linearDamping = linearDamping;
        rb.angularDamping = 2f;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // 이동 시 navmesh 갱신(적이 새 위치를 피함). 정지 시에만 carve(슬라이드 중엔 비용 X).
        var obstacle = GetComponent<UnityEngine.AI.NavMeshObstacle>();
        if (obstacle == null)
            obstacle = gameObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();
        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;
    }

    public void Push(Vector3 force, Vector3 point)
    {
        if (rb != null)
            rb.AddForceAtPosition(force, point, ForceMode.Impulse);
    }
}
