using UnityEngine;

/// <summary>
/// 전장의 안개 비주얼 — 맵 전체에 대해 "이 지면 지점이 보이나"(FieldOfView 재사용, 콘+사거리+가림)를
/// 텍셀에 구워 가시성 마스크 Texture2D를 만든다. 글로벌 셰이더 변수로 노출하고, 지면 위 fog 쿼드
/// (TDS/VisionFog)가 샘플해 시야 밖을 회색으로 덮는다. (적 숨김은 FieldOfView가 별도 처리.)
/// </summary>
[RequireComponent(typeof(FieldOfView))]
public class VisionMask : MonoBehaviour
{
    [SerializeField] private int resolution = 96;
    [SerializeField] private Vector2 worldCenter = Vector2.zero;
    [SerializeField] private float worldSize = 80f;
    [SerializeField] private float updateInterval = 0.07f;
    [SerializeField] private float fogQuadHeight = 0.05f;

    private FieldOfView fov;
    private Texture2D mask;
    private Color32[] pixels;
    private Color32[] blurBuffer;
    private float nextUpdate;
    private GameObject fogQuad;

    private static readonly int MaskId = Shader.PropertyToID("_VisionMask");
    private static readonly int CenterSizeId = Shader.PropertyToID("_VisionMaskCenterSize");

    public Texture2D Mask => mask; // 검증용

    /// <summary>월드 XZ → 마스크 텍셀 좌표(검증/샘플용).</summary>
    public Vector2Int WorldToTexel(Vector3 world)
    {
        float u = (world.x - worldCenter.x) / worldSize + 0.5f;
        float v = (world.z - worldCenter.y) / worldSize + 0.5f;
        return new Vector2Int(
            Mathf.Clamp(Mathf.FloorToInt(u * resolution), 0, resolution - 1),
            Mathf.Clamp(Mathf.FloorToInt(v * resolution), 0, resolution - 1));
    }

    private void Awake()
    {
        fov = GetComponent<FieldOfView>();

        mask = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        pixels = new Color32[resolution * resolution];
        blurBuffer = new Color32[resolution * resolution];

        Shader.SetGlobalTexture(MaskId, mask);
        Shader.SetGlobalVector(CenterSizeId, new Vector4(worldCenter.x, worldCenter.y, worldSize, worldSize));

        CreateFogQuad();
        Compute();
    }

    private void CreateFogQuad()
    {
        var shader = Shader.Find("TDS/VisionFog");
        if (shader == null)
        {
            Debug.LogWarning("[VisionMask] 'TDS/VisionFog' 셰이더를 못 찾음 — fog 쿼드 생략.");
            return;
        }

        fogQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        fogQuad.name = "VisionFogQuad";
        var col = fogQuad.GetComponent<Collider>();
        if (col != null) Destroy(col);

        fogQuad.transform.position = new Vector3(worldCenter.x, fogQuadHeight, worldCenter.y);
        fogQuad.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // XY 쿼드 → 바닥(XZ)
        fogQuad.transform.localScale = new Vector3(worldSize, worldSize, 1f);

        var mr = fogQuad.GetComponent<MeshRenderer>();
        mr.sharedMaterial = new Material(shader);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    private void LateUpdate()
    {
        if (fov == null || mask == null || Time.time < nextUpdate)
            return;
        nextUpdate = Time.time + updateInterval;
        Compute();
    }

    private void Compute()
    {
        for (int y = 0; y < resolution; y++)
        {
            float wz = worldCenter.y + ((y + 0.5f) / resolution - 0.5f) * worldSize;
            for (int x = 0; x < resolution; x++)
            {
                float wx = worldCenter.x + ((x + 0.5f) / resolution - 0.5f) * worldSize;
                byte v = fov.IsVisible(new Vector3(wx, 0f, wz)) ? (byte)255 : (byte)0;
                pixels[y * resolution + x] = new Color32(v, v, v, 255);
            }
        }
        Blur(); // 가시성 경계를 부드럽게(블록 2D 느낌 완화)
        mask.SetPixels32(pixels);
        mask.Apply(false);
    }

    // 3x3 박스 블러(버퍼 재사용, 할당 없음).
    private void Blur()
    {
        System.Array.Copy(pixels, blurBuffer, pixels.Length);
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int sum = 0, cnt = 0;
                for (int dy = -1; dy <= 1; dy++)
                {
                    int ny = y + dy;
                    if (ny < 0 || ny >= resolution) continue;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx;
                        if (nx < 0 || nx >= resolution) continue;
                        sum += blurBuffer[ny * resolution + nx].r;
                        cnt++;
                    }
                }
                byte v = (byte)(sum / cnt);
                pixels[y * resolution + x] = new Color32(v, v, v, 255);
            }
        }
    }

    private void OnDestroy()
    {
        if (fogQuad != null) Destroy(fogQuad);
    }
}
