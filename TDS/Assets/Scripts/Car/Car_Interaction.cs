using UnityEngine;

public class Car_Interaction : Interactable
{
    private Car_HealthController carHealthController;
    private Car_Controller carController;
    private Transform player;
    private Player playerComp;

    private float defaultPlayerScale = 1f;

    [Header("Exit details")]
    [SerializeField] private float exitCheckRadius = .2f;
    [SerializeField] private Transform[] exitPoints;
    [SerializeField] private LayerMask whatToIngoreForExit;

    private void Start()
    {
        carHealthController = GetComponent<Car_HealthController>();
        carController = GetComponent<Car_Controller>();

        foreach (var point in exitPoints)
        {
            var mr = point.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;
            var sc = point.GetComponent<SphereCollider>();
            if (sc != null) sc.enabled = false;
        }
    }

    // 현재 아키텍처 호환: GameManager가 있으면(SampleScene) 그 플레이어, 없으면(맵) 태그 "Player"로 해석.
    private Player ResolvePlayer()
    {
        if (GameManager.instance != null && GameManager.instance.player != null)
            return GameManager.instance.player;
        var go = GameObject.FindWithTag("Player");
        return go != null ? go.GetComponent<Player>() : null;
    }

    public override void Interaction()
    {
        base.Interaction();
        GetIntoTheCar();
    }

    private void GetIntoTheCar()
    {
        playerComp = ResolvePlayer();
        if (playerComp == null)
            return;
        player = playerComp.transform;

        ControlsManager.instance.SwitchToCarControls();
        carHealthController.UpdateCarHealthUI(); // UI 없으면 내부에서 no-op
        carController.ActivateCar(true);

        defaultPlayerScale = player.localScale.x;
        if (weaponController != null)
            weaponController.ShowTacticalLight(false);

        // 플레이어를 차량에 태움 — CameraFollow(태그 "Player")가 따라오므로 카메라가 자동으로 차량 추적.
        player.localScale = new Vector3(0.01f, 0.01f, 0.01f);
        player.parent = transform;
        player.localPosition = Vector3.up / 2;

        if (CameraManager.instance != null) // SampleScene 경로(맵은 parent+CameraFollow로 충분)
            CameraManager.instance.ChangeCameraTarget(transform, 12, .5f);

        EnsureDamageProxy(); // 몬스터가 차를 공격하도록 Player 레이어 히트박스
    }

    private GameObject damageProxy;

    private void EnsureDamageProxy()
    {
        if (damageProxy != null) { damageProxy.SetActive(true); return; }

        damageProxy = new GameObject("CarDamageProxy");
        int playerLayer = LayerMask.NameToLayer("Player");
        damageProxy.layer = playerLayer >= 0 ? playerLayer : gameObject.layer;
        damageProxy.transform.SetParent(transform, false);
        damageProxy.transform.localPosition = Vector3.up * 0.6f;
        var box = damageProxy.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(2.6f, 1.6f, 5.0f); // 차체를 넉넉히 덮음
        damageProxy.AddComponent<CarDamageProxy>().Init(carHealthController);
    }

    private void RemoveDamageProxy()
    {
        if (damageProxy != null) Destroy(damageProxy);
        damageProxy = null;
    }

    public void GetOutOfTheCar()
    {
        if (carController.carActive == false)
            return;

        RemoveDamageProxy(); // 차 떠나면 더 이상 차를 공격 대상으로 안 둠
        carController.ActivateCar(false);

        if (weaponController != null)
            weaponController.ShowTacticalLight(true);

        if (player != null)
        {
            player.parent = null;
            player.position = GetExitPoint();
            player.localScale = new Vector3(defaultPlayerScale, defaultPlayerScale, defaultPlayerScale);
        }

        ControlsManager.instance.SwitchToCharacterControls();

        if (CameraManager.instance != null && playerComp != null && playerComp.aim != null)
            CameraManager.instance.ChangeCameraTarget(playerComp.aim.GetAimCameraTarget(), 8.5f);
    }

    private Vector3 GetExitPoint()
    {
        var pts = new Vector3[exitPoints.Length];
        for (int i = 0; i < exitPoints.Length; i++)
            pts[i] = exitPoints[i].position;

        return TDS.Core.CarExit.TryPickClearPoint(pts, IsExitClear, out var r) ? r : transform.position;
    }

    private bool IsExitClear(Vector3 point)
    {
        Collider[] colliders = Physics.OverlapSphere(point, exitCheckRadius, ~whatToIngoreForExit);
        return colliders.Length == 0;
    }

    private void OnDrawGizmos()
    {
        if (exitPoints.Length > 0)
        {
            foreach (var point in exitPoints)
            {
                Gizmos.DrawWireSphere(point.position, exitCheckRadius);
            }
        }
    }
}
