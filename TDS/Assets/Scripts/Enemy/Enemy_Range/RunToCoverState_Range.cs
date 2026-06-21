using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RunToCoverState_Range : EnemyState
{
    private Enemy_Range enemy;
    private Vector3 destination;

    private float lastProgressTime;
    private float bestRemaining;

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

        if (enemy.AgentReady)
        {
            enemy.agent.isStopped = false;
            enemy.agent.speed = enemy.runSpeed;
            enemy.agent.SetDestination(destination);
        }

        lastProgressTime = Time.time;
        bestRemaining = float.MaxValue;
    }

    public override void Exit()
    {
        base.Exit();

        lastTimeTookCover = Time.time;
    }

    public override void Update()
    {
        base.Update();

        if (!enemy.AgentReady)
            return; // navmesh 밖이면 회전/진전판정 스킵(StuckRecovery가 복구)

        enemy.FaceTarget(GetNextPathPoint());

        // 진전 추적: 남은 거리가 의미있게 줄면 진전 시각 갱신, 안 줄면(못 닿아 비빔) 누적된다.
        if (!enemy.agent.pathPending)
        {
            float remaining = enemy.agent.remainingDistance;
            if (!float.IsInfinity(remaining) && remaining < bestRemaining - 0.1f)
            {
                bestRemaining = remaining;
                lastProgressTime = Time.time;
            }
        }

        float dist = Vector3.Distance(enemy.transform.position, destination);
        float sinceProgress = Time.time - lastProgressTime;

        // 도달 반경 안이거나 일정 시간 진전이 없으면(비빔) 도착으로 간주 → 사격 시작(무한 grinding 방지).
        if (TDS.Core.CoverApproach.ShouldEngage(dist, sinceProgress))
            stateMachine.ChangeState(enemy.battleState);
    }
}
