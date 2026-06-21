using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RunToCoverState_Range : EnemyState
{
    private Enemy_Range enemy;
    private Vector3 destination;

    public float lastTimeTookCover { get; private set; }

    public RunToCoverState_Range(Enemy enemyBase, EnemyStateMachine stateMachine, string animBoolName) : base(enemyBase, stateMachine, animBoolName)
    {
        enemy = enemyBase as Enemy_Range;
    }

    public override bool IsLocomotion => true;

    public override void Enter()
    {
        base.Enter();
        destination = enemy.currentCover.transform.position;

        enemy.visuals.EnableIK(true,false);

        enemy.agent.isStopped = false;
        enemy.agent.speed = enemy.runSpeed;
        enemy.agent.SetDestination(destination);
    }

    public override void Exit()
    {
        base.Exit();

        lastTimeTookCover = Time.time;
    }

    public override void Update()
    {
        base.Update();

        enemy.FaceTarget(GetNextPathPoint());

        bool arrived = Vector3.Distance(enemy.transform.position, destination) < .8f;

        // 엄폐점이 navmesh에서 약간 벗어나 경로가 부분(PathPartial)이면 정확히 못 닿아 멈춘다 →
        // 더 못 가고 정지했으면 도착으로 간주(BattleState 전이, 무한 대기 방지).
        bool cannotGetCloser = enemy.agent.hasPath
            && !enemy.agent.pathPending
            && enemy.agent.remainingDistance <= enemy.agent.stoppingDistance + 0.15f
            && enemy.agent.velocity.sqrMagnitude < 0.05f;

        if (arrived || cannotGetCloser)
            stateMachine.ChangeState(enemy.battleState);
    }
}
