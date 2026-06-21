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

    // §12 1차: 근접 교전권에선 플레이어 시야 정면을 피해 플랭크/뒤로 접근(창발 포위), 멀면 직진.
    // 목적지 갱신은 CanUpdateDestination()(0.25s)으로 throttle → 관성/이력(떨림 방지).
    private const float ScoringRange = 12f;
    private const float PreferredDistance = 2.6f;
    private const float ViewHalfAngle = 60f;

    private Vector3 EngageDestination()
    {
        Vector3 playerPos = enemy.player.position;

        if (Vector3.Distance(enemy.transform.position, playerPos) > ScoringRange)
            return playerPos; // 멀면 직진(§12.5 거리 레짐)

        var ctx = new BattleMoveContext
        {
            playerPos = playerPos,
            playerForward = enemy.player.forward,
            enemyPos = enemy.transform.position,
            preferredDistance = PreferredDistance,
            viewHalfAngleDeg = ViewHalfAngle,
            wView = 1f,
            wDist = 0.1f,
            wInertia = 0.12f,
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
