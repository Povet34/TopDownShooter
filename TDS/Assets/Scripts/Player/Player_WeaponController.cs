using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_WeaponController : MonoBehaviour
{
    [SerializeField] private LayerMask whatIsAlly;
    [Space]
    private Player player;
    private const float REFERENCE_BULLET_SPEED = 20;
    //This is the default speed from whcih our mass formula is derived.

    [SerializeField] private List<Weapon_Data> defaultWeaponData;
    [SerializeField] private Weapon currentWeapon;
    private bool weaponReady;
    private bool isShooting;

    [Header("Bullet details")]
    [SerializeField] private float bulletImpactForce = 100;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float bulletSpeed;
    [SerializeField] private Light fireEffectLight;
    [Tooltip("이동 중 사격 탄퍼짐 페널티 배수(§MovingSpread). 전속 이동 시 탄퍼짐 = 기본×(1+이 값).")]
    [SerializeField] private float movingSpreadPenalty = 2f;

    /// <summary>지금 사격 중인가(이동 감속 등에 사용).</summary>
    public bool IsShooting() => isShooting;


    [SerializeField] private Transform weaponHolder;
    [SerializeField] private Light tacticalLight;

    [Header("Inventory")]


    [SerializeField] private int maxSlots = 2;
    [SerializeField] private List<Weapon> weaponSlots;

    [SerializeField] private GameObject weaponPickupPrefab;

    private void Start()
    {
        player = GetComponent<Player>();
        AssignInputEvents();
    }

    private void Update()
    {
        if (isShooting)
            Shoot();
    }

    #region Slots managment - Pickup\Equip\Drop\Ready Weapon

    public void SetDefaultWeapon(List<Weapon_Data> newWeaponData)
    {
        defaultWeaponData = new List<Weapon_Data>(newWeaponData);
        weaponSlots.Clear();

        foreach(Weapon_Data weaponData in defaultWeaponData)
        {
            PickupWeapon(new Weapon(weaponData));
        }

        EquipWeapon(0);
    }

    /// <summary>
    /// 영구 업그레이드(Firepower/Munitions)를 모든 무기에 반영. 각 무기의 기본값(weaponData)에서
    /// 다시 계산해 적용 → 재호출/구매 시에도 중복 합산되지 않는다. SO 자산은 건드리지 않음(런타임 Weapon만).
    /// </summary>
    public void ApplyUpgradeBonuses(int bonusDamage, int bonusReserve)
    {
        if (weaponSlots == null) return;
        foreach (var w in weaponSlots)
        {
            if (w == null || w.weaponData == null) continue;
            w.bulletDamage = w.weaponData.bulletDamage + Mathf.Max(0, bonusDamage);
            w.totalReserveAmmo = w.weaponData.totalReserveAmmo + Mathf.Max(0, bonusReserve);
        }
    }
    private void EquipWeapon(int i)
    {
        if (i >= weaponSlots.Count)
            return;

        SetWeaponReady(false);

        currentWeapon = weaponSlots[i];
        player.weaponVisuals.PlayWeaponEquipAnimation();

        //CameraManager.instance.ChangeCameraDistance(currentWeapon.cameraDistance);

        UpdateWeaponUI();
    }

    public void PickupWeapon(Weapon newWeapon)
    {
        if (WeaponInSlots(newWeapon.weaponType) != null)
        {
            WeaponInSlots(newWeapon.weaponType).totalReserveAmmo += newWeapon.bulletsInMagazine;
            return;
        }

        if (weaponSlots.Count >= maxSlots && newWeapon.weaponType != currentWeapon.weaponType)
        {
            int weaponIndex = weaponSlots.IndexOf(currentWeapon);

            player.weaponVisuals.SwitchOffWeaponModels();
            weaponSlots[weaponIndex] = newWeapon;

            CreateWeaponOnTheGround();
            EquipWeapon(weaponIndex);
            return;
        }

        weaponSlots.Add(newWeapon);
        player.weaponVisuals.SwitchOnBackupWeaponModel();

        UpdateWeaponUI();
    }
    private void DropWeapon()
    {
        if (HasOnlyOneWeapon())
            return;


        CreateWeaponOnTheGround();

        weaponSlots.Remove(currentWeapon);
        EquipWeapon(0);
    }

    private void CreateWeaponOnTheGround()
    {
        GameObject droppedWeapon = ObjectPool.instance.GetObject(weaponPickupPrefab, transform);
        droppedWeapon.GetComponent<Pickup_Weapon>()?.SetupPickupWeapon(currentWeapon, transform);
    }

    public void SetWeaponReady(bool ready)
    {
        weaponReady = ready;

        if(ready)
            player.sound.weaponReady.Play();
    }
    public bool WeaponReady() => weaponReady;

    #endregion

    public void ShowTacticalLight(bool isOn)
    {
        tacticalLight.gameObject.SetActive(isOn);
    }

    public void UpdateWeaponUI()
    {
        if (UI.instance == null) // UI 없는 컨텍스트(맵 단독)에서도 안전
            return;

        UI.instance.inGameUI.UpdateWeaponUI(weaponSlots, currentWeapon);
    }

    private IEnumerator BurstFire()
    {
        SetWeaponReady(false);

        for (int i = 1; i <= currentWeapon.bulletsPerShot; i++)
        {
            FireSingleBullet();

            yield return new WaitForSeconds(currentWeapon.burstFireDelay);

            if (i >= currentWeapon.bulletsPerShot)
                SetWeaponReady(true);
        }
    }

    private void Shoot()
    {
        if (WeaponReady() == false)
            return;

        if (currentWeapon.CanShoot() == false)
            return;

        player.weaponVisuals.PlayFireAnimation();

        if (currentWeapon.shootType == ShootType.Single)
            isShooting = false;

        if (currentWeapon.BurstActivated() == true)
        {
            StartCoroutine(BurstFire());
            return;
        }


        FireSingleBullet();
        TriggerEnemyDodge();
    }

    private void FireSingleBullet()
    {
        currentWeapon.bulletsInMagazine--;
        UpdateWeaponUI();

        player.weaponVisuals.CurrentWeaponModel().fireSFX.Play();
        StartCoroutine(ShowFireEffectLight());

        GameObject newBullet = ObjectPool.instance.GetObject(bulletPrefab,GunPoint());

        newBullet.transform.rotation = Quaternion.LookRotation(GunPoint().forward);

        Rigidbody rbNewBullet = newBullet.GetComponent<Rigidbody>();

        Bullet bulletScript = newBullet.GetComponent<Bullet>();
        bulletScript.BulletSetup(whatIsAlly,currentWeapon.bulletDamage, currentWeapon.gunDistance,bulletImpactForce, emitImpactNoise: true); // 플레이어 총알만 피격음 발신


        // 이동 중 사격 페널티: 빠를수록 탄퍼짐↑(§MovingSpread).
        float moveSpeed = player.movement != null ? player.movement.CurrentPlanarSpeed : 0f;
        float maxSpeed = player.movement != null ? player.movement.MaxSpeed : 1f;
        float spreadMult = TDS.Core.MovingSpread.SpreadMultiplier(moveSpeed, maxSpeed, movingSpreadPenalty);
        Vector3 bulletsDirection = currentWeapon.ApplySpread(BulletDirection(), spreadMult);

        rbNewBullet.mass = REFERENCE_BULLET_SPEED / currentWeapon.weaponData.bulletSpeed;
        rbNewBullet.linearVelocity = bulletsDirection * currentWeapon.weaponData.bulletSpeed;

        // §6.2.1 소음: 발포음(테이블 35m)은 주변 적이 플레이어를 조사하게 만든다(뒤돌아 있어도).
        NoisePing.EmitGunshot(player.transform.position);
    }

    private IEnumerator ShowFireEffectLight()
    {
        if (fireEffectLight.gameObject.activeSelf)
            yield break;

        fireEffectLight.gameObject.SetActive(true);
        yield return new WaitForSeconds(0.05f);
        fireEffectLight.gameObject.SetActive(false);
    }

    private void Reload()
    {
        SetWeaponReady(false);
        player.weaponVisuals.PlayReloadAnimation();

        player.weaponVisuals.CurrentWeaponModel().realodSfx.Play();

        // We do actuall refill of bullets in Playe_AnimationEvents
        // We UpdateWeaponUI in Player_AnimationEvents
    }


    public Vector3 BulletDirection()
    {
        Transform aim = player.aim.Aim();
        Transform gunPoint = GunPoint();

        // 정밀조준이면 3D, 아니면 수평. 어느 쪽이든 조준점이 총구와 겹치면 총구 전방으로 대체(랜덤 발사 방지).
        if (player.aim.CanAimPrecisly())
            return TDS.Core.AimDirection.Resolve(gunPoint.position, aim.position, gunPoint.forward);

        return TDS.Core.AimDirection.ResolveHorizontal(gunPoint.position, aim.position, gunPoint.forward);
    }

    public bool HasOnlyOneWeapon() => weaponSlots.Count <= 1;
    public Weapon WeaponInSlots(WeaponType weaponType)
    {
        foreach (Weapon weapon in weaponSlots)
        {
            if (weapon.weaponType == weaponType)
                return weapon;
        }

        return null;
    }
    public Weapon CurrentWeapon() => currentWeapon;
    public Transform GunPoint() => player.weaponVisuals.CurrentWeaponModel().gunPoint;

    private void TriggerEnemyDodge()
    {
        Vector3 rayOrigin = GunPoint().position;
        Vector3 rayDirection = BulletDirection();

        if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, Mathf.Infinity))
        {
            Enemy_Melee enemy_Melee = hit.collider.gameObject.GetComponentInParent<Enemy_Melee>();

            if (enemy_Melee != null)
                enemy_Melee.ActivateDodgeRoll();
        }
    }

    #region Input Events

    private void AssignInputEvents()
    {
        PlayerControls controls = player.controls;

        controls.Character.Fire.performed += context => isShooting = true;
        controls.Character.Fire.canceled += context => isShooting = false;

        controls.Character.EquipSlot1.performed += context => EquipWeapon(0);
        controls.Character.EquipSlot2.performed += context => EquipWeapon(1);
        controls.Character.EquipSlot3.performed += context => EquipWeapon(2);
        controls.Character.EquipSlot4.performed += context => EquipWeapon(3);
        controls.Character.EquipSlot5.performed += context => EquipWeapon(4);

        controls.Character.DropCurrentWeapon.performed += context => DropWeapon();

        controls.Character.Reload.performed += context =>
        {
            if (currentWeapon != null && currentWeapon.CanReload() && WeaponReady())
            {
                Reload();
            }
        };

        controls.Character.ToogleWeaponMode.performed += context => { if (currentWeapon != null) currentWeapon.ToggleBurst(); };

    }



    #endregion
}
