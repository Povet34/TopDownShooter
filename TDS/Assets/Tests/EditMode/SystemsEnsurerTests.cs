using NUnit.Framework;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class SystemsEnsurerTests
    {
        [Test]
        public void Spawns_when_absent()
        {
            int spawns = 0;
            bool present = false;
            bool created = SystemsEnsurer.Ensure(() => present, () => { spawns++; present = true; });

            Assert.IsTrue(created);
            Assert.AreEqual(1, spawns);
        }

        [Test]
        public void Skips_when_already_present()
        {
            int spawns = 0;
            bool created = SystemsEnsurer.Ensure(() => true, () => spawns++);

            Assert.IsFalse(created);
            Assert.AreEqual(0, spawns);
        }

        [Test]
        public void Idempotent_across_two_calls()
        {
            bool present = false;
            int spawns = 0;
            System.Func<bool> exists = () => present;
            System.Action spawn = () => { spawns++; present = true; };

            SystemsEnsurer.Ensure(exists, spawn);
            SystemsEnsurer.Ensure(exists, spawn);

            Assert.AreEqual(1, spawns);
        }
    }
}
