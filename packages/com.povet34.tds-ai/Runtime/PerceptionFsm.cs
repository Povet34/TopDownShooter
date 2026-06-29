namespace TDS.Core
{
    public enum PerceptionState { Patrol, Alert, Engage }

    /// <summary>
    /// 적 인지 FSM(순수, §6.3). 순찰 → 경계(조사) → 교전, 그리고 시야 상실 시 역방향.
    /// 입력은 시야(seesPlayer)와 소음(heardNoise)뿐 — 실제 이동/조준/수색 위치는 글루가 담당한다.
    ///
    ///   순찰 --소음--> 경계 --시야--> 교전
    ///        --시야------------------> 교전
    ///   교전 --시야상실 T초--> 경계 --조사 실패(타임아웃)--> 순찰
    /// </summary>
    public class PerceptionFsm
    {
        public PerceptionState State { get; private set; } = PerceptionState.Patrol;

        /// <summary>교전 중 시야를 이 시간(초) 이상 잃으면 경계로 내려가 마지막 위치를 수색한다.</summary>
        public float LoseSightDuration { get; set; } = 3f;
        /// <summary>경계(조사)를 이 시간(초) 동안 했는데 못 찾으면 순찰로 복귀한다.</summary>
        public float InvestigateDuration { get; set; } = 5f;

        /// <summary>교전 중 시야를 잃은 누적 시간(초). 다시 보면 0으로 리셋.</summary>
        public float TimeWithoutSight { get; private set; }
        /// <summary>경계 상태 경과 시간(초). 새 소음이나 경계 진입 시 0으로 리셋.</summary>
        public float InvestigateElapsed { get; private set; }

        public PerceptionState Tick(bool seesPlayer, bool heardNoise, float dt)
        {
            switch (State)
            {
                case PerceptionState.Patrol:
                    if (seesPlayer) EnterEngage();
                    else if (heardNoise) EnterAlert();
                    break;

                case PerceptionState.Alert:
                    if (seesPlayer) EnterEngage();
                    else if (heardNoise) EnterAlert();        // 새 소음 → 조사 대상/타이머 갱신
                    else
                    {
                        InvestigateElapsed += dt;
                        if (InvestigateElapsed >= InvestigateDuration)
                            State = PerceptionState.Patrol;
                    }
                    break;

                case PerceptionState.Engage:
                    if (seesPlayer)
                    {
                        TimeWithoutSight = 0f;
                    }
                    else
                    {
                        TimeWithoutSight += dt;
                        if (TimeWithoutSight >= LoseSightDuration)
                            EnterAlert();
                    }
                    break;
            }

            return State;
        }

        /// <summary>피격 등 외부 사유로 즉시 교전 상태로 강제(시야 밖이라도). 시야 상실 타이머도 리셋.</summary>
        public void ForceEngage() => EnterEngage();

        private void EnterEngage()
        {
            State = PerceptionState.Engage;
            TimeWithoutSight = 0f;
        }

        private void EnterAlert()
        {
            State = PerceptionState.Alert;
            InvestigateElapsed = 0f;
        }
    }
}
