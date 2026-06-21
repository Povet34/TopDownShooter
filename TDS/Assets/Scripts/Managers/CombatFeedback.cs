using UnityEngine;
using TDS.Core;

/// <summary>
/// 전투 피드백 글루(Systems 영속). 순수 <see cref="HitStop"/>를 Time.timeScale에 적용하고,
/// 셰이크는 <see cref="CameraFollow"/>에 위임, 피격 FX를 스폰한다. <see cref="ICombatFeedbackService"/> 등록.
/// </summary>
[DisallowMultipleComponent]
public class CombatFeedback : MonoBehaviour, ICombatFeedbackService
{
    [Header("Hit FX (CFXR 등)")]
    [SerializeField] private GameObject hitFx;
    [SerializeField] private float hitFxLifetime = 2f;

    [Header("Camera shake trauma")]
    [SerializeField] private float hitTrauma = 0.18f;
    [SerializeField] private float killTrauma = 0.42f;

    [Header("Hitstop (초, unscaled)")]
    [SerializeField] private float killHitStop = 0.06f;

    private readonly HitStop hitStop = new HitStop();
    private CameraFollow cachedCam;
    private bool wasActive;

    private void Awake()
    {
        GameServices.Registry.Register<ICombatFeedbackService>(this);
    }

    private void OnDisable()
    {
        // 정지(timeScale 0) 도중 파괴/리로드되면 0으로 남는 사고 방지
        if (Time.timeScale != 1f)
            Time.timeScale = 1f;
    }

    private void Update()
    {
        // 히트스톱 활성 구간(+해제 프레임)에서만 timeScale을 건드린다(외부 일시정지와 충돌 방지).
        if (hitStop.IsActive || wasActive)
        {
            Time.timeScale = hitStop.Tick(Time.unscaledDeltaTime);
            wasActive = hitStop.IsActive;
        }
    }

    public void ReportHit(Vector3 position, float intensity)
    {
        Shaker()?.AddTrauma(hitTrauma * Mathf.Max(0.1f, intensity));
        SpawnHitFx(position);
    }

    public void ReportKill(Vector3 position)
    {
        Shaker()?.AddTrauma(killTrauma);
        hitStop.Trigger(killHitStop);
        SpawnHitFx(position);
    }

    private CameraFollow Shaker()
    {
        if (cachedCam == null)
            cachedCam = Object.FindFirstObjectByType<CameraFollow>();
        return cachedCam;
    }

    private void SpawnHitFx(Vector3 position)
    {
        if (hitFx == null)
            return;
        var fx = Instantiate(hitFx, position, Quaternion.identity);
        Destroy(fx, hitFxLifetime);
    }
}
