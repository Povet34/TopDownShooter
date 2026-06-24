using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveState_Range : EnemyState
{
    private Enemy_Range enemy;
    private Vector3 destination;
    private float lastRetargetTime;

    public MoveState_Range(Enemy enemyBase, EnemyStateMachine stateMachine, string animBoolName) : base(enemyBase, stateMachine, animBoolName)
    {
        enemy = enemyBase as Enemy_Range;
    }

    public override bool IsLocomotion => true;

    public override void Enter()
    {
        base.Enter();

        if (!enemy.AgentReady)
            return; // navmesh 밖이면 이동 명령 스킵(StuckRecovery가 복구 후 재진입)

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

        if (!enemy.AgentReady)
            return; // navmesh 밖이면 회전/도착판정 스킵(StuckRecovery가 복구)

        enemy.FaceTarget(GetNextPathPoint());

        // 분대원은 이동 중에도 분대 앵커(대형 목표)를 추종 — 조사 지점이 갱신되면 처음 목적지를 끝까지
        // 가지 않고 새 지점으로 따라간다. (분대 GetPatrolDestination은 부수효과 없는 TryGetPatrolPoint.)
        if (enemy.Squad != null && !enemy.HasSearchPoint && Time.time > lastRetargetTime + 0.2f)
        {
            lastRetargetTime = Time.time;
            enemy.agent.SetDestination(enemy.GetPatrolDestination());
        }

        if (enemy.agent.remainingDistance <= enemy.agent.stoppingDistance + .05f)
            stateMachine.ChangeState(enemy.idleState);
    }
}
