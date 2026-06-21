using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 분대(Squad) — 한 곳에서 뭉쳐 스폰된 적들이 "의식을 공유"한다(§6.2 그룹 인지).
/// 멤버 중 한 명이라도 플레이어를 보거나(시야) 누군가 피격당하면 분대 전원이 교전에 들어간다.
/// 개별 적의 시야/이동 AI는 그대로 두고, 이 레이어가 "교전 공유"만 얹는다(재작성 X).
///
/// 참고: 형제 프로젝트 SpawntableGenerator의 MonsterPack(앵커+공유 PackState) 개념을 TDS의
/// 적별 perception 구조에 맞게 경량화한 것.
/// </summary>
public class Squad : MonoBehaviour
{
    private readonly List<Enemy> members = new List<Enemy>();

    [Tooltip("누군가 피격당한 뒤 이 시간(초) 동안 분대 전원 교전 유지(시야 없어도)")]
    [SerializeField] private float hitAlertDuration = 4f;

    private float hitAlertUntil = -999f;

    public IReadOnlyList<Enemy> Members => members;
    public bool Engaged { get; private set; }

    public void Register(Enemy e)
    {
        if (e == null || members.Contains(e))
            return;
        members.Add(e);
        e.Squad = this;
    }

    /// <summary>멤버가 피격당함 → 분대 전원 즉시 교전(뒤돌아 있어도). 적이 GetHit에서 호출.</summary>
    public void OnMemberHit()
    {
        hitAlertUntil = Time.time + hitAlertDuration;
    }

    private void Update()
    {
        PruneDead();
        if (members.Count == 0)
        {
            Destroy(gameObject);
            return;
        }

        // 한 명이라도 플레이어를 보거나, 최근 누군가 맞았으면 → 분대 전원 교전.
        bool trigger = Time.time < hitAlertUntil;
        if (!trigger)
        {
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i] != null && members[i].SeesPlayer())
                {
                    trigger = true;
                    break;
                }
            }
        }

        Engaged = trigger;
        if (!trigger)
            return;

        // ForceEngage는 시야 상실 타이머를 리셋하므로, 트리거가 유지되는 한 전원 교전을 유지한다.
        // 트리거가 사라지면(아무도 안 보이고 피격도 오래됨) 각 적이 제 lose-sight 타이머로 개별 이탈.
        for (int i = 0; i < members.Count; i++)
            if (members[i] != null)
                members[i].SquadEngage();
    }

    private void PruneDead()
    {
        for (int i = members.Count - 1; i >= 0; i--)
        {
            var m = members[i];
            bool dead = m == null || m.health == null || m.health.currentHealth <= 0;
            if (dead)
                members.RemoveAt(i);
        }
    }
}
