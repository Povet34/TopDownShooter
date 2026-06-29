using UnityEngine;

namespace TDS.Core
{
    /// <summary>
    /// 상시 로밍 분대 디렉터의 순수 수학(기획 2026-06-22). 웨이브를 대체해 맵에 분대를 상시 유지한다:
    /// 가장자리에서 스폰 → 처음 정한 방향 그대로 순찰(플레이어 추적 안 함) → 순찰 상태로 맵 가장자리에
    /// 닿으면 디스폰 → 디렉터가 새 가장자리에서 새 분대를 리스폰. 정사각 맵(중심+halfExtent) 기준.
    /// 글루(SpawnDirector·Squad)는 이 시임을 호출만 한다.
    /// </summary>
    public static class SquadRoam
    {
        /// <summary>
        /// 맵 경계 둘레의 한 점. perimeterT in [0,1) 가 둘레 비율(0=북쪽 모서리에서 시계방향).
        /// 결과는 항상 경계 위(max(|dx|,|dz|)==halfExtent). y는 center.y.
        /// </summary>
        public static Vector3 EdgeSpawnPoint(Vector3 center, float halfExtent, float perimeterT)
        {
            float u = Mathf.Repeat(perimeterT, 1f) * 4f; // 0..4 (네 변)
            int side = Mathf.Min(3, (int)u);
            float frac = u - side;
            float a = Mathf.Lerp(-halfExtent, halfExtent, frac);

            Vector3 p;
            switch (side)
            {
                case 0:  p = new Vector3(a, 0f, halfExtent); break;   // 북(+z)
                case 1:  p = new Vector3(halfExtent, 0f, -a); break;  // 동(+x)
                case 2:  p = new Vector3(-a, 0f, -halfExtent); break; // 남(-z)
                default: p = new Vector3(-halfExtent, 0f, a); break;  // 서(-x)
            }
            return new Vector3(center.x + p.x, center.y, center.z + p.z);
        }

        /// <summary>
        /// 첫 순찰 방향 = 대상(플레이어) 쪽 평면 방향(정규화). 가장자리 스폰이라 안쪽(플레이어)으로 향해야
        /// 벽에 박혀 바로 디스폰되지 않는다. 겹치면 forward 폴백. 이후엔 NextPatrolDirection으로 고정.
        /// </summary>
        public static Vector3 InitialPatrolDirection(Vector3 from, Vector3 toward)
        {
            Vector3 d = toward - from; d.y = 0f;
            if (d.sqrMagnitude < 1e-6f)
                return Vector3.forward;
            return d.normalized;
        }

        /// <summary>
        /// 순찰 전진 방향(고정). 플레이어를 추적(재조정)하지 않고 처음 정한 방향을 그대로 유지하다가,
        /// 길이 막히면(blocked=true, 맵 끝/네브메시 밖) 반대로 반전만 한다.
        /// </summary>
        public static Vector3 NextPatrolDirection(Vector3 currentDir, bool blocked)
            => blocked ? -currentDir : currentDir;

        /// <summary>분대 중심이 맵 가장자리(안쪽 사각 밖, margin 이내)에 닿았는가.</summary>
        public static bool IsAtEdge(Vector3 centroid, Vector3 center, float halfExtent, float margin)
        {
            float dx = Mathf.Abs(centroid.x - center.x);
            float dz = Mathf.Abs(centroid.z - center.z);
            float inner = halfExtent - margin;
            return dx >= inner || dz >= inner;
        }

        /// <summary>디스폰 조건: 순찰(비교전) 상태 + 가장자리 도달. 교전 중이면 가장자리여도 남는다.</summary>
        public static bool ShouldDespawn(bool patrolling, bool atEdge) => patrolling && atEdge;

        /// <summary>상시 유지 목표(maxSquads)까지 부족한 만큼 새로 스폰할 분대 수.</summary>
        public static int SquadsToSpawn(int currentCount, int maxSquads)
            => Mathf.Max(0, maxSquads - currentCount);
    }
}
