namespace TDS.Core
{
    /// <summary>
    /// 끼임(stuck) 감지(순수, 상태 보유). 이동하려는데 실제로 안 움직이는 시간이 누적되면 stuck.
    /// 글루가 매 틱 "이번 틱에 충분히 움직였나"를 넘기면, 안 움직인 시간이 stuckSeconds를 넘을 때 true.
    /// </summary>
    public class StuckTracker
    {
        private float stillTime;

        public void Reset() => stillTime = 0f;

        public bool Tick(bool movedEnough, float dt, float stuckSeconds)
        {
            if (movedEnough)
            {
                stillTime = 0f;
                return false;
            }

            stillTime += dt;
            if (stillTime >= stuckSeconds)
            {
                stillTime = 0f; // 복구 후 곧바로 재발동하지 않도록 리셋
                return true;
            }
            return false;
        }
    }
}
