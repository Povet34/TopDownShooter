using UnityEngine;

public class Bullet : MonoBehaviour
{
    private int bulletDamage;
    private float impactForce;
    private float impactNoiseRadius; // >0이면 비-적(땅/벽)에 박힐 때 피격음 발신(§6.2.1). 플레이어 총알만 설정.

    private BoxCollider cd;
    private Rigidbody rb;
    private MeshRenderer meshRenderer;
    private TrailRenderer trailRenderer;


    [SerializeField] private GameObject bulletImpactFX;


    private Vector3 startPosition;
    private float flyDistance;
    private bool bulletDisabled;

    [Tooltip("이 시간이 지나면 무조건 풀로 반환(아무것도 안 맞고 떠다니며 쌓이는 것 방지)")]
    [SerializeField] private float maxLifetime = 2f;
    private float spawnTime;

    private LayerMask allyLayerMask;


    protected virtual void Awake()
    {
        cd = GetComponent<BoxCollider>();
        rb = GetComponent<Rigidbody>();
        meshRenderer = GetComponent<MeshRenderer>();
        trailRenderer = GetComponent<TrailRenderer>();
    }

    public void BulletSetup(LayerMask allyLayerMask, int bulletDamage, float flyDistance = 100, float impactForce = 100, float impactNoiseRadius = 0f)
    {
        this.allyLayerMask = allyLayerMask;
        this.impactForce = impactForce;
        this.bulletDamage = bulletDamage;
        this.impactNoiseRadius = impactNoiseRadius;

        bulletDisabled = false;
        cd.enabled = true;
        meshRenderer.enabled = true;

        trailRenderer.Clear();
        trailRenderer.time = .25f;
        startPosition = transform.position;
        spawnTime = Time.time;
        this.flyDistance = flyDistance + .5f; // magic number .5f is a length of tip of the laser ( Check method UpdateAimVisuals() on PlayerAim script) ;
    }

    protected virtual void Update()
    {
        // 안전망: 무엇에도 안 맞고 떠다니는 총알이 쌓이지 않게 일정 시간 뒤 무조건 반환.
        if (Time.time - spawnTime > maxLifetime)
        {
            ReturnBulletToPool();
            return;
        }

        FadeTrailIfNeeded();
        DisableBulletIfNeeded();
        ReturnToPoolIfNeeded();
    }

    protected void ReturnToPoolIfNeeded()
    {
        if (trailRenderer.time < 0)
            ReturnBulletToPool();
    }
    protected void DisableBulletIfNeeded()
    {
        if (Vector3.Distance(startPosition, transform.position) > flyDistance && !bulletDisabled)
        {
            cd.enabled = false;
            meshRenderer.enabled = false;
            bulletDisabled = true;
        }
    }
    protected void FadeTrailIfNeeded()
    {
        if (Vector3.Distance(startPosition, transform.position) > flyDistance - 1.5f)
            trailRenderer.time -= 2 * Time.deltaTime; // magic number 2 is choosen trhou testing
    }



    protected virtual void OnCollisionEnter(Collision collision)
    {
        if (FriendlyFare() == false)
        {
            // Use a bitwise AND to check if the collsion layer is in the allyLayerMask
            if ((allyLayerMask.value & (1 << collision.gameObject.layer)) > 0)
            {
                ReturnBulletToPool(); // 아군엔 피해 없이 즉시 반환(예전 10초 지연 → 총알 누적 원인)
                return;
            }
        }

        CreateImpactFx();
        ReturnBulletToPool();

        IDamagable damagable = collision.gameObject.GetComponentInParent<IDamagable>();
        damagable?.TakeDamage(bulletDamage);

        ApplyBulletImpactToEnemy(collision);
        ApplyImpactToMovable(collision);
        EmitImpactNoise(collision);
    }

    // 적이 아닌 표면(땅/벽 등)에 박히면 피격음 발신(§6.2.1). 발사음을 못 들은 적도 근처 박힘이면 수색.
    private void EmitImpactNoise(Collision collision)
    {
        if (impactNoiseRadius <= 0f)
            return; // 플레이어 총알만(enemy 총알은 muzzle 핑도 안 냄 — 일관성)
        if (collision.gameObject.GetComponentInParent<Enemy>() != null)
            return; // 적 명중은 피격(GetHit→분대 교전)이 따로 처리 — 땅 박힘 소리는 비-적만
        NoisePing.EmitImpact(collision.contacts[0].point, impactNoiseRadius);
    }

    private void ApplyImpactToMovable(Collision collision)
    {
        Movable movable = collision.collider.GetComponentInParent<Movable>();
        if (movable != null)
            movable.Push(rb.linearVelocity.normalized * impactForce, collision.contacts[0].point);
    }

    private void ApplyBulletImpactToEnemy(Collision collision)
    {
        Enemy enemy = collision.gameObject.GetComponentInParent<Enemy>();
        if (enemy != null)
        {
            Vector3 force = rb.linearVelocity.normalized * impactForce;
            Rigidbody hitRigidbody = collision.collider.attachedRigidbody;
            enemy.BulletImpact(force, collision.contacts[0].point, hitRigidbody);
        }
    }

    protected void ReturnBulletToPool(float delay = 0)
    {
        // 씬/테스트 teardown으로 풀이 파괴됐으면(== null) 그냥 비활성화 — MissingReference 방지.
        if (ObjectPool.instance == null)
        {
            gameObject.SetActive(false);
            return;
        }
        ObjectPool.instance.ReturnObject(gameObject, delay);
    }


    protected void CreateImpactFx()
    {
        GameObject newFx = Instantiate(bulletImpactFX);
        newFx.transform.position = transform.position;

        Destroy(newFx, 1);

        //GameObject newImpactFx = ObjectPool.instance.GetObject(bulletImpactFX, transform);
        //ObjectPool.instance.ReturnObject(newImpactFx, 1);
    }

    private bool FriendlyFare() => GameManager.instance.friendlyFire;
}
