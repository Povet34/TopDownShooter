using NUnit.Framework;
using UnityEngine;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class SquadRoamTests
    {
        private static Vector3 Center => new Vector3(5f, 0f, -3f);
        private const float H = 50f;

        [Test]
        public void EdgeSpawnPoint_always_lands_on_boundary()
        {
            for (int i = 0; i < 16; i++)
            {
                float t = i / 16f;
                Vector3 p = SquadRoam.EdgeSpawnPoint(Center, H, t);
                float dx = Mathf.Abs(p.x - Center.x);
                float dz = Mathf.Abs(p.z - Center.z);
                Assert.AreEqual(H, Mathf.Max(dx, dz), 1e-3f, $"t={t} 가 경계 위가 아님");
            }
        }

        [Test]
        public void EdgeSpawnPoint_wraps_perimeter()
        {
            // perimeterT는 둘레 비율이라 t와 t+1은 같은 점(부동소수 오차 허용).
            Vector3 a = SquadRoam.EdgeSpawnPoint(Center, H, 0.3f);
            Vector3 b = SquadRoam.EdgeSpawnPoint(Center, H, 1.3f);
            Assert.Less((a - b).magnitude, 1e-2f);
        }

        [Test]
        public void EdgeSpawnPoint_keeps_center_height()
        {
            Vector3 c = new Vector3(0f, 7f, 0f);
            Assert.AreEqual(7f, SquadRoam.EdgeSpawnPoint(c, H, 0.6f).y, 1e-5f);
        }

        [Test]
        public void InitialPatrolDirection_points_at_target_flat()
        {
            // 첫 방향은 플레이어(대상) 쪽 — 가장자리 스폰이라 안쪽으로 향해야 함.
            Vector3 d = SquadRoam.InitialPatrolDirection(new Vector3(0f, 0f, 10f), new Vector3(0f, 5f, 0f));
            Assert.AreEqual(0f, d.y, 1e-5f, "평면이어야");
            Assert.AreEqual(-1f, d.z, 1e-4f, "대상(원점) 쪽 -z");
            Assert.AreEqual(1f, d.magnitude, 1e-4f);
        }

        [Test]
        public void InitialPatrolDirection_falls_back_to_forward_when_overlapping()
        {
            Assert.AreEqual(Vector3.forward, SquadRoam.InitialPatrolDirection(Center, Center));
        }

        [Test]
        public void NextPatrolDirection_keeps_direction_when_clear()
        {
            // 길이 안 막히면 방향 고정(플레이어 추적 안 함).
            Vector3 dir = new Vector3(0.6f, 0f, -0.8f);
            Assert.AreEqual(dir, SquadRoam.NextPatrolDirection(dir, blocked: false));
        }

        [Test]
        public void NextPatrolDirection_reverses_when_blocked()
        {
            Vector3 dir = new Vector3(1f, 0f, 0f);
            Assert.AreEqual(-dir, SquadRoam.NextPatrolDirection(dir, blocked: true));
        }

        [Test]
        public void NextPatrolDirection_stays_fixed_over_many_clear_steps()
        {
            // 여러 번 전진해도(막힘 없음) 처음 방향 그대로 — "순찰 방향 고정" 보장.
            Vector3 dir = new Vector3(0f, 0f, 1f);
            Vector3 cur = dir;
            for (int i = 0; i < 20; i++)
                cur = SquadRoam.NextPatrolDirection(cur, blocked: false);
            Assert.AreEqual(dir, cur);
        }

        [Test]
        public void IsAtEdge_false_at_center_true_near_boundary()
        {
            Assert.IsFalse(SquadRoam.IsAtEdge(Center, Center, H, margin: 4f));
            Vector3 nearEast = Center + new Vector3(H - 2f, 0f, 0f); // inner=46, dx=48 >= 46
            Assert.IsTrue(SquadRoam.IsAtEdge(nearEast, Center, H, margin: 4f));
        }

        [Test]
        public void ShouldDespawn_only_when_patrolling_and_at_edge()
        {
            Assert.IsTrue(SquadRoam.ShouldDespawn(patrolling: true, atEdge: true));
            Assert.IsFalse(SquadRoam.ShouldDespawn(patrolling: false, atEdge: true), "교전 중이면 가장자리여도 남음");
            Assert.IsFalse(SquadRoam.ShouldDespawn(patrolling: true, atEdge: false));
        }

        [Test]
        public void SquadsToSpawn_fills_up_to_max_and_never_negative()
        {
            Assert.AreEqual(3, SquadRoam.SquadsToSpawn(currentCount: 0, maxSquads: 3));
            Assert.AreEqual(1, SquadRoam.SquadsToSpawn(currentCount: 2, maxSquads: 3));
            Assert.AreEqual(0, SquadRoam.SquadsToSpawn(currentCount: 5, maxSquads: 3));
        }
    }
}
