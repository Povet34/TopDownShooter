using NUnit.Framework;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class BootSequenceTests
    {
        [Test]
        public void Plan_loads_Systems_before_Map()
        {
            var plan = BootSequence.Plan("Systems", "Map_Generated");
            Assert.AreEqual(2, plan.Count);
            Assert.AreEqual("Systems", plan[0]);
            Assert.AreEqual("Map_Generated", plan[1]);
        }

        [Test]
        public void Plan_skips_empty_scene_names()
        {
            var plan = BootSequence.Plan("Systems", "");
            Assert.AreEqual(1, plan.Count);
            Assert.AreEqual("Systems", plan[0]);
        }
    }
}
