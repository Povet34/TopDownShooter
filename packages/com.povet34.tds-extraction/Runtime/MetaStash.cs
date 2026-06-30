using System.Collections.Generic;
using System.Text;

namespace TDS.Core
{
    /// <summary>
    /// 런 사이에 유지되는 반출(extraction) 전리품 스태시(순수, 테스트 가능). 탈출 성공 시 휴대 통화 +
    /// 인벤토리 아이템을 누적한다. 영속화는 글루가 <see cref="Serialize"/>/<see cref="Deserialize"/>
    /// 문자열로 PlayerPrefs 등에 저장. UnityEngine 의존 없음.
    /// </summary>
    public class MetaStash
    {
        public int Currency { get; private set; }

        private readonly Dictionary<string, int> items = new Dictionary<string, int>();
        public IReadOnlyDictionary<string, int> Items => items;

        public int TotalItemCount
        {
            get { int n = 0; foreach (var v in items.Values) n += v; return n; }
        }

        public void AddCurrency(int amount) { if (amount > 0) Currency += amount; }

        /// <summary>업그레이드 구매 등에 통화를 지출 — 잔액이 충분할 때만 차감하고 true.</summary>
        public bool TrySpend(int amount)
        {
            if (amount <= 0 || Currency < amount) return false;
            Currency -= amount;
            return true;
        }

        public void AddItem(string id, int count = 1)
        {
            if (string.IsNullOrEmpty(id) || count <= 0) return;
            items[id] = (items.TryGetValue(id, out var c) ? c : 0) + count;
        }

        public void Clear() { Currency = 0; items.Clear(); }

        /// <summary>"currency|id:count;id:count" 형식으로 직렬화.</summary>
        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.Append(Currency).Append('|');
            bool first = true;
            foreach (var kv in items)
            {
                if (!first) sb.Append(';');
                sb.Append(kv.Key).Append(':').Append(kv.Value);
                first = false;
            }
            return sb.ToString();
        }

        /// <summary>역직렬화(깨진 입력은 무시하고 가능한 만큼 복원).</summary>
        public static MetaStash Deserialize(string data)
        {
            var stash = new MetaStash();
            if (string.IsNullOrEmpty(data)) return stash;

            int bar = data.IndexOf('|');
            if (bar < 0) return stash;
            if (int.TryParse(data.Substring(0, bar), out int cur) && cur > 0)
                stash.Currency = cur;

            string rest = data.Substring(bar + 1);
            if (rest.Length == 0) return stash;
            foreach (var pair in rest.Split(';'))
            {
                int colon = pair.LastIndexOf(':');
                if (colon <= 0) continue;
                string id = pair.Substring(0, colon);
                if (int.TryParse(pair.Substring(colon + 1), out int cnt))
                    stash.AddItem(id, cnt);
            }
            return stash;
        }
    }
}
