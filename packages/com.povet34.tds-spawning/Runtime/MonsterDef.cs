using UnityEngine;

namespace TDS.Core
{
    /// <summary>
    /// 몬스터 정의(데이터). 기존 적 프리팹을 래핑 + 스폰 메타(가중치/코스트/태그).
    /// 기존 Enemy AI를 그대로 재사용 — prefab 참조만 들고 있어 TDS.Core에 둔다.
    /// </summary>
    [CreateAssetMenu(fileName = "MD_", menuName = "TDS/Spawn/Monster Def")]
    public class MonsterDef : ScriptableObject
    {
        public string id;
        public GameObject prefab;
        [Min(0f)] public float weight = 1f;
        [Min(0)] public int cost = 1;
        public string[] tags;
    }
}
