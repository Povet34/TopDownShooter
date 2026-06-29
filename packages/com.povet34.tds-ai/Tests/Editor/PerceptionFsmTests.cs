using NUnit.Framework;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    // §6.3 인지 FSM: 순찰 → 경계(조사) → 교전, 시야 상실 시 역방향.
    public class PerceptionFsmTests
    {
        private static PerceptionFsm New(float lose = 3f, float investigate = 5f)
            => new PerceptionFsm { LoseSightDuration = lose, InvestigateDuration = investigate };

        [Test]
        public void Starts_in_patrol()
        {
            Assert.AreEqual(PerceptionState.Patrol, New().State);
        }

        [Test]
        public void Patrol_seeing_player_engages()
        {
            var fsm = New();
            Assert.AreEqual(PerceptionState.Engage, fsm.Tick(seesPlayer: true, heardNoise: false, dt: 0.1f));
        }

        [Test]
        public void Patrol_hearing_noise_goes_to_alert()
        {
            var fsm = New();
            Assert.AreEqual(PerceptionState.Alert, fsm.Tick(seesPlayer: false, heardNoise: true, dt: 0.1f));
        }

        [Test]
        public void Patrol_quiet_stays_patrol()
        {
            var fsm = New();
            Assert.AreEqual(PerceptionState.Patrol, fsm.Tick(false, false, 0.1f));
        }

        [Test]
        public void Sight_beats_noise_from_patrol()
        {
            var fsm = New();
            Assert.AreEqual(PerceptionState.Engage, fsm.Tick(seesPlayer: true, heardNoise: true, dt: 0.1f));
        }

        [Test]
        public void Alert_seeing_player_engages()
        {
            var fsm = New();
            fsm.Tick(false, true, 0.1f); // → Alert
            Assert.AreEqual(PerceptionState.Engage, fsm.Tick(true, false, 0.1f));
        }

        [Test]
        public void Alert_times_out_back_to_patrol()
        {
            var fsm = New(investigate: 1f);
            fsm.Tick(false, true, 0.1f); // → Alert
            fsm.Tick(false, false, 0.6f); // 0.6 누적
            Assert.AreEqual(PerceptionState.Alert, fsm.State); // 아직
            Assert.AreEqual(PerceptionState.Patrol, fsm.Tick(false, false, 0.6f)); // 1.2 ≥ 1 → 순찰
        }

        [Test]
        public void New_noise_in_alert_refreshes_investigation()
        {
            var fsm = New(investigate: 1f);
            fsm.Tick(false, true, 0.1f);   // → Alert
            fsm.Tick(false, false, 0.9f);  // 0.9 누적(타임아웃 직전)
            fsm.Tick(false, true, 0.0f);   // 새 소음 → 타이머 리셋
            Assert.AreEqual(0f, fsm.InvestigateElapsed, 1e-5f);
            Assert.AreEqual(PerceptionState.Alert, fsm.Tick(false, false, 0.5f)); // 0.5 < 1 → 여전히 경계
        }

        [Test]
        public void Engage_keeps_engaging_while_seen()
        {
            var fsm = New();
            fsm.Tick(true, false, 0.1f); // → Engage
            Assert.AreEqual(PerceptionState.Engage, fsm.Tick(true, false, 10f));
            Assert.AreEqual(0f, fsm.TimeWithoutSight, 1e-5f);
        }

        [Test]
        public void Engage_brief_loss_keeps_engaging()
        {
            var fsm = New(lose: 3f);
            fsm.Tick(true, false, 0.1f); // → Engage
            Assert.AreEqual(PerceptionState.Engage, fsm.Tick(false, false, 1f)); // 1 < 3
        }

        [Test]
        public void Engage_long_loss_drops_to_alert()
        {
            var fsm = New(lose: 1f);
            fsm.Tick(true, false, 0.1f);  // → Engage
            fsm.Tick(false, false, 0.6f); // 0.6
            Assert.AreEqual(PerceptionState.Alert, fsm.Tick(false, false, 0.6f)); // 1.2 ≥ 1 → Alert
        }

        [Test]
        public void Regaining_sight_during_loss_resets_timer()
        {
            var fsm = New(lose: 1f);
            fsm.Tick(true, false, 0.1f);
            fsm.Tick(false, false, 0.8f); // 0.8 (아직 교전)
            fsm.Tick(true, false, 0.1f);  // 다시 봄 → 리셋
            Assert.AreEqual(PerceptionState.Engage, fsm.State);
            Assert.AreEqual(0f, fsm.TimeWithoutSight, 1e-5f);
        }

        [Test]
        public void Force_engage_from_patrol_without_sight()
        {
            var fsm = New();
            fsm.ForceEngage(); // 피격
            Assert.AreEqual(PerceptionState.Engage, fsm.State);
            // 시야 없어도 LoseSightDuration 동안은 교전 유지
            Assert.AreEqual(PerceptionState.Engage, fsm.Tick(false, false, fsm.LoseSightDuration - 0.1f));
        }

        [Test]
        public void Full_cycle_patrol_alert_engage_alert_patrol()
        {
            var fsm = New(lose: 1f, investigate: 1f);
            Assert.AreEqual(PerceptionState.Alert, fsm.Tick(false, true, 0.1f));   // 소음 → 경계
            Assert.AreEqual(PerceptionState.Engage, fsm.Tick(true, false, 0.1f));  // 발견 → 교전
            fsm.Tick(false, false, 0.6f);
            Assert.AreEqual(PerceptionState.Alert, fsm.Tick(false, false, 0.6f));  // 시야상실 → 경계
            fsm.Tick(false, false, 0.6f);
            Assert.AreEqual(PerceptionState.Patrol, fsm.Tick(false, false, 0.6f)); // 조사실패 → 순찰
        }
    }
}
