namespace TDS.Core
{
    /// <summary>맵 오브젝트의 용도(복수 가능). 높이로 Cover/Hide가 갈리고 Breakable/Movable은 부가.</summary>
    [System.Flags]
    public enum MapObjectRole
    {
        None = 0,
        Blocking = 1,    // navmesh/통행을 막는 정적 장애물
        Cover = 2,       // 낮은 단상(≤0.8) — 그 위로 사격
        Hide = 4,        // 높은 것 — 완전 은폐(못 쏨)
        Breakable = 8,   // 피격 누적 시 파괴
        Movable = 16,    // 총알/특정 공격에 밀림
    }

    /// <summary>높이 + 부가 속성으로 맵 오브젝트 역할(플래그)을 분류(순수).</summary>
    public static class MapObjectClassifier
    {
        public static MapObjectRole Classify(float height, bool isCover, bool breakable, bool movable)
        {
            MapObjectRole r = MapObjectRole.Blocking;

            if (isCover)
                r |= CoverEvaluation.IsShootable(height) ? MapObjectRole.Cover : MapObjectRole.Hide;

            if (breakable) r |= MapObjectRole.Breakable;
            if (movable) r |= MapObjectRole.Movable;

            return r;
        }
    }
}
