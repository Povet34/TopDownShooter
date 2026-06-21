using NUnit.Framework;
using UnityEngine;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class BattleMoverTests
    {
        private static BattleMoveContext Ctx(Vector3 enemyPos, float wView = 1f, float wDist = 0f, float wInertia = 0f)
        {
            return new BattleMoveContext
            {
                playerPos = Vector3.zero,
                playerForward = Vector3.forward, // +z를 바라봄
                enemyPos = enemyPos,
                preferredDistance = 3f,
                viewHalfAngleDeg = 60f,
                wView = wView,
                wDist = wDist,
                wInertia = wInertia,
            };
        }

        [Test]
        public void Exposure_max_directly_ahead()
        {
            Assert.AreEqual(1f, BattleMover.FrontExposure(new Vector3(0, 0, 5), Vector3.zero, Vector3.forward, 60f), 1e-3f);
        }

        [Test]
        public void Exposure_zero_behind()
        {
            Assert.AreEqual(0f, BattleMover.FrontExposure(new Vector3(0, 0, -5), Vector3.zero, Vector3.forward, 60f), 1e-4f);
        }

        [Test]
        public void Exposure_zero_to_the_side_outside_cone()
        {
            // 정면 +z, 반각 60° → 옆(+x, 90°)은 콘 밖
            Assert.AreEqual(0f, BattleMover.FrontExposure(new Vector3(5, 0, 0), Vector3.zero, Vector3.forward, 60f), 1e-4f);
        }

        [Test]
        public void Exposure_partial_inside_cone()
        {
            // 정면에서 ~30° (반각 60° 안) → 0과 1 사이
            float e = BattleMover.FrontExposure(new Vector3(3f, 0, 5f), Vector3.zero, Vector3.forward, 60f);
            Assert.Greater(e, 0f);
            Assert.Less(e, 1f);
        }

        [Test]
        public void On_top_of_player_is_max_exposure()
        {
            Assert.AreEqual(1f, BattleMover.FrontExposure(Vector3.zero, Vector3.zero, Vector3.forward, 60f), 1e-4f);
        }

        [Test]
        public void Score_prefers_behind_over_front()
        {
            var ctx = Ctx(new Vector3(0, 0, -3));
            float behind = BattleMover.Score(new Vector3(0, 0, -3), ctx);
            float front = BattleMover.Score(new Vector3(0, 0, 3), ctx);
            Assert.Greater(behind, front, "정면보다 뒤가 더 높은 점수여야 함");
        }

        [Test]
        public void Inertia_prefers_nearer_candidate()
        {
            // 시야 무시, 관성만 → 현재 위치에 가까운 후보가 더 높은 점수
            var ctx = Ctx(new Vector3(0, 0, -3), wView: 0f, wInertia: 1f);
            float near = BattleMover.Score(new Vector3(0, 0, -3.1f), ctx);
            float far = BattleMover.Score(new Vector3(3, 0, 0), ctx);
            Assert.Greater(near, far);
        }

        [Test]
        public void PickEngagePosition_avoids_the_front()
        {
            // 적이 플레이어 뒤(-z)에 있을 때 → 뒤/플랭크의 낮은 노출 지점을 고름
            var ctx = Ctx(new Vector3(0, 0, -3), wView: 1f, wInertia: 0.1f);
            Vector3 pick = BattleMover.PickEngagePosition(ctx);

            float exposure = BattleMover.FrontExposure(pick, ctx.playerPos, ctx.playerForward, ctx.viewHalfAngleDeg);
            Assert.Less(exposure, 0.5f, "고른 위치가 시야 정면(노출 높음)임");
            // 링 위(선호 거리 근처)
            float dist = new Vector2(pick.x, pick.z).magnitude;
            Assert.AreEqual(3f, dist, 0.2f, "선호 거리 링 위가 아님");
        }

        [Test]
        public void ViewAvoid_threatened_right_after_damage()
        {
            // 방금 피격(t=0) → threatened 가중치
            Assert.AreEqual(1f, BattleMover.ViewAvoidWeight(0f, 2.5f, calmWeight: 0f, threatenedWeight: 1f), 1e-4f);
        }

        [Test]
        public void ViewAvoid_calm_after_grace()
        {
            // 그레이스 경과 → calm(평소: 회피 안 함)
            Assert.AreEqual(0f, BattleMover.ViewAvoidWeight(3f, 2.5f, calmWeight: 0f, threatenedWeight: 1f), 1e-4f);
        }

        [Test]
        public void ViewAvoid_decays_within_grace()
        {
            // 절반 시점 → calm과 threatened의 중간
            float w = BattleMover.ViewAvoidWeight(1.25f, 2.5f, calmWeight: 0f, threatenedWeight: 1f);
            Assert.AreEqual(0.5f, w, 1e-3f);
        }

        [Test]
        public void ViewAvoid_no_grace_is_calm()
        {
            Assert.AreEqual(0f, BattleMover.ViewAvoidWeight(0f, 0f, calmWeight: 0f, threatenedWeight: 1f), 1e-4f);
        }

        [Test]
        public void PickEngagePosition_empty_candidates_stays_put()
        {
            var ctx = Ctx(new Vector3(1, 0, 1));
            Assert.AreEqual(new Vector3(1, 0, 1), BattleMover.PickEngagePosition(ctx, 0));
        }

        // ---- §12 2차: 소프트 간격(겹침 회피) ----

        [Test]
        public void Spacing_penalty_zero_when_no_allies()
        {
            var ctx = Ctx(Vector3.zero);
            Assert.AreEqual(0f, BattleMover.SpacingPenalty(new Vector3(0, 0, 3), ctx), 1e-6f);
        }

        [Test]
        public void Spacing_penalty_higher_when_ally_is_closer()
        {
            var ctx = Ctx(Vector3.zero);
            ctx.wSpacing = 1f;
            ctx.spacingRadius = 4f;
            ctx.allies = new[] { new Vector3(0, 0, 3) };

            float near = BattleMover.SpacingPenalty(new Vector3(0, 0, 3.5f), ctx); // 아군 0.5 거리
            float far = BattleMover.SpacingPenalty(new Vector3(0, 0, 0f), ctx);    // 아군 3 거리
            Assert.Greater(near, far, "아군에 가까운 후보가 더 큰 페널티여야 함");
        }

        [Test]
        public void Spacing_penalty_zero_outside_radius()
        {
            var ctx = Ctx(Vector3.zero);
            ctx.wSpacing = 1f;
            ctx.spacingRadius = 4f;
            ctx.allies = new[] { new Vector3(0, 0, 3) };
            Assert.AreEqual(0f, BattleMover.SpacingPenalty(new Vector3(0, 0, -10f), ctx), 1e-6f);
        }

        [Test]
        public void PickEngagePosition_spreads_away_from_clustered_allies()
        {
            // 적이 플레이어 뒤(-z), 아군들이 -z 한쪽에 뭉쳐 있음 → 다른 쪽 플랭크를 고르게 됨
            var ctx = Ctx(new Vector3(0, 0, -3), wView: 0.3f, wInertia: 0.05f);
            ctx.wSpacing = 2f;
            ctx.spacingRadius = 4f;
            ctx.allies = new[] { new Vector3(0, 0, -3), new Vector3(0.5f, 0, -3f) };

            Vector3 pick = BattleMover.PickEngagePosition(ctx);

            // 뭉친 아군(-z)에서 떨어진 곳(아군과의 최소 거리가 spacingRadius에 근접하거나 그 이상)
            float minAllyDist = Mathf.Min(
                Vector3.Distance(pick, ctx.allies[0]),
                Vector3.Distance(pick, ctx.allies[1]));
            Assert.Greater(minAllyDist, 2f, "뭉친 아군 근처를 골라 겹침");
        }
    }
}
