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

        enemy.visuals.EnableIK(true, true);

        stateTimer = enemy.attackDelay;
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

        ChangeCoverIfShould();
        RepositionIfThreatened();

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

    // §12: 최근 피격 시 사거리 유지하며 플레이어 시야 정면을 피해 재배치(strafe). 평소엔 제자리 사격.
    private void RepositionIfThreatened()
    {
        bool threatened = Time.time - enemy.LastTimeDamaged < EvadeGrace;

        if (!threatened)
        {
            if (repositioning) StopRepositioning();
            return;
        }

        repositionTimer -= Time.deltaTime;
        if (repositionTimer < 0f)
        {
            repositionTimer = RepositionInterval;

            float range = Mathf.Max(4f, Vector3.Distance(enemy.transform.position, enemy.player.position));
            var ctx = new BattleMoveContext
            {
                playerPos = enemy.player.position,
                playerForward = enemy.player.forward,
                enemyPos = enemy.transform.position,
                preferredDistance = range,   // 현재 사거리 유지하며 안전한 각으로
                viewHalfAngleDeg = 60f,
                wView = 1f,                  // 정면 회피
                wDist = 0.6f,                // 사거리 유지
                wInertia = 0.1f,
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

    private void ChangeCoverIfShould()
    {
        if (enemy.coverPerk != CoverPerk.CanTakeAndChangeCover)
            return;

        coverCheckTimer -= Time.deltaTime;

        if (coverCheckTimer < 0)
        {
            coverCheckTimer = .5f; // We do cover check each .5f seconds

            if (ReadyToChangeCover() && ReadyToLeaveCover())

            {
                if (enemy.CanGetCover())
                    stateMachine.ChangeState(enemy.runToCoverState);
            }
        }
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
