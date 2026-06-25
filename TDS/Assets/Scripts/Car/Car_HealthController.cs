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
        carController.rb.AddExplosionForce(explosionForce, center, explosionRadius, explosionUpwardsModifier, ForceMode.Impulse);

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
