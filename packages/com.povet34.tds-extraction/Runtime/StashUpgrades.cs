using System.Collections.Generic;
using System.Text;

namespace TDS.Core
{
    /// <summary>구매 가능한 영구 업그레이드 정의(불변 데이터). 비용은 레벨당 선형 증가.</summary>
    public class UpgradeDef
    {
        public string Id { get; }
        public string Name { get; }
        public int BaseCost { get; }
        public int MaxLevel { get; }
        public float PerLevel { get; }   // 레벨당 효과량(예: +25 HP, +0.08 속도)
        public string Unit { get; }

        public UpgradeDef(string id, string name, int baseCost, int maxLevel, float perLevel, string unit)
        {
            Id = id; Name = name; BaseCost = baseCost; MaxLevel = maxLevel; PerLevel = perLevel; Unit = unit;
        }
    }

    /// <summary>
    /// 스태시 통화로 사는 영구 업그레이드(순수, 테스트 가능). 정의는 코드 고정(<see cref="Default"/>),
    /// <b>레벨만</b> 영속화한다. 비용 = BaseCost×(현재레벨+1). 통화 차감은 글루가 <see cref="MetaStash.TrySpend"/>로,
    /// 효과 적용(최대 체력/이동속도 등)도 글루가 <see cref="TotalBonus"/>를 읽어 얹는다.
    /// </summary>
    public class StashUpgrades
    {
        private readonly List<UpgradeDef> defs;
        private readonly Dictionary<string, int> levels = new Dictionary<string, int>();

        public StashUpgrades(IEnumerable<UpgradeDef> definitions) { defs = new List<UpgradeDef>(definitions); }

        /// <summary>기본 업그레이드 세트.</summary>
        public static StashUpgrades Default() => new StashUpgrades(new[]
        {
            new UpgradeDef("vitality",  "Vitality",  30, 5, 25f,   "max HP"),
            new UpgradeDef("swiftness", "Swiftness", 40, 5, 0.08f, "move speed"),
            new UpgradeDef("padding",   "Padding",   50, 4, 0.06f, "damage resist"),
            new UpgradeDef("firepower", "Firepower", 45, 5, 4f,    "bullet damage"),
            new UpgradeDef("munitions", "Munitions", 35, 5, 40f,   "reserve ammo"),
            new UpgradeDef("insurance", "Insurance", 60, 4, 0.2f,  "death recovery"),
        });

        public IReadOnlyList<UpgradeDef> Defs => defs;
        public UpgradeDef Def(string id) => defs.Find(d => d.Id == id);
        public int LevelOf(string id) => levels.TryGetValue(id, out var l) ? l : 0;

        public bool IsMaxed(string id)
        {
            var d = Def(id);
            return d != null && LevelOf(id) >= d.MaxLevel;
        }

        /// <summary>다음 레벨 구매 비용(최대치면 0).</summary>
        public int CostOf(string id)
        {
            var d = Def(id);
            if (d == null || IsMaxed(id)) return 0;
            return d.BaseCost * (LevelOf(id) + 1);
        }

        public bool CanAfford(string id, int currency)
        {
            var d = Def(id);
            return d != null && !IsMaxed(id) && currency >= CostOf(id);
        }

        /// <summary>레벨을 1 올린다(통화 차감/검증은 호출자 책임). 최대치면 false.</summary>
        public bool Purchase(string id)
        {
            var d = Def(id);
            if (d == null || IsMaxed(id)) return false;
            levels[id] = LevelOf(id) + 1;
            return true;
        }

        /// <summary>현재 레벨까지의 누적 효과량(레벨×PerLevel).</summary>
        public float TotalBonus(string id) => LevelOf(id) * (Def(id)?.PerLevel ?? 0f);

        public string SerializeLevels()
        {
            var sb = new StringBuilder();
            foreach (var kv in levels)
            {
                if (kv.Value <= 0) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(kv.Key).Append(':').Append(kv.Value);
            }
            return sb.ToString();
        }

        public void LoadLevels(string data)
        {
            levels.Clear();
            if (string.IsNullOrEmpty(data)) return;
            foreach (var pair in data.Split(';'))
            {
                int colon = pair.LastIndexOf(':');
                if (colon <= 0) continue;
                string id = pair.Substring(0, colon);
                if (int.TryParse(pair.Substring(colon + 1), out int lv) && lv > 0 && Def(id) != null)
                    levels[id] = lv;
            }
        }
    }
}
