using UnityEngine;

/// <summary>
/// 전장의 안개 비주얼 — "이 지면 지점이 보이나"(FieldOfView 재사용: 콘+사거리+가림)를 텍셀에 구워
/// 가시성 마스크 Texture2D를 만든다. 글로벌 셰이더 변수로 노출하고 지면 fog 쿼드(TDS/VisionFog)가
/// 샘플해 시야 밖을 회색으로 덮는다. (적 숨김은 FieldOfView가 별도 처리.)
///
/// 해상도: 맵 전체가 아니라 <b>플레이어 주변 영역(regionSize)만</b> 덮어 같은 텍셀로 훨씬 조밀하게.
/// 계산은 framesPerMask 프레임에 나눠(스파이크 방지), 중심은 텍셀 그리드에 스냅(shimmer 방지).
/// 모든 값은 Inspector에서 튜닝 가능.
/// </summary>
[RequireComponent(typeof(FieldOfView))]
public class VisionMask : MonoBehaviour
{
    [Header("마스크 해상도 / 영역")]
    [Tooltip("마스크 텍스처 한 변의 픽셀 수. 클수록 선명(비용 ↑)")]
    [SerializeField] private int resolution = 160;
    [Tooltip("플레이어 주변 마스크가 덮는 월드 크기. 작을수록 텍셀 밀도(선명도) ↑. 시야 사거리×2보다 크게")]
    [SerializeField] private float regionSize = 48f;
    [Tooltip("마스크 한 장을 몇 프레임에 나눠 계산할지(프레임 스파이크 방지). 클수록 부드럽지만 갱신 느림")]
    [Min(1)][SerializeField] private int framesPerMask = 4;
    [SerializeField] private bool followPlayer = true;
    [SerializeField] private bool blur = true;

    [Header("Fog 룩")]
    [SerializeField] private Color fogColor = new Color(0.04f, 0.05f, 0.07f, 1f);
    [Range(0f, 1f)][SerializeField] private float maxDarkness = 0.8f;
    [SerializeField] private float fogQuadHeight = 0.05f;
    [Tooltip("지면을 덮는 fog 쿼드 크기(맵+카메라 시야를 덮을 만큼 크게)")]
    [SerializeField] private float fogQuadSize = 160f;

    private FieldOfView fov;
    private Texture2D mask;
    private Color32[] pixels;
    private Color32[] blurBuffer;
    private Vector2 center;       // 현재 적용된(표시 중) 마스크 중심
    private Vector2 cycleCenter;  // 계산 진행 중 사이클의 중심
    private int computeRow;
    private GameObject fogQuad;

    private static readonly int MaskId = Shader.PropertyToID("_VisionMask");
    private static readonly int CenterSizeId = Shader.PropertyToID("_VisionMaskCenterSize");

    public Texture2D Mask => mask;

    /// <summary>월드 XZ → 현재 마스크 텍셀 좌표(검증/샘플용).</summary>
    public Vector2Int WorldToTexel(Vector3 world)
    {
        float u = (world.x - center.x) / regionSize + 0.5f;
        float v = (world.z - center.y) / regionSize + 0.5f;
        return new Vector2Int(
            Mathf.Clamp(Mathf.FloorToInt(u * resolution), 0, resolution - 1),
            Mathf.Clamp(Mathf.FloorToInt(v * resolution), 0, resolution - 1));
    }

    private Vector2 SnappedCenter()
    {
        Vector2 c = followPlayer
            ? new Vector2(transform.position.x, transform.position.z)
            : Vector2.zero;
        float texel = regionSize / resolution;
        return new Vector2(Mathf.Round(c.x / texel) * texel, Mathf.Round(c.y / texel) * texel);
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

        CreateFogQuad();

        // 초기 1회 풀 계산(즉시 마스크 준비)
        Vector2 c = SnappedCenter();
        for (int y = 0; y < resolution; y++) ComputeRow(y, c);
        Finish(c);
    }

    private void LateUpdate()
    {
        if (mask == null || fov == null) return;

        if (computeRow == 0)
            cycleCenter = SnappedCenter();

        int rows = Mathf.Max(1, Mathf.CeilToInt((float)resolution / framesPerMask));
        int end = Mathf.Min(resolution, computeRow + rows);
        for (int y = computeRow; y < end; y++)
            ComputeRow(y, cycleCenter);
        computeRow = end;

        if (computeRow >= resolution)
        {
            computeRow = 0;
            Finish(cycleCenter);
        }
    }

    private void ComputeRow(int y, Vector2 c)
    {
        float wz = c.y + ((y + 0.5f) / resolution - 0.5f) * regionSize;
        int row = y * resolution;
        for (int x = 0; x < resolution; x++)
        {
            float wx = c.x + ((x + 0.5f) / resolution - 0.5f) * regionSize;
            byte v = fov.IsVisible(new Vector3(wx, 0f, wz)) ? (byte)255 : (byte)0;
            pixels[row + x] = new Color32(v, v, v, 255);
        }
    }

    private void Finish(Vector2 c)
    {
        center = c;
        if (blur) Blur();
        mask.SetPixels32(pixels);
        mask.Apply(false);
        Shader.SetGlobalVector(CenterSizeId, new Vector4(center.x, center.y, regionSize, regionSize));
    }

    // 3x3 박스 블러(버퍼 재사용, 할당 없음) — 가시성 경계를 부드럽게.
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
                    int nrow = ny * resolution;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx;
                        if (nx < 0 || nx >= resolution) continue;
                        sum += blurBuffer[nrow + nx].r;
                        cnt++;
                    }
                }
                byte v = (byte)(sum / cnt);
                pixels[y * resolution + x] = new Color32(v, v, v, 255);
            }
        }
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

        fogQuad.transform.position = new Vector3(0f, fogQuadHeight, 0f);
        fogQuad.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // XY 쿼드 → 바닥(XZ)
        fogQuad.transform.localScale = new Vector3(fogQuadSize, fogQuadSize, 1f);

        var mr = fogQuad.GetComponent<MeshRenderer>();
        var mat = new Material(shader);
        mat.SetColor("_FogColor", fogColor);
        mat.SetFloat("_MaxDarkness", maxDarkness);
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    private void OnDestroy()
    {
        if (fogQuad != null) Destroy(fogQuad);
    }
}
