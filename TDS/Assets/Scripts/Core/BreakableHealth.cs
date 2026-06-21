namespace TDS.Core
{
    /// <summary>파괴 가능한 오브젝트의 누적 피해 처리(순수).</summary>
    public static class BreakableHealth
    {
        /// <summary>피해를 적용해 새 체력을 반환. 이번 호출로 막 파괴됐으면 broke=true(이미 파괴 상태면 false).</summary>
        public static int Apply(int current, int damage, out bool broke)
        {
            int next = current - damage;
            broke = current > 0 && next <= 0;
            return next;
        }
    }
}
