using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using TDS.Core;

public enum EnemyType { Melee, Range, Boss ,Random}

[RequireComponent(typeof(NavMeshAgent))]
public class Enemy : MonoBehaviour
{
    public EnemyType enemyType;
    public LayerMask whatIsAlly;
    public LayerMask whatIsPlayer;
    
    [Header("Idle data")]
    public float idleTime;
    public float aggresionRange;

    [Header("Move data")]
    public float walkSpeed = 1.5f;
    public float runSpeed = 3;
    public float turnSpeed;
    private bool manualMovement;
    private bool manualRotation;

    [SerializeField] private Transform[] patrolPoints;
    private Vector3[] patrolPointsPosition;
    private int currentPatrolIndex;

    public bool inBattleMode { get; private set; }
    protected bool isMeleeAttackReady;

    /// <summary>마지막으로 피해를 입은 시각(Time.time). 최근 피격 시 회피 무빙 가중치를 높이는 데 사용(§12).</summary>
    public float LastTimeDamaged { get; private set; } = -999f;

    [Header("Death")]
    [Tooltip("사망 후 이 시간(초) 뒤 래그돌을 고정해 끝없는 슬라이딩/꿈틀거림을 멈춤")]
    [SerializeField] private float deadFreezeDelay = 5f;

    public Transform player {  get; private set; }
    public Animator anim { get; private set; }
    public NavMeshAgent agent { get; private set; }
    public EnemyStateMachine stateMachine { get; private set; }
    public Enemy_Visuals visuals { get; private set; }

    public Enemy_Health health { get; private set; }

    public Ragdoll ragdoll { get; private set; }

    public Enemy_DropController dropController { get; private set; }
    public AudioManager audioManager { get; private set; }

    protected virtual void Awake()
    {
        stateMachine = new EnemyStateMachine();

        health = GetComponent<Enemy_Health>();
        ragdoll = GetComponent<Ragdoll>();
        visuals = GetComponent<Enemy_Visuals>();
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();
        dropController = GetComponent<Enemy_DropController>();
        player = GameObject.Find("Player").GetComponent<Transform>();
    }

    protected virtual void Start()
    {
        InitializePatrolPoints();
        audioManager = AudioManager.instance;
    }

  

    protected virtual void Update()
    {
        if (ShouldEnterBattleMode())
            EnterBattleMode();

        UpdateLocomotionAnimation();
    }

    /// <summary>
    /// 이동 상태(IsLocomotion)에선 실제 평면 속도로 이동 애니 재생속도를 맞춰 제자리걸음/발 미끄러짐을 줄인다.
    /// navmesh가 위치를 구동하므로(root motion off) 재생속도만 바뀐다. 그 외 상태는 정상 속도(1).
    /// </summary>
    private void UpdateLocomotionAnimation()
    {
        if (anim == null || agent == null)
            return;

        EnemyState state = stateMachine != null ? stateMachine.currentState : null;
        if (state != null && state.IsLocomotion)
        {
            Vector3 v = agent.velocity; v.y = 0f;
            anim.speed = LocomotionAnim.PlaybackSpeed(v.magnitude, agent.speed);
        }
        else if (anim.speed != 1f)
        {
            anim.speed = 1f;
        }
    }

    protected virtual void InitializePerk()
    {

    }

    public virtual void MakeEnemyVIP()
    {
        int additionalHealth = Mathf.RoundToInt(health.currentHealth * 1.5f);

        health.currentHealth += additionalHealth;

        transform.localScale = transform.localScale * 1.15f;
    }

    protected bool ShouldEnterBattleMode()
    {
        if (IsPlayerInAgrresionRange() && !inBattleMode)
        {
            EnterBattleMode();
            return true;
        }

        return false;
    }

    public virtual void EnterBattleMode()
    {
        inBattleMode = true;
    }

    public virtual void GetHit(int damage)
    {
        EnterBattleMode();
        LastTimeDamaged = Time.time; // 최근 피격 → 회피 무빙 가중치↑ (§12 그레이스 피리어드)
        health.ReduceHealth(damage);

        if (health.ShouldDie())
            Die();
        else
            GameServices.Registry.Resolve<ICombatFeedbackService>()?.ReportHit(transform.position + Vector3.up, 1f);
    }

    public virtual void Die()
    {
        GameServices.Registry.Resolve<ICombatFeedbackService>()?.ReportKill(transform.position + Vector3.up);

        dropController.DropItems();


        anim.enabled = false;
        if (agent.isOnNavMesh) // navmesh 밖 에이전트에서도 안전
            agent.isStopped = true;
        agent.enabled = false;

        ragdoll.RagdollActive(true);
        StartCoroutine(FreezeRagdollAfterDelay()); // 일정 시간 뒤 고정 → 끝없이 안 움직이게

        MissionObject_HuntTarget huntTarget = GetComponent<MissionObject_HuntTarget>();
        huntTarget?.InvokeOnTargetKilled();
    }

    private IEnumerator FreezeRagdollAfterDelay()
    {
        yield return new WaitForSeconds(deadFreezeDelay);
        if (ragdoll != null)
            ragdoll.Freeze();
    }

    public virtual void MeleeAttackCheck(Transform[] damagePoints, float attackCheckRadius,GameObject fx,int damage)
    {
        if (isMeleeAttackReady == false)
            return;

        foreach (Transform attackPoint in damagePoints)
        {
            Collider[] detectedHits =
                Physics.OverlapSphere(attackPoint.position, attackCheckRadius, whatIsPlayer);


            for (int i = 0; i < detectedHits.Length; i++)
            {
                IDamagable damagable = detectedHits[i].GetComponent<IDamagable>();

                if (damagable != null)
                {

                    damagable.TakeDamage(damage);
                    isMeleeAttackReady = false;
                    GameObject newAttackFx = ObjectPool.instance.GetObject(fx, attackPoint);

                    ObjectPool.instance.ReturnObject(newAttackFx, 1);
                    return;
                }
            }

        }

    }

    public void EnableMeleeAttackCheck(bool enable) => isMeleeAttackReady = enable;


    public virtual void BulletImpact( Vector3 force,Vector3 hitPoint,Rigidbody rb)
    {
        if(health.ShouldDie())
            StartCoroutine(DeathImpactCourutine(force,hitPoint,rb));
    }
    private IEnumerator DeathImpactCourutine(Vector3 force, Vector3 hitPoint, Rigidbody rb)
    {
        yield return new WaitForSeconds(.1f);

        rb.AddForceAtPosition(force, hitPoint, ForceMode.Impulse);
    }

    public void FaceTarget(Vector3 target,float turnSpeed = 0)
    {
        Vector3 directionToTarget = target - transform.position;
        if (directionToTarget.sqrMagnitude < 0.0001f)
            return; // 대상이 현재 위치와 거의 같음 → 회전 불필요(LookRotation zero-vector 경고 방지)

        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);

        Vector3 currentEulerAngels = transform.rotation.eulerAngles;

        if (turnSpeed == 0)
            turnSpeed = this.turnSpeed;

        float yRotation = 
            Mathf.LerpAngle(currentEulerAngels.y, targetRotation.eulerAngles.y, turnSpeed * Time.deltaTime);

        transform.rotation = Quaternion.Euler(currentEulerAngels.x, yRotation, currentEulerAngels.z);
    }

    

    #region Animation events
    public void ActivateManualMovement(bool manualMovement) => this.manualMovement = manualMovement;
    public bool ManualMovementActive() => manualMovement;

    public void ActivateManualRotation(bool manualRotation) => this.manualRotation = manualRotation;
    public bool ManualRotationActive() => manualRotation;
    public void AnimationTrigger() => stateMachine.currentState.AnimationTrigger();



    public virtual void AbilityTrigger()
    {
        stateMachine.currentState.AbilityTrigger();
    }

    #endregion

    #region Patrol logic
    public Vector3 GetPatrolDestination()
    {
        Vector3 destination = patrolPointsPosition[currentPatrolIndex];

        currentPatrolIndex++;

        if (currentPatrolIndex >= patrolPoints.Length)
            currentPatrolIndex = 0;

        return destination;
    }
    private void InitializePatrolPoints()
    {
        patrolPointsPosition = new Vector3[patrolPoints.Length];

        for (int i = 0; i < patrolPoints.Length; i++)
        {
            patrolPointsPosition[i] = patrolPoints[i].position;
            patrolPoints[i].gameObject.SetActive(false);
        }
    }

    #endregion

    public bool IsPlayerInAgrresionRange() => Vector3.Distance(transform.position, player.position) < aggresionRange;
    protected virtual void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(transform.position, aggresionRange);
    }
}
