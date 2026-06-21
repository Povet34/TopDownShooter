using UnityEngine;

namespace TDS.Core
{
    /// <summary>
    /// 교전 이동 입력. 플레이어 위치·시야방향, 적 현재위치, 선호 교전거리/시야각, 가중치.
    /// </summary>
    public struct BattleMoveContext
    {
        public Vector3 playerPos;
        public Vector3 playerForward;   // 플레이어가 바라보는(조준) 방향
        public Vector3 enemyPos;        // 적 현재 위치
        public float preferredDistance; // 선호 교전 거리(링 반경)
        public float viewHalfAngleDeg;  // 플레이어 시야 콘 반각

        // 가중치(클수록 그 항을 더 중시)
        public float wView;    // 시야 정면 회피
        public float wDist;    // 선호 교전거리 유지
        public float wInertia; // 적게 움직이기(관성/이력 → 떨림 방지·자연 분산)
    }

    /// <summary>
    /// 시야-회피 유틸리티 이동(순수, §12 1차). 플레이어 시야 정면을 피하는 후보 목적지를 점수로 골라
    /// "포위하려" 하지 않아도 결과적으로 플랭크/뒤로 모이게 한다(창발적 포위). 유니티 수학만 사용.
    /// </summary>
    public static class BattleMover
    {
        /// <summary>
        /// 위치가 플레이어 시야 콘에 얼마나 노출됐는지 [0,1]. 1=정면 중심, 0=콘 밖/뒤.
        /// </summary>
        public static float FrontExposure(Vector3 pos, Vector3 playerPos, Vector3 playerForward, float halfAngleDeg)
        {
            Vector3 toPos = pos - playerPos; toPos.y = 0f;
            Vector3 fwd = playerForward; fwd.y = 0f;

            if (toPos.sqrMagnitude < 1e-6f || fwd.sqrMagnitude < 1e-6f)
                return 1f; // 플레이어 위 또는 방향 불명 → 최대 노출로 간주

            toPos.Normalize();
            fwd.Normalize();

            float cosA = Vector3.Dot(fwd, toPos);
            float cosHalf = Mathf.Cos(halfAngleDeg * Mathf.Deg2Rad);

            if (cosA <= cosHalf)
                return 0f; // 콘 밖

            return (cosA - cosHalf) / (1f - cosHalf); // 콘 안: 가장자리 0 → 중심 1
        }

        /// <summary>후보 목적지 점수(높을수록 좋음). 모든 항은 페널티 → 음수 합.</summary>
        public static float Score(Vector3 candidate, in BattleMoveContext ctx)
        {
            float exposure = FrontExposure(candidate, ctx.playerPos, ctx.playerForward, ctx.viewHalfAngleDeg);

            Vector3 toPlayer = candidate - ctx.playerPos; toPlayer.y = 0f;
            float distErr = Mathf.Abs(toPlayer.magnitude - ctx.preferredDistance);

            Vector3 move = candidate - ctx.enemyPos; move.y = 0f;
            float moveCost = move.magnitude;

            return -(ctx.wView * exposure + ctx.wDist * distErr + ctx.wInertia * moveCost);
        }

        /// <summary>
        /// 플레이어 주변 preferredDistance 링에 후보를 생성·점수화해 최선의 교전 위치를 고른다.
        /// 후보가 없으면 적 현재 위치(제자리).
        /// </summary>
        public static Vector3 PickEngagePosition(in BattleMoveContext ctx, int candidateCount = 16)
        {
            if (candidateCount < 1)
                return ctx.enemyPos;

            Vector3 best = ctx.enemyPos;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < candidateCount; i++)
            {
                float ang = (float)i / candidateCount * Mathf.PI * 2f;
                Vector3 cand = ctx.playerPos + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * ctx.preferredDistance;
                cand.y = ctx.enemyPos.y;

                float s = Score(cand, ctx);
                if (s > bestScore)
                {
                    bestScore = s;
                    best = cand;
                }
            }

            return best;
        }
    }
}
