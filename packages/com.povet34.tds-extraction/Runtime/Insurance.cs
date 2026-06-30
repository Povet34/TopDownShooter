using UnityEngine;

namespace TDS.Core
{
    /// <summary>
    /// 사망 시 보험/부분반출 계산(순수, 테스트 가능). 휴대 전리품(통화·아이템 수)의 일정 비율을 회수한다.
    /// 회수량 = floor(amount × clamp01(rate)). 회수분을 스태시에 넣고 나머지를 잃는 처리는 글루가 담당.
    /// </summary>
    public static class Insurance
    {
        public static int Recovered(int amount, float rate)
        {
            if (amount <= 0 || rate <= 0f) return 0;
            return Mathf.FloorToInt(amount * Mathf.Clamp01(rate));
        }
    }
}
