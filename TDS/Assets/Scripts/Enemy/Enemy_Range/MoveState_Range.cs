using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveState_Range : EnemyState
{
    private Enemy_Range enemy;
    private Vector3 destination;

    public MoveState_Range(Enemy enemyBase, EnemyStateMachine stateMachine, string animBoolName) : base(enemyBase, stateMachine, animBoolName)
    {
        enemy = enemyBase as Enemy_Range;
    }

    public override bool IsLocomotion => true;

    public override void Enter()
    {
        base.Enter();

        enemy.agent.isStopped = false; // 교전 상태에서 멈춰있던 agent를 순찰 진입 시 다시 풀어줌
        enemy.agent.speed = enemy.walkSpeed;

        // 경계 수색이면 조사 지점으로, 아니면 다음 순찰점으로.
        if (enemy.HasSearchPoint)
        {
            destination = enemy.SearchPoint;
            enemy.ConsumeSearchPoint();
        }
        else
            destination = enemy.GetPatrolDestination();
        enemy.agent.SetDestination(destination);
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void Update()
    {
        base.Update();

        enemy.FaceTarget(GetNextPathPoint());


        if (enemy.agent.remainingDistance <= enemy.agent.stoppingDistance + .05f)
            stateMachine.ChangeState(enemy.idleState);
    }
}
