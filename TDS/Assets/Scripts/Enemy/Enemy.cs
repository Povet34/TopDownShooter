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
    [Tooltip("분대 소속일 때 발포음(muzzle) 청각 반경(§6.2.1). 이 안의 발포음은 크기와 무관하게 들린다. 피격음은 미적용(발신 반경 그대로).")]
    [SerializeField] protected float squadHearingRadius = 50f;
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

        // 경계 → 조사 지점으로 이동. 분대 소속이면 분대가 그룹으로 조사(앵커 이동), 솔로면 개별 수색.
        if (state == TDS.Core.PerceptionState.Alert)
        {
            if (prev != TDS.Core.PerceptionState.Alert)
            {
                Vector3 point = heard ? noisePos : lastKnownPlayerPos;
                if (Squad != null) Squad.OnMemberHeardNoise(point);
                else OnEnterAlert(point);
            }
            else if (heard && Squad != null)
            {
                Squad.OnMemberHeardNoise(noisePos); // 경계 중 새 소음 → 분대 조사 지점 갱신
            }
        }
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

    /// <summary>
    /// 총구음/피격음 2채널 중 들리는 게 있으면 true(+ 조사 위치). 경계 진입 트리거(§6.2).
    /// 총구음 우선(플레이어에 더 가까운 단서) → 없으면 피격음 위치로 수색. 판정은 순수 NoiseModel.
    /// </summary>
    protected virtual bool HeardNoise(out Vector3 noisePos)
    {
        var m = NoisePing.Muzzle;
        var im = NoisePing.Impact;
        Vector3 pos = transform.position;
        // 발포음(muzzle)은 분대원이 멀리서도 듣는다(squadHearingRadius, 기본 50m).
        // 피격음(impact)은 실탄 기준 근거리만(발신 반경 그대로, ~10m) — 분대 청각 부스트 미적용.
        // (폭발성 공격 등 큰 피격음은 추후 발신 반경을 키워 표현.)
        float muzzleHear = Squad != null ? squadHearingRadius : 0f;
        bool muzzleHeard = TDS.Core.NoiseModel.Heard(Vector3.Distance(pos, m.position), Mathf.Max(m.radius, muzzleHear), Time.time - m.time, NoiseMaxAge);
        bool impactHeard = TDS.Core.NoiseModel.Heard(Vector3.Distance(pos, im.position), im.radius, Time.time - im.time, NoiseMaxAge);
        return TDS.Core.NoiseModel.Investigate(muzzleHeard, m.position, impactHeard, im.position, out noisePos) != TDS.Core.NoiseKind.None;
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

    private float lastOffMeshWarn = -999f;

    private void UpdateStuckRecovery()
    {
        // 살아있는데 agent가 navmesh 밖으로 빠짐 = 진짜 navmesh 문제. 조용히 두지(얼어붙음) 말고
        // 가까운 navmesh로 복귀시키고, 자주 일어나면 알 수 있게 경고로 표면화(rate-limited).
        bool alive = health == null || health.currentHealth > 0;
        if (alive && agent != null && agent.enabled && !agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out var back, 6f, NavMesh.AllAreas))
            {
                agent.Warp(back.position);
                lastStuckRefPos = back.position;
            }
            if (Time.time - lastOffMeshWarn > 3f)
            {
                lastOffMeshWarn = Time.time;
                Debug.LogWarning($"[Enemy] '{name}'이(가) NavMesh 밖으로 빠져 복귀시킴 @{transform.position:0.0} — navmesh 커버리지/스폰/넉백 확인 필요", this);
            }
            return;
        }

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
        // 분대 소속이면 분대 공유 앵커 주변 대형 위치로 — 흩어지지 않고 함께 로밍(§6.2 그룹).
        if (Squad != null && Squad.TryGetPatrolPoint(this, out var squadPoint))
            return squadPoint;

        if (patrolPointsPosition == null || patrolPointsPosition.Length == 0)
            return transform.position; // 순찰점 없는 스폰 적은 제자리(크래시 방지)

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
        DrawPerceptionGizmo();
    }

    // 적이 지금 어떤 인지 상태인지 한눈에: 머리 위 색 구슬(초록=순찰/주황=경계/빨강=교전) + 시야 콘 + 근접 반경.
    // 게임뷰 우상단 Gizmos 토글을 켜면 플레이 중에도 보인다.
    private void DrawPerceptionGizmo()
    {
        Color c;
        switch (PerceptionState)
        {
            case TDS.Core.PerceptionState.Engage: c = Color.red; break;
            case TDS.Core.PerceptionState.Alert: c = new Color(1f, 0.55f, 0f); break; // 주황
            default: c = Color.green; break;
        }

        Vector3 head = transform.position + Vector3.up * 2.3f;
        Gizmos.color = c;
        Gizmos.DrawSphere(head, 0.22f);

        // 시야 콘(수평): 전방 ±viewHalfAngle, 사거리 aggresionRange
        Vector3 fwd = transform.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude > 1e-4f)
        {
            fwd.Normalize();
            Vector3 eye = transform.position + Vector3.up * 0.5f;
            Gizmos.color = new Color(c.r, c.g, c.b, 0.5f);
            Vector3 prev = eye + (Quaternion.AngleAxis(-viewHalfAngle, Vector3.up) * fwd) * aggresionRange;
            Gizmos.DrawLine(eye, prev);
            const int seg = 14;
            for (int i = 1; i <= seg; i++)
            {
                float ang = Mathf.Lerp(-viewHalfAngle, viewHalfAngle, (float)i / seg);
                Vector3 p = eye + (Quaternion.AngleAxis(ang, Vector3.up) * fwd) * aggresionRange;
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
            Gizmos.DrawLine(prev, eye);
        }

        // 근접 인지 반경(콘 밖/뒤라도 이 안이면 발각)
        Gizmos.color = new Color(c.r, c.g, c.b, 0.25f);
        Gizmos.DrawWireSphere(transform.position, senseRadius);

#if UNITY_EDITOR
        var style = new GUIStyle { fontSize = 11 };
        style.normal.textColor = c;
        string label = PerceptionState.ToString();
        if (Squad != null) label += "  [분대]";
        UnityEditor.Handles.Label(head + Vector3.up * 0.35f, label, style);
#endif
    }
}
