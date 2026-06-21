using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TDS.Core;

public class ChaseState_Melee : EnemyState
{
    private Enemy_Melee enemy;
    private float lastTimeUpdatedDistanation;

    public ChaseState_Melee(Enemy enemyBase, EnemyStateMachine stateMachine, string animBoolName) : base(enemyBase, stateMachine, animBoolName)
    {
        enemy = enemyBase as Enemy_Melee;
    }

    public override bool IsLocomotion => true;

    public override void Enter()
    {
        base.Enter();


        enemy.agent.speed = enemy.runSpeed;
        enemy.agent.isStopped = false;

    }
    public override void Exit()
    {
        base.Exit();
    }

    public override void Update()
    {
        base.Update();

        if (enemy.PlayerInAttackRange())
            stateMachine.ChangeState(enemy.attackState);

        enemy.FaceTarget(GetNextPathPoint());

        if (CanUpdateDestination())
        {
            enemy.agent.destination = EngageDestination();
        }
    }

    // §12: 평소엔 공격 사거리까지 그냥 근접(둘러싸서 때림). 최근 피격(그레이스 피리어드) 시에만
    // 플레이어 시야 정면을 강하게 회피해 안전한 각으로 재배치. 멀면 직진(거리 레짐).
    // 목적지 갱신은 CanUpdateDestination()(0.25s) throttle → 관성/이력(떨림 방지).
    private const float ScoringRange = 12f;
    private const float ViewHalfAngle = 60f;
    private const float DamageGrace = 2.5f;
    private const float CalmViewWeight = 0f;          // 평소: 회피 안 함
    private const float ThreatenedViewWeight = 1.2f;  // 최근 피격: 강한 회피
    private const float SpacingRadius = 2.2f;          // §12 2차: 이 반경 안 아군과 겹침 회피

    private Vector3 EngageDestination()
    {
        Vector3 playerPos = enemy.player.position;

        if (Vector3.Distance(enemy.transform.position, playerPos) > ScoringRange)
            return playerPos; // 멀면 직진

        // 공격 사거리 안쪽까지 접근 = 둘러싸서 때릴 수 있는 거리(포위). 이전 고정 2.6이 미접근 원인.
        float preferred = Mathf.Max(0.6f, enemy.attackData.attackRange * 0.85f);

        // 최근 피격 시에만 회피 — 아니면 그냥 근접 공격
        float wView = BattleMover.ViewAvoidWeight(Time.time - enemy.LastTimeDamaged, DamageGrace, CalmViewWeight, ThreatenedViewWeight);

        var ctx = new BattleMoveContext
        {
            playerPos = playerPos,
            playerForward = enemy.player.forward,
            enemyPos = enemy.transform.position,
            preferredDistance = preferred,
            viewHalfAngleDeg = ViewHalfAngle,
            wView = wView,
            wDist = 1f,        // 공격 사거리 유지(근접 강제)
            wInertia = 0.1f,
            allies = enemy.NearbyAllyPositions(SpacingRadius), // §12 2차: 둘러쌀 때 겹침 완화
            wSpacing = 0.6f,
            spacingRadius = SpacingRadius,
        };
        return BattleMover.PickEngagePosition(ctx);
    }

    private bool CanUpdateDestination()
    {
        if (Time.time > lastTimeUpdatedDistanation + .25f)
        {
            lastTimeUpdatedDistanation = Time.time;
            return true;
        }

        return false;
    }
}
