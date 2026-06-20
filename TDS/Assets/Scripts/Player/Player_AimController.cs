using System;
using UnityEngine;

public class Player_AimController : MonoBehaviour
{
    private Player player;
    private PlayerControls controls;

    [Header("Aim Viusal - Laser")]
    [SerializeField] private LayerMask laserLayerMask; // ���� �ݶ��̴� �ɷ�����, ���̴� �޽��鸸 ó���Ϸ���
    [SerializeField] private LineRenderer aimLaser; // this component is on the waepon holder(child of a player)
    [SerializeField] private Transform aimLaserEnd; //�̰� sprite�� hit normal 

    [Header("Aim Control")]
    [SerializeField] float preciseAimCamDist = 6;
    [SerializeField] float regularAimCamDist = 7;
    [SerializeField] float camChangeRate = 5;

    [Space]
    [Header("Aim Setup")]
    [SerializeField] private Transform aim;
    [SerializeField] private bool isAimingPrecisly;
    [SerializeField] float offsetChangeRate = 6;
    float offsetY;

    [Header("Aim Layer")]
    [SerializeField] private LayerMask preciseAim;
    [SerializeField] private LayerMask regularAim;

    [Header("Camera control")]
    [SerializeField] private Transform cameraTarget;
    [Range(.5f, 1)]
    [SerializeField] private float minCameraDistance = 1.5f;
    [Range(1, 3f)]
    [SerializeField] private float maxCameraDistance = 4;
    [Range(3f, 5f)]
    [SerializeField] private float cameraSensetivity = 5f;

    [Space]

    private Vector2 mouseInput;
    private RaycastHit lastKnownMouseHit;

    private void Start()
    {
        player = GetComponent<Player>();
        AssignInputEvents();

        Cursor.visible = false;
    }
    private void Update()
    {
        if (player.health.isDead)
            return;

        if (player.controlsEnabled == false)
            return;

        UpdateAimVisuals();
        UpdateAimPosition();
        UpdateCameraPosition();
    }

    private void EnablePrecisesAim(bool enable)
    {
        isAimingPrecisly = !isAimingPrecisly;
        Cursor.visible = false;

        if(enable)
        {
            CameraManager.instance.ChangeCameraDistance(preciseAimCamDist, camChangeRate);
            Time.timeScale = 0.9f;
        }
        else
        {
            CameraManager.instance.ChangeCameraDistance(regularAimCamDist, camChangeRate);
            Time.timeScale = 1f;
        }
    }


    public Transform GetAimCameraTarget()
    {
        cameraTarget.position = player.transform.position;
        return cameraTarget;
    }
    public void EnableAimLaer(bool enable) => aimLaser.enabled = enable;
    private void UpdateAimVisuals()
    {
        aim.transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward);
        aimLaser.enabled = player.weapon.WeaponReady();

        if (aimLaser.enabled == false)
            return;


        WeaponModel weaponModel = player.weaponVisuals.CurrentWeaponModel();

        weaponModel.transform.LookAt(aim);
        weaponModel.gunPoint.LookAt(aim);


        Transform gunPoint = player.weapon.GunPoint();
        Vector3 laserDirection = player.weapon.BulletDirection();

        float laserTipLenght = .5f;
        float gunDistance = player.weapon.CurrentWeapon().gunDistance;

        Vector3 endPoint = gunPoint.position + laserDirection * gunDistance;

        if (Physics.Raycast(gunPoint.position, laserDirection, out RaycastHit hit, gunDistance, laserLayerMask))
        {
            endPoint = hit.point;
            laserTipLenght = 0;
        }

        aimLaser.SetPosition(0, gunPoint.position);
        aimLaser.SetPosition(1, endPoint);
        aimLaser.SetPosition(2, endPoint + laserDirection * laserTipLenght);

        aimLaserEnd.transform.position = endPoint;
        aimLaserEnd.transform.forward = hit.normal;
    }
    private void UpdateAimPosition()
    {
        aim.position = GetMouseHitInfo().point;

        Vector3 newPosition = isAimingPrecisly ? aim.position : transform.position;
        aim.position = new Vector3(aim.position.x, newPosition.y + AdjustedOffsetY(), aim.position.z);
    }

    float AdjustedOffsetY()
    {
        if(isAimingPrecisly)
        {
            offsetY = Mathf.Lerp(offsetY, 1f, offsetChangeRate * Time.deltaTime * 0.5f);
        }
        else
        {
            offsetY = Mathf.Lerp(offsetY, 0f, offsetChangeRate * Time.deltaTime);
        }
        return offsetY;
    }

    public Transform Aim() => aim;
    public bool CanAimPrecisly() => isAimingPrecisly;
    public RaycastHit GetMouseHitInfo()
    {
        if (Camera.main == null) // 메인 카메라 없는 컨텍스트에서도 안전
            return lastKnownMouseHit;

        Ray ray = Camera.main.ScreenPointToRay(mouseInput);

        if (Physics.Raycast(ray, out var hitInfo, Mathf.Infinity, preciseAim))
        {
            lastKnownMouseHit = hitInfo;
            return hitInfo;
        }

        return lastKnownMouseHit;
    }

    #region Camera Region

    private void UpdateCameraPosition()
    {
        if(Vector3.Distance(cameraTarget.position, DesieredCameraPosition()) < .5f)
            return;

        cameraTarget.position = Vector3.Lerp(cameraTarget.position, DesieredCameraPosition(), cameraSensetivity * Time.deltaTime);


    }

    private Vector3 DesieredCameraPosition()
    {
        float actualMaxCameraDistance = player.movement.moveInput.y < -.5f ? minCameraDistance : maxCameraDistance;

        Vector3 desiredCameraPosition = GetMouseHitInfo().point;
        Vector3 aimDirection = (desiredCameraPosition - transform.position).normalized;

        float distanceToDesierdPosition = Vector3.Distance(transform.position, desiredCameraPosition);
        float clampedDistance = Mathf.Clamp(distanceToDesierdPosition, minCameraDistance, actualMaxCameraDistance);

        desiredCameraPosition = transform.position + aimDirection * clampedDistance;
        desiredCameraPosition.y = transform.position.y + 1;

        return desiredCameraPosition;
    }

    #endregion

    private void AssignInputEvents()
    {
        controls = player.controls;


        controls.Character.PreciseAim.performed += context => EnablePrecisesAim(true);
        controls.Character.PreciseAim.canceled += context => EnablePrecisesAim(false);

        controls.Character.Aim.performed += context => mouseInput = context.ReadValue<Vector2>();
        controls.Character.Aim.canceled += context => mouseInput = Vector2.zero;
    }

}
