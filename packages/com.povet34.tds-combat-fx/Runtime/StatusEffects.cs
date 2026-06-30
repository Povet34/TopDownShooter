using System.Collections.Generic;
using UnityEngine;

namespace TDS.Core
{
    public enum StatusKind { Bleed, Slow, Stun }

    /// <summary>
    /// 캐릭터 상태이상(디버프) 집합(순수, 테스트 가능). 효과를 지속시간+세기로 적용하고, Tick으로
    /// 시간을 진행시키며(만료 제거) 이번 틱 출혈 데미지를 돌려준다. 슬로우/스턴은 이동 속도 배수로 집계.
    /// 적용/표시는 글루(PlayerStatus/HUD)가 얹는다. magnitude 의미: Bleed=초당 데미지, Slow=감속률(0~1).
    /// </summary>
    public class StatusEffects
    {
        private class Effect { public float Remaining; public float Magnitude; }
        private readonly Dictionary<StatusKind, Effect> active = new Dictionary<StatusKind, Effect>();

        /// <summary>효과 적용 — 같은 종류면 더 긴 지속/큰 세기로 갱신(리프레시).</summary>
        public void Apply(StatusKind kind, float duration, float magnitude)
        {
            if (duration <= 0f) return;
            if (active.TryGetValue(kind, out var e))
            {
                e.Remaining = Mathf.Max(e.Remaining, duration);
                e.Magnitude = Mathf.Max(e.Magnitude, magnitude);
            }
            else active[kind] = new Effect { Remaining = duration, Magnitude = magnitude };
        }

        public bool Has(StatusKind kind) => active.ContainsKey(kind);
        public bool IsStunned => Has(StatusKind.Stun);
        public bool Any => active.Count > 0;
        public IEnumerable<StatusKind> Active => active.Keys;

        /// <summary>이동 속도 배수(1=정상). 스턴이면 0, 슬로우면 (1-감속률).</summary>
        public float SpeedMultiplier
        {
            get
            {
                if (IsStunned) return 0f;
                if (active.TryGetValue(StatusKind.Slow, out var e)) return Mathf.Clamp01(1f - e.Magnitude);
                return 1f;
            }
        }

        /// <summary>dt만큼 진행 → 이번 틱에 적용할 출혈 데미지(누적값) 반환, 만료 효과 제거.</summary>
        public float Tick(float dt)
        {
            if (dt <= 0f || active.Count == 0) return 0f;

            float bleed = 0f;
            List<StatusKind> expired = null;
            foreach (var kv in active)
            {
                if (kv.Key == StatusKind.Bleed) bleed += kv.Value.Magnitude * dt;
                kv.Value.Remaining -= dt;
                if (kv.Value.Remaining <= 0f) (expired ??= new List<StatusKind>()).Add(kv.Key);
            }
            if (expired != null) foreach (var k in expired) active.Remove(k);
            return bleed;
        }

        public void Clear() => active.Clear();
    }
}
