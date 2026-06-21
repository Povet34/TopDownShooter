using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 전장의 안개 비주얼 (GPU) — 플레이어에서 360° 레이로 가시성 폴리곤 메시를 만들고(시야 콘 안은
/// 장애물까지, 밖은 nearRadius 작은 원), 그 흰색 메시를 탑다운 직교 행렬로 RenderTexture에 직접
/// 그린다(CommandBuffer — URP 카메라/데칼 파이프라인 우회). = 가시성 마스크. 지면 fog 쿼드
/// (TDS/VisionFog)가 마스크를 샘플해 시야 밖을 회색으로 덮는다.
/// CPU 텍셀별 레이캐스트(수천/프레임) 대신 레이 ~rayCount개 + 메시 1장 → 고화질·저비용.
/// (적 숨김은 FieldOfView가 별도 처리.)
/// </summary>
[RequireComponent(typeof(FieldOfView))]
public class VisionMask : MonoBehaviour
{
    [Header("마스크")]
    [SerializeField] private int rtResolution = 512;
    [SerializeField] private Vector2 worldCenter = Vector2.zero;
    [SerializeField] private float worldSize = 80f;
    [Tooltip("가시성 폴리곤 레이 수(많을수록 가림 경계 선명)")]
    [SerializeField] private int rayCount = 240;

    [Header("Fog 룩")]
    [SerializeField] private Color fogColor = new Color(0.04f, 0.05f, 0.07f, 1f);
    [Range(0f, 1f)][SerializeField] private float maxDarkness = 0.8f;
    [SerializeField] private float fogQuadHeight = 0.05f;
    [SerializeField] private float fogQuadSize = 160f;

    private FieldOfView fov;
    private RenderTexture maskRT;
    private Camera matrixCam;       // 행렬 계산용(비활성, 렌더 안 함)
    private CommandBuffer cmd;
    private Mesh visMesh;
    private Material visMat;
    private Vector3[] verts;
    private int[] tris;
    private GameObject fogQuad;

    private static readonly int MaskId = Shader.PropertyToID("_VisionMask");
    private static readonly int CenterSizeId = Shader.PropertyToID("_VisionMaskCenterSize");

    public Vector3[] DebugVerts => verts;

    private void Awake()
    {
        fov = GetComponent<FieldOfView>();

        maskRT = new RenderTexture(rtResolution, rtResolution, 0, RenderTextureFormat.ARGB32)
        {
            name = "VisionMaskRT",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        maskRT.Create();

        Shader.SetGlobalTexture(MaskId, maskRT);
        Shader.SetGlobalVector(CenterSizeId, new Vector4(worldCenter.x, worldCenter.y, worldSize, worldSize));

        SetupVisMesh();
        SetupMatrixCamera();
        SetupFogQuad();
        cmd = new CommandBuffer { name = "VisionMask" };

        BuildMesh();
        RenderMask();
    }

    private void SetupVisMesh()
    {
        visMesh = new Mesh { name = "VisMesh" };
        visMesh.MarkDynamic();

        var shader = Shader.Find("TDS/VisMesh");
        visMat = new Material(shader != null ? shader : Shader.Find("Hidden/Internal-Colored"));

        verts = new Vector3[rayCount + 1];
        tris = new int[rayCount * 3];
        for (int i = 0; i < rayCount; i++)
        {
            tris[i * 3] = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = (i + 1) % rayCount + 1;
        }
    }

    private void SetupMatrixCamera()
    {
        var go = new GameObject("VisionMatrixCam");
        go.hideFlags = HideFlags.HideAndDontSave;
        matrixCam = go.AddComponent<Camera>();
        matrixCam.enabled = false; // 렌더 안 함 — 행렬만
        matrixCam.orthographic = true;
        matrixCam.orthographicSize = worldSize * 0.5f;
        matrixCam.aspect = 1f;
        matrixCam.nearClipPlane = 0.1f;
        matrixCam.farClipPlane = 200f;
        matrixCam.transform.position = new Vector3(worldCenter.x, 100f, worldCenter.y);
        matrixCam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    private void SetupFogQuad()
    {
        var shader = Shader.Find("TDS/VisionFog");
        if (shader == null) { Debug.LogWarning("[VisionMask] 'TDS/VisionFog' 셰이더 못 찾음 — fog 쿼드 생략."); return; }

        fogQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        fogQuad.name = "VisionFogQuad";
        var col = fogQuad.GetComponent<Collider>();
        if (col != null) Destroy(col);
        fogQuad.transform.position = new Vector3(worldCenter.x, fogQuadHeight, worldCenter.y);
        fogQuad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        fogQuad.transform.localScale = new Vector3(fogQuadSize, fogQuadSize, 1f);

        var mat = new Material(shader);
        mat.SetColor("_FogColor", fogColor);
        mat.SetFloat("_MaxDarkness", maxDarkness);
        var mr = fogQuad.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    private void LateUpdate()
    {
        BuildMesh();
        RenderMask();
    }

    private void BuildMesh()
    {
        if (visMesh == null || fov == null) return;

        Vector3 player = transform.position;
        Vector3 eye = player + Vector3.up * fov.EyeHeight;
        Vector3 fwd = fov.ViewDirection; fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-6f) fwd = transform.forward;
        fwd.Normalize();

        float half = fov.CurrentHalfAngle;
        float range = fov.CurrentRange;
        float near = fov.NearRadius;
        LayerMask occ = fov.OccluderMask;
        float cosHalf = Mathf.Cos(half * Mathf.Deg2Rad);

        verts[0] = player;
        for (int i = 0; i < rayCount; i++)
        {
            float ang = (float)i / rayCount * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));

            float radius;
            if (Vector3.Dot(fwd, dir) >= cosHalf)
                radius = Physics.Raycast(eye, dir, out RaycastHit hit, range, occ, QueryTriggerInteraction.Ignore)
                    ? hit.distance : range;
            else
                radius = near;

            verts[i + 1] = player + dir * radius;
        }

        visMesh.Clear();
        visMesh.vertices = verts;
        visMesh.triangles = tris;
        visMesh.RecalculateBounds();
    }

    private void RenderMask()
    {
        if (cmd == null || maskRT == null || matrixCam == null) return;

        cmd.Clear();
        cmd.SetRenderTarget(maskRT);
        cmd.ClearRenderTarget(true, true, Color.black);
        cmd.SetViewProjectionMatrices(matrixCam.worldToCameraMatrix,
            GL.GetGPUProjectionMatrix(matrixCam.projectionMatrix, true));
        cmd.DrawMesh(visMesh, Matrix4x4.identity, visMat, 0, 0);
        Graphics.ExecuteCommandBuffer(cmd);
    }

    /// <summary>RT 마스크 값(0~1)을 월드 지점에서 읽음 — 검증/디버그용.</summary>
    public float ReadMaskAt(Vector3 world)
    {
        if (maskRT == null) return 0f;
        var prev = RenderTexture.active;
        RenderTexture.active = maskRT;
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        float u = (world.x - worldCenter.x) / worldSize + 0.5f;
        float v = (world.z - worldCenter.y) / worldSize + 0.5f;
        if (SystemInfo.graphicsUVStartsAtTop) v = 1f - v; // 셰이더 UV 보정과 동일
        int px = Mathf.Clamp(Mathf.RoundToInt(u * maskRT.width), 0, maskRT.width - 1);
        int py = Mathf.Clamp(Mathf.RoundToInt(v * maskRT.height), 0, maskRT.height - 1);
        tex.ReadPixels(new Rect(px, py, 1, 1), 0, 0);
        tex.Apply();
        float r = tex.GetPixel(0, 0).r;
        Destroy(tex);
        RenderTexture.active = prev;
        return r;
    }

    private void OnDestroy()
    {
        if (fogQuad != null) Destroy(fogQuad);
        if (matrixCam != null) Destroy(matrixCam.gameObject);
        if (cmd != null) cmd.Release();
        if (visMesh != null) Destroy(visMesh);
        if (visMat != null) Destroy(visMat);
        if (maskRT != null) { maskRT.Release(); Destroy(maskRT); }
    }
}
