using UnityEngine;
using TDS.Core;

public class BattleState_Range : EnemyState
{
    private Enemy_Range enemy;

    private float lastTimeShot = -10;
    private int bulletsShot = 0;

    // §12: 최근 피격 시 제자리에서 굳지 않고 사거리 유지하며 시야 회피로 재배치(strafe).
    private const float EvadeGrace = 3f;
    private const float RepositionInterval = 0.7f;
    private float repositionTimer;
    private Vector3 repositionDest;
    private bool repositioning;

    private int bulletsPerAttack;
    private float weaponCooldown;

    private float coverCheckTimer;
    private bool firstTimeAttack = true;
    public BattleState_Range(Enemy enemyBase, EnemyStateMachine stateMachine, string animBoolName) : base(enemyBase, stateMachine, animBoolName)
    {
        enemy = enemyBase as Enemy_Range;
    }
    public override void Enter()
    {
        base.Enter();
        SetupValuesForFirstAttack();

        enemy.agent.isStopped = true;
        enemy.agent.velocity = Vector3.zero;
        enemy.agent.updateRotation = false; // 전투 중엔 이동방향이 아니라 플레이어를 바라봄(사선/strafe 이동을 위해)

        enemy.visuals.EnableIK(true, true);

        stateTimer = enemy.attackDelay;
    }

    public override void Exit()
    {
        base.Exit();
        enemy.agent.updateRotation = true; // 다음 상태(이동/엄폐주행)는 이동방향을 바라봄
        StopRepositioning();
        enemy.anim.SetBool("Strafing", false);
    }


    public override void Update()
    {
        base.Update();

        if (enemy.IsSeeingPlayer())
            enemy.FaceTarget(enemy.aim.position);

        if (enemy.CanThrowGrenade())
            stateMachine.ChangeState(enemy.throwGrenadeState);

        if (MustAdvancePlayer())
            stateMachine.ChangeState(enemy.advancePlayerState);

        SeekCoverOrReposition();
        UpdateStrafeAnimation();

        if (stateTimer > 0)
            return;

        if (WeaponOutOfBullets())
        {
            if (enemy.IsUnstopppable() && UnstoppableWalkReady())
            {
                enemy.advanceDuration = weaponCooldown;
                stateMachine.ChangeState(enemy.advancePlayerState);
            }

            if (WeaponOnCooldown())
                AttemptToResetWeapon();

            return;
        }


        if (CanShoot() && enemy.IsAimOnPlayer())
        {
            Shoot();
        }
    }

    private bool MustAdvancePlayer()
    {
        if (enemy.IsUnstopppable())
            return false;

        return enemy.IsPlayerInAgrresionRange() == false && ReadyToLeaveCover();
    }

    private bool UnstoppableWalkReady()
    {
        float distanceToPlayer = Vector3.Distance(enemy.transform.position, enemy.player.position);
        bool outOfStoppingDistance = distanceToPlayer > enemy.advanceStoppingDistance;
        bool unstoppableWalkOnCooldown =
            Time.time < enemy.weaponData.maxWeaponCooldown + enemy.advancePlayerState.lastTimeAdvanced;

        return outOfStoppingDistance && unstoppableWalkOnCooldown == false;
    }

    // 원거리 기본 행동: 피격당하거나(threatened) 위험(시야/근접)하면 먼저 근처 엄폐를 찾아 그 뒤로 숨고,
    // 적절한 엄폐가 없으면 melee식으로 사거리 유지하며 시야 정면을 피해 재배치(strafe)한다.
    private void SeekCoverOrReposition()
    {
        bool threatened = Time.time - enemy.LastTimeDamaged < EvadeGrace;
        bool inDanger = ReadyToChangeCover();
        bool coverAllowed = enemy.coverPerk == CoverPerk.CanTakeAndChangeCover && ReadyToLeaveCover();

        // 엄폐 가능 여부는 0.5초마다, 필요할 때만 평가(CanGetCover는 비용·점유 부수효과 있음).
        coverCheckTimer -= Time.deltaTime;
        bool reevalCover = coverCheckTimer < 0f;
        if (reevalCover) coverCheckTimer = 0.5f;

        bool coverAvailable = reevalCover && coverAllowed && (threatened || inDanger) && enemy.CanGetCover();

        switch (RangedEngageDecision.Decide(threatened, inDanger, coverAllowed, coverAvailable))
        {
            case RangedEngageAction.TakeCover:
                StopRepositioning();
                stateMachine.ChangeState(enemy.runToCoverState);
                break;

            case RangedEngageAction.Reposition: // 적절한 엄폐 없음 + 피격 → melee식 재배치
                UpdateBattleMoverReposition();
                break;

            case RangedEngageAction.Hold:
                if (repositioning) StopRepositioning();
                break;
        }
    }

    private const float StrafeMinSpeed = 0.4f;

    // 재배치(strafe) 중엔 다리 애니를 이동 방향(플레이어 기준)에 맞춰 2D 블렌드로 구동(사선뛰기).
    // 단, 실제로 이동 중일 때만 — 못 움직이는데 달리는 클립이 돌면 '제자리 뛰기'가 됨.
    private void UpdateStrafeAnimation()
    {
        if (repositioning && enemy.agent.velocity.magnitude > StrafeMinSpeed)
        {
            Vector2 blend = StrafeBlend.Compute(enemy.agent.velocity, enemy.transform.forward);
            enemy.anim.SetBool("Strafing", true);
            enemy.anim.SetFloat("StrafeX", blend.x, 0.1f, Time.deltaTime);
            enemy.anim.SetFloat("StrafeY", blend.y, 0.1f, Time.deltaTime);
        }
        else
        {
            enemy.anim.SetBool("Strafing", false);
        }
    }

    private const float SpacingRadius = 3.5f;

    private void UpdateBattleMoverReposition()
    {
        repositionTimer -= Time.deltaTime;
        if (repositionTimer < 0f)
        {
            repositionTimer = RepositionInterval;

            float preferredRange = Mathf.Max(6f, enemy.advanceStoppingDistance);
            float distToPlayer = Vector3.Distance(enemy.transform.position, enemy.player.position);

            // §12 2차: 회피 행동(능력 게이트) → 목표 거리(strafe 유지 / backstep·flee 멀리)
            float healthFrac = enemy.health.maxHealth > 0 ? (float)enemy.health.currentHealth / enemy.health.maxHealth : 1f;
            var abilities = new EvasionAbilities { canStrafe = enemy.canStrafe, canBackstep = enemy.canBackstep, canFlee = enemy.canFlee };
            EvasionAction action = EvasionPlanner.Decide(true, healthFrac, abilities, distToPlayer, preferredRange, enemy.fleeHealthFraction);
            float targetDistance = EvasionPlanner.TargetDistance(action, preferredRange);

            var ctx = new BattleMoveContext
            {
                playerPos = enemy.player.position,
                playerForward = enemy.player.forward,
                enemyPos = enemy.transform.position,
                preferredDistance = targetDistance,
                viewHalfAngleDeg = 60f,
                wView = 1f,                  // 정면 회피
                wDist = 0.6f,                // 목표 거리 유지
                wInertia = 0.1f,
                allies = enemy.NearbyAllyPositions(SpacingRadius), // §12 2차: 몹 간 겹침 회피
                wSpacing = 0.8f,
                spacingRadius = SpacingRadius,
            };
            repositionDest = BattleMover.PickEngagePosition(ctx);
            enemy.agent.isStopped = false;
            enemy.agent.SetDestination(repositionDest);
            repositioning = true;
        }

        if (repositioning && Vector3.Distance(enemy.transform.position, repositionDest) < 1.2f)
            StopRepositioning();
    }

    private void StopRepositioning()
    {
        enemy.agent.isStopped = true;
        enemy.agent.velocity = Vector3.zero;
        repositioning = false;
    }

    #region Cover system region


    private bool ReadyToLeaveCover()
    {
        return Time.time > enemy.minCoverTime + enemy.runToCoverState.lastTimeTookCover;
    }

    private bool ReadyToChangeCover()
    {
        bool inDanger = IsPlayerInClearSight() || IsPlayerClose();
        bool advanceTimeIsOver = Time.time > enemy.advancePlayerState.lastTimeAdvanced + enemy.advanceDuration;

        return inDanger && advanceTimeIsOver;
    }


    private bool IsPlayerClose()
    {
        return Vector3.Distance(enemy.transform.position, enemy.player.transform.position) < enemy.safeDistance;
    }

    private bool IsPlayerInClearSight()
    {
        Vector3 directionToPlayer = enemy.player.transform.position - enemy.transform.position;


        if (Physics.Raycast(enemy.transform.position, directionToPlayer, out RaycastHit hit))
        {
            if (hit.transform.root == enemy.player.root)
                return true;
        }

        return false;
    }


    #endregion

    #region Weapon region

    private void AttemptToResetWeapon()
    {
        bulletsShot = 0;
        bulletsPerAttack = enemy.weaponData.GetBulletsPerAttack();
        weaponCooldown = enemy.weaponData.GetWeaponCooldown();
    }
    private bool WeaponOnCooldown() => Time.time > lastTimeShot + weaponCooldown;
    private bool WeaponOutOfBullets() => bulletsShot >= bulletsPerAttack;
    private bool CanShoot() => Time.time > lastTimeShot + 1 / enemy.weaponData.fireRate;
    private void Shoot()
    {
        enemy.FireSingleBullet();
        lastTimeShot = Time.time;
        bulletsShot++;
    }

    private void SetupValuesForFirstAttack()
    {
        if (firstTimeAttack)
        {
            //Advance stop distance should be slitly smaller than aggresion range in order
            //in order for enemy to advance all the time.
            enemy.aggresionRange = enemy.advanceStoppingDistance + 2;


            firstTimeAttack = false;
            bulletsPerAttack = enemy.weaponData.GetBulletsPerAttack();
            weaponCooldown = enemy.weaponData.GetWeaponCooldown();
        }
    }

    #endregion
}
