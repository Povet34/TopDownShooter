namespace TDS.Core
{
    /// <summary>
    /// 플레이어 전리품 지갑(순수, 테스트 가능). 휴대분(Carried)을 들고 다니다가
    /// 수송선 탑승 시 <see cref="Bank"/>로 반출(Banked)한다. 사망 시 휴대분 소실(<see cref="DropCarried"/>).
    /// </summary>
    public class LootWallet
    {
        public int Carried { get; private set; }
        public int Banked { get; private set; }

        public void Add(int amount)
        {
            if (amount > 0) Carried += amount;
        }

        /// <summary>휴대분을 반출 처리(Banked로 합산) 후 그 양을 반환하고 휴대분을 0으로.</summary>
        public int Bank()
        {
            int b = Carried;
            Banked += b;
            Carried = 0;
            return b;
        }

        /// <summary>사망 등으로 휴대분 소실.</summary>
        public void DropCarried() => Carried = 0;
    }
}
