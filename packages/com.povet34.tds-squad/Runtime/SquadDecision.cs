namespace TDS.Core
{
    /// <summary>분대가 이번 틱에 무엇을 하려는지(기즈모·행동이 공유).</summary>
    public enum SquadIntent { Patrolling, Engaging, Despawning }

    /// <summary>
    /// 분대 의사결정(순수, §6.3). 우선순위:
    /// ① 교전 트리거(누가 보거나 최근 피격) → 전원 교전.
    /// ② (로밍) 순찰 상태로 스폰 가장자리를 한 번 벗어난 뒤 다시 가장자리 도달 → 디스폰.
    /// ③ 그 외 → 순찰(앵커를 플레이어 쪽으로 전진).
    /// Squad.Update와 OnDrawGizmos가 같은 함수를 써서 "보이는 결정 = 실제 결정"을 보장한다.
    /// </summary>
    public static class SquadDecision
    {
        public static SquadIntent Resolve(bool engageTrigger, bool roaming, bool hasLeftEdge, bool atEdge)
        {
            if (engageTrigger)
                return SquadIntent.Engaging;
            if (roaming && hasLeftEdge && SquadRoam.ShouldDespawn(patrolling: true, atEdge: atEdge))
                return SquadIntent.Despawning;
            return SquadIntent.Patrolling;
        }
    }
}
