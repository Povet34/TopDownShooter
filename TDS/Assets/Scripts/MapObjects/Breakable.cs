using UnityEngine;

/// <summary>
/// 피격 누적 시 파괴되는 오브젝트. 총알이 `IDamagable.TakeDamage`를 호출하면 체력이 깎이고,
/// 0 이하가 되면 파편을 흩뿌리며 사라진다(엄폐물이면 엄폐 기능도 함께 제거됨).
/// </summary>
[DisallowMultipleComponent]
public class Breakable : MonoBehaviour, IDamagable
{
    [SerializeField] private int maxHealth = 30;
    [SerializeField] private GameObject debrisFX;   // 있으면 사용, 없으면 간단 파편 생성
    [SerializeField] private int fragmentCount = 6;

    private int currentHealth;
    private bool broken;

    public bool IsBroken => broken;

    /// <summary>막 파괴되는 순간 1회 발생(파편/Destroy 직전). Explosive 등이 구독해 폭발을 얹는다.</summary>
    public event System.Action Broken;

    private void Awake() => currentHealth = maxHealth;

    public void TakeDamage(int damage)
    {
        if (broken)
            return;

        currentHealth = TDS.Core.BreakableHealth.Apply(currentHealth, damage, out bool broke);
        if (broke)
            Break();
    }

    private void Break()
    {
        broken = true;
        Broken?.Invoke(); // 폭발 등 부가 효과(파편/Destroy 전에 — 폭발 범위피해가 옆 배럴을 연쇄로 깸)

        if (debrisFX != null)
        {
            var fx = Instantiate(debrisFX, transform.position, Quaternion.identity);
            Destroy(fx, 3f);
        }
        else
        {
            SpawnSimpleFragments();
        }

        Destroy(gameObject);
    }

    private void SpawnSimpleFragments()
    {
        var rend = GetComponentInChildren<Renderer>();
        Vector3 size = rend != null ? rend.bounds.size : Vector3.one;
        Vector3 center = rend != null ? rend.bounds.center : transform.position;
        Material mat = rend != null ? rend.sharedMaterial : null;

        for (int i = 0; i < fragmentCount; i++)
        {
            var frag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frag.transform.position = center + Random.insideUnitSphere * 0.3f;
            frag.transform.localScale = size * 0.28f;
            if (mat != null) frag.GetComponent<Renderer>().sharedMaterial = mat;

            var rb = frag.AddComponent<Rigidbody>();
            rb.AddForce((Random.insideUnitSphere + Vector3.up) * 3f, ForceMode.Impulse);
            Destroy(frag, 2f);
        }
    }
}
