using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
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
    [Tooltip("시야 사거리(콘 안에서 이 거리까지 플레이어를 본다). §6.2 인지")]
    public float aggresionRange;

    [Header("Perception (§6.2 시야)")]
    [Tooltip("시야 콘 반각(도). 이 각도 안 + 사거리 안 + 가림 없을 때만 발각")]
    [SerializeField] protected float viewHalfAngle = 70f;
    [Tooltip("이 반경 안이면 콘 밖/뒤라도 인지(몰래 바로 옆 접근 차단)")]
    [SerializeField] protected float senseRadius = 2f;
    [SerializeField] protected float eyeHeight = 1.6f;
    [Tooltip("시야를 가리는 환경 레이어(0이면 Default+Environment 자동)")]
    [SerializeField] protected LayerMask viewOccluderMask = 0;
    [Tooltip("교전 중 시야를 이 시간(초) 잃으면 경계로 내려가 마지막 위치 수색")]
    [SerializeField] protected float loseSightSeconds = 3f;
    [Tooltip("경계(수색)를 이 시간(초) 했는데 못 찾으면 순찰 복귀")]
    [SerializeField] protected float investigateSeconds = 5f;

    protected readonly TDS.Core.PerceptionFsm perception = new TDS.Core.PerceptionFsm();
    public TDS.Core.PerceptionState PerceptionState => perception.State;

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

        perception.LoseSightDuration = loseSightSeconds;
        perception.InvestigateDuration = investigateSeconds;
        if (viewOccluderMask == 0)
            viewOccluderMask = LayerMask.GetMask("Default", "Environment");
    }

  

    protected virtual void Update()
    {
        UpdateAggro();

        UpdateLocomotionAnimation();
        UpdateStuckRecovery();
    }

    // §6.2/§6.3: 시야(콘+가림+근접) 기반 인지로 교전을 켜고, 시야를 잃으면 경계→순찰로 내려가며 교전을 끈다.
    // Boss는 이 동작을 오버라이드해 기존 거리 aggro를 유지한다.
    private Vector3 lastKnownPlayerPos;
    private const float NoiseMaxAge = 0.3f;

    /// <summary>NavMesh 명령을 내려도 안전한가(살아있고 활성이고 navmesh 위). 죽었거나 끼어 빠진 적은 false.</summary>
    public bool AgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

    /// <summary>소속 분대(있으면). 분대원 중 한 명이라도 발각/피격되면 전원 교전 공유. null이면 단독.</summary>
    public Squad Squad { get; set; }

    /// <summary>분대 공유 트리거 — 시야 밖이라도 즉시 교전 + 시야상실 타이머 리셋(교전 유지).</summary>
    public void SquadEngage()
    {
        if (!AgentReady)
            return;
        perception.ForceEngage();
        if (!inBattleMode)
            EnterBattleMode();
    }

    protected virtual void UpdateAggro()
    {
        // 죽었거나 navmesh 밖이면 인지/교전 전환 금지(StuckRecovery가 복구) — 비활성 agent 명령 에러 방지.
        if (!AgentReady)
            return;

        bool sees = SeesPlayer();
        if (sees)
            lastKnownPlayerPos = player.position;

        bool heard = HeardNoise(out Vector3 noisePos);

        var prev = perception.State;
        var state = perception.Tick(sees, heard, Time.deltaTime);

        if (state == TDS.Core.PerceptionState.Engage)
        {
            if (!inBattleMode)
                EnterBattleMode();
            return;
        }

        if (inBattleMode)
            ExitBattleMode();

        // 경계로 막 진입 → 조사 지점으로 이동(소음이면 소음 위치, 시야상실이면 마지막 목격 위치).
        if (state == TDS.Core.PerceptionState.Alert && prev != TDS.Core.PerceptionState.Alert)
            OnEnterAlert(heard ? noisePos : lastKnownPlayerPos);
    }

    /// <summary>경계 진입 시 호출 — 서브클래스가 조사 지점으로 이동(수색)시킨다. Boss는 사용 안 함.</summary>
    protected virtual void OnEnterAlert(Vector3 investigatePoint) { }

    /// <summary>플레이어가 시야 콘(각도+사거리) 안 + 가려지지 않음, 또는 아주 가까우면(근접) true.</summary>
    public bool SeesPlayer()
    {
        if (player == null)
            return false;

        Vector3 self = transform.position;
        Vector3 target = player.position;

        Vector3 flat = target - self; flat.y = 0f;
        if (flat.sqrMagnitude <= senseRadius * senseRadius)
            return true; // 인접 자각

        if (!TDS.Core.ViewCone.InView(self, transform.forward, target, viewHalfAngle, aggresionRange))
            return false;

        // 가림: 눈높이에서 대상까지 환경에 막히면 안 보임
        Vector3 eye = self + Vector3.up * eyeHeight;
        Vector3 t = target + Vector3.up * (eyeHeight * 0.7f);
        Vector3 dir = t - eye;
        float dist = dir.magnitude;
        if (dist < 0.5f)
            return true;

        return !Physics.Raycast(eye, dir / dist, dist - 0.4f, viewOccluderMask, QueryTriggerInteraction.Ignore);
    }

    /// <summary>최근 총성 등 소음이 이 적에게 들리면 true(+ 소음 위치). 경계 진입 트리거(§6.2).</summary>
    protected virtual bool HeardNoise(out Vector3 noisePos)
    {
        noisePos = NoisePing.Position;
        float age = Time.time - NoisePing.Time;
        float dist = Vector3.Distance(transform.position, NoisePing.Position);
        return TDS.Core.NoiseModel.Heard(dist, NoisePing.Radius, age, NoiseMaxAge);
    }

    /// <summary>경계 중 수색할 지점(마지막 목격/소음 위치). MoveState가 순찰점 대신 사용.</summary>
    public Vector3 SearchPoint { get; private set; }
    public bool HasSearchPoint { get; private set; }
    public void SetSearchPoint(Vector3 p) { SearchPoint = p; HasSearchPoint = true; }
    public void ConsumeSearchPoint() { HasSearchPoint = false; }

    // §2: navmesh가 낮은 장애물 위로 베이크되거나 회피 교착으로 적이 끼는 문제 → 일정 시간 진전이 없으면
    // 가까운 navmesh 바닥으로 워프 + 재경로해서 빠져나오게 한다(원인 불문 안전망).
    private readonly TDS.Core.StuckTracker stuckTracker = new TDS.Core.StuckTracker();
    private Vector3 lastStuckRefPos;
    private const float StuckSeconds = 1.5f;
    private const float StuckProgressThreshold = 0.5f; // 이 시간 안에 이만큼 못 움직이면 끼임

    private bool ShouldCheckStuck()
    {
        return agent != null && agent.isOnNavMesh && !agent.isStopped && !manualMovement
            && agent.hasPath && !agent.pathPending
            && !float.IsInfinity(agent.remainingDistance)
            && agent.remainingDistance > agent.stoppingDistance + 0.3f;
    }

    private void UpdateStuckRecovery()
    {
        if (!ShouldCheckStuck())
        {
            stuckTracker.Reset();
            lastStuckRefPos = transform.position;
            return;
        }

        bool progressed = Vector3.Distance(transform.position, lastStuckRefPos) > StuckProgressThreshold;
        if (progressed)
            lastStuckRefPos = transform.position;

        if (stuckTracker.Tick(progressed, Time.deltaTime, StuckSeconds))
            RecoverFromStuck();
    }

    private void RecoverFromStuck()
    {
        Vector3 dest = agent.destination;
        if (NavMesh.SamplePosition(transform.position, out var hit, 2.5f, NavMesh.AllAreas))
            agent.Warp(hit.position); // 장애물 위/밖이면 가까운 navmesh 바닥으로 끌어내림
        agent.ResetPath();
        agent.SetDestination(dest);
        lastStuckRefPos = transform.position;
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

    // 시야를 잃어 교전에서 빠질 때. 서브클래스가 순찰(idle)로 복귀시킨다.
    public virtual void ExitBattleMode()
    {
        inBattleMode = false;
        if (agent != null && agent.isOnNavMesh)
            agent.isStopped = false; // 교전 중 멈춰있던 agent를 풀어 순찰 이동이 가능하게
    }

    public virtual void GetHit(int damage)
    {
        perception.ForceEngage(); // 뒤에서 맞아도 즉시 교전(시야 밖이라도) — 곧바로 이탈 방지
        EnterBattleMode();
        Squad?.OnMemberHit(); // 분대 공유: 한 명이 맞으면 전원 교전 (§6.2 그룹 인지)
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
        directionToTarget.y = 0f; // 수평 회전만 — 수직 성분이 LookRotation을 깨뜨리지 않게

        // off-mesh 경로점 등으로 NaN/Inf가 들어오거나 거의 0이면 회전 스킵(LookRotation/Euler assert 방지).
        if (!IsFiniteVector(directionToTarget) || directionToTarget.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);

        Vector3 currentEulerAngels = transform.rotation.eulerAngles;

        if (turnSpeed == 0)
            turnSpeed = this.turnSpeed;

        float yRotation = 
            Mathf.LerpAngle(currentEulerAngels.y, targetRotation.eulerAngles.y, turnSpeed * Time.deltaTime);

        transform.rotation = Quaternion.Euler(currentEulerAngels.x, yRotation, currentEulerAngels.z);
    }

    private static bool IsFiniteVector(Vector3 v)
        => !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)
          || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));

    // §12 2차 소프트 간격용: 주변 같은 편 적 위치(자신 제외, 적 단위 중복 제거).
    private static readonly Collider[] allyHitBuffer = new Collider[32];
    public Vector3[] NearbyAllyPositions(float radius)
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, radius, allyHitBuffer);
        var seen = new HashSet<Enemy>();
        var list = new List<Vector3>();
        for (int i = 0; i < count; i++)
        {
            var e = allyHitBuffer[i].GetComponentInParent<Enemy>();
            if (e != null && e != this && seen.Add(e))
                list.Add(e.transform.position);
        }
        return list.ToArray();
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
