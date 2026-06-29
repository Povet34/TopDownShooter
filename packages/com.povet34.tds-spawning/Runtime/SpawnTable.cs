using System.Collections.Generic;
using UnityEngine;

namespace TDS.Core
{
    /// <summary>
    /// 스폰 테이블(데이터). MonsterDef들을 가중치로 묶는다. 상황/난이도/테마별로 여러 개.
    /// </summary>
    [CreateAssetMenu(fileName = "ST_", menuName = "TDS/Spawn/Spawn Table")]
    public class SpawnTable : ScriptableObject
    {
        public List<MonsterDef> entries = new List<MonsterDef>();

        /// <summary>roll∈[0,1)로 가중 선택. 비었으면 null.</summary>
        public MonsterDef Pick(float roll)
        {
            if (entries == null || entries.Count == 0)
                return null;

            var weights = new List<float>(entries.Count);
            foreach (var e in entries)
                weights.Add(e != null ? e.weight : 0f);

            int i = SpawnSelection.PickIndex(weights, roll);
            return i >= 0 ? entries[i] : null;
        }
    }
}
