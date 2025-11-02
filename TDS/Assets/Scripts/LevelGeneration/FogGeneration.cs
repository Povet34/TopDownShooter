using FischlWorks_FogWar;
using UnityEngine;

public class FogGeneration : MonoBehaviour
{
    [SerializeField] GameObject fogWarPrefab;

    private void Start()
    {
        Bounds bounds = GetCombinedWorldBounds();

        bounds.center = new Vector3(bounds.center.x, 2f, bounds.center.z);
        var fogWar = Instantiate(fogWarPrefab, bounds.center, Quaternion.identity).GetComponent<csFogWar>();

        fogWar.SetLevelDimension(Mathf.CeilToInt(bounds.size.x), Mathf.CeilToInt(bounds.size.z));


        var headtr = FindAnyObjectByType<Player>().transform.Find("Fog Head");
        fogWar.AddFogRevealer(new csFogWar.FogRevealer(headtr.transform, 10, true));
    }

    public Bounds GetCombinedWorldBounds(bool includeInactive = true, bool fallbackToColliders = true)
    {
        var renderers = GetComponentsInChildren<Renderer>(includeInactive);
        if (renderers.Length == 0 && fallbackToColliders)
        {
            var colliders = GetComponentsInChildren<Collider>(includeInactive);
            if (colliders.Length == 0)
                return new Bounds(transform.position, Vector3.zero);

            Bounds b = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
                b.Encapsulate(colliders[i].bounds);
            return b;
        }

        if (renderers.Length == 0)
            return new Bounds(transform.position, Vector3.zero);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds;
    }

    public Vector3 GetCombinedWorldCenter(bool includeInactive = true, bool fallbackToColliders = true)
        => GetCombinedWorldBounds(includeInactive, fallbackToColliders).center;

    // 로컬 좌표계 기준 바운드가 필요하면 8개 꼭짓점을 로컬로 변환해 AABB를 재계산합니다.
    public Bounds GetCombinedLocalBounds(bool includeInactive = true, bool fallbackToColliders = true)
    {
        var world = GetCombinedWorldBounds(includeInactive, fallbackToColliders);

        Vector3 ext = world.extents;
        Vector3 c = world.center;

        Vector3[] corners = new Vector3[8];
        int idx = 0;
        for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                    corners[idx++] = transform.InverseTransformPoint(c + Vector3.Scale(ext, new Vector3(x, y, z)));

        Vector3 min = corners[0];
        Vector3 max = corners[0];
        for (int i = 1; i < corners.Length; i++)
        {
            min = Vector3.Min(min, corners[i]);
            max = Vector3.Max(max, corners[i]);
        }
        return new Bounds((min + max) * 0.5f, max - min);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        var b = GetCombinedWorldBounds(true);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(b.center, b.size);
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(b.center, 0.1f);
    }
#endif
}
