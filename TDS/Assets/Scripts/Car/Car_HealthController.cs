using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Car_HealthController : MonoBehaviour, IDamagable
{
    private Car_Controller carController;

    public int maxHealth;
    public int currentHealth;

    private bool carBroken;

    [Header("Explosion info")]
    [SerializeField] int explosionDamage = 350;
    [SerializeField] ParticleSystem fireFx;
    [SerializeField] ParticleSystem explosionFx;
    [SerializeField] Transform explosionPoint;

    [Header("Spawned FX (프리팹 — 차일드 미할당 대응, CFXR 등)")]
    [Tooltip("부서질 때 차에 붙는 불 FX(CFXR Fire 등). 폭발 전까지 타오름")]
    [SerializeField] GameObject fireFxPrefab;
    [Tooltip("폭발 순간 스폰할 폭발 FX(CFXR3 Fire Explosion B 등)")]
    [SerializeField] GameObject explosionFxPrefab;

    [Space]
    [SerializeField] float explosionRadius = 3; 
    [SerializeField] float explosionDelay = 3;
    [SerializeField] float explosionForce = 7;
    [SerializeField] float explosionUpwardsModifier = 2;

    private void Start()
    {
        carController = GetComponent<Car_Controller>();
        currentHealth = maxHealth;
    }

    private void Update()
    {
        if(fireFx != null && fireFx.gameObject.activeSelf)
        {
            fireFx.transform.rotation = Quaternion.identity;
        }
    }

    public void UpdateCarHealthUI()
    {
        if (UI.instance != null) // 맵 씬엔 UI 싱글톤 없음 — MapHUD가 currentHealth를 읽어 표시
            UI.instance.inGameUI.UpdateCarHealthUI(currentHealth, maxHealth);
    }

    private void ReduceHealth(int damage)
    {
        if (carBroken)
            return;

        currentHealth -= damage;

        if (currentHealth < 0)
            BrakeTheCar();
    }

    private void BrakeTheCar()
    {
        carBroken = true;
        carController.BrakeTheCar();

        if (fireFx != null) fireFx.gameObject.SetActive(true);
        if (fireFxPrefab != null) // 차에 붙여 타오르게(폭발 시 함께 정리)
            Instantiate(fireFxPrefab, transform.position + Vector3.up * 0.6f, Quaternion.identity, transform);
        StartCoroutine(ExplosionCo(explosionDelay));
    }

    public void TakeDamage(int damage)
    {
        ReduceHealth(damage);
        UpdateCarHealthUI();
    }

    IEnumerator ExplosionCo(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (explosionFx != null) explosionFx.gameObject.SetActive(true);

        Vector3 center = explosionPoint != null ? explosionPoint.position : transform.position;
        if (explosionFxPrefab != null)
        {
            var fx = Instantiate(explosionFxPrefab, center, Quaternion.identity); // 월드(차와 함께 안 사라지게)
            Destroy(fx, 4f);
        }

        // 폭발이 잔해를 날리도록 구속 해제 + 질량 비례 임펄스(고정/직립 구속이면 안 날아감).
        if (carController != null && carController.rb != null)
        {
            carController.rb.constraints = RigidbodyConstraints.None;
            carController.rb.AddExplosionForce(explosionForce * carController.rb.mass, center,
                explosionRadius, explosionUpwardsModifier, ForceMode.Impulse);
        }

        Explode(center);
    }

    private void Explode(Vector3 center)
    {
        HashSet<GameObject> unieqEntites = new HashSet<GameObject>();
        Collider[] colliders = Physics.OverlapSphere(center, explosionRadius);

        foreach (Collider collider in colliders)
        {
            IDamagable damagable = collider.GetComponent<IDamagable>();
            if (damagable != null && !unieqEntites.Contains(collider.gameObject))
            {
                damagable.TakeDamage(explosionDamage);
                unieqEntites.Add(collider.gameObject);

                collider.GetComponentInChildren<Rigidbody>()?.AddExplosionForce(explosionForce, center, explosionRadius, explosionUpwardsModifier, ForceMode.VelocityChange);
            }
        }
    }
}
