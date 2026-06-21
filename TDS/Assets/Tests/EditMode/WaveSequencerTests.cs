using NUnit.Framework;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class WaveSequencerTests
    {
        [Test]
        public void Zero_waves_is_finished_immediately()
        {
            var seq = new WaveSequencer(0);
            Assert.IsTrue(seq.Finished);
            Assert.AreEqual(WaveAction.Done, seq.Decide(0, 0f, 0f));
        }

        [Test]
        public void First_decide_spawns_regardless_of_alive()
        {
            var seq = new WaveSequencer(3);
            Assert.AreEqual(-1, seq.CurrentWave);
            Assert.AreEqual(WaveAction.SpawnNext, seq.Decide(99, 0f, 0f));
        }

        [Test]
        public void Marks_spawned_advances_current_wave()
        {
            var seq = new WaveSequencer(3);
            seq.MarkSpawned();
            Assert.AreEqual(0, seq.CurrentWave);
            seq.MarkSpawned();
            Assert.AreEqual(1, seq.CurrentWave);
        }

        [Test]
        public void Waits_while_enemies_alive()
        {
            var seq = new WaveSequencer(3);
            seq.MarkSpawned(); // wave 0
            Assert.AreEqual(WaveAction.Wait, seq.Decide(2, 1f, 0f));
        }

        [Test]
        public void Advances_when_cleared()
        {
            var seq = new WaveSequencer(3);
            seq.MarkSpawned(); // wave 0
            Assert.AreEqual(WaveAction.SpawnNext, seq.Decide(0, 5f, 0f));
        }

        [Test]
        public void Last_wave_cleared_is_done()
        {
            var seq = new WaveSequencer(2);
            seq.MarkSpawned(); // wave 0
            seq.MarkSpawned(); // wave 1 (last)
            Assert.AreEqual(1, seq.CurrentWave);
            Assert.AreEqual(WaveAction.Done, seq.Decide(0, 5f, 0f));
        }

        [Test]
        public void Timeout_advances_even_with_enemies_alive()
        {
            var seq = new WaveSequencer(3);
            seq.MarkSpawned(); // wave 0
            // 적이 5마리 남았어도 maxWaveTime 10초 경과 → 진행
            Assert.AreEqual(WaveAction.SpawnNext, seq.Decide(5, 11f, 10f));
        }

        [Test]
        public void Zero_max_wave_time_disables_timeout()
        {
            var seq = new WaveSequencer(3);
            seq.MarkSpawned(); // wave 0
            // maxWaveTime=0 → 시간 아무리 흘러도 적 남으면 대기
            Assert.AreEqual(WaveAction.Wait, seq.Decide(5, 9999f, 0f));
        }

        [Test]
        public void Full_three_wave_run()
        {
            var seq = new WaveSequencer(3);
            // 첫 스폰
            Assert.AreEqual(WaveAction.SpawnNext, seq.Decide(0, 0f, 0f));
            seq.MarkSpawned();
            // 웨이브0 진행 중
            Assert.AreEqual(WaveAction.Wait, seq.Decide(3, 1f, 0f));
            // 웨이브0 클리어 → 웨이브1
            Assert.AreEqual(WaveAction.SpawnNext, seq.Decide(0, 2f, 0f));
            seq.MarkSpawned();
            // 웨이브1 클리어 → 웨이브2
            Assert.AreEqual(WaveAction.SpawnNext, seq.Decide(0, 2f, 0f));
            seq.MarkSpawned();
            // 웨이브2(마지막) 클리어 → 종료
            Assert.AreEqual(WaveAction.Done, seq.Decide(0, 2f, 0f));
            seq.MarkFinished();
            Assert.IsTrue(seq.Finished);
        }
    }
}
