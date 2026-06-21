using NUnit.Framework;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class MapObjectClassifierTests
    {
        [Test]
        public void Low_cover_is_blocking_and_cover()
        {
            var r = MapObjectClassifier.Classify(0.7f, isCover: true, breakable: false, movable: false);
            Assert.IsTrue(r.HasFlag(MapObjectRole.Cover));
            Assert.IsFalse(r.HasFlag(MapObjectRole.Hide));
            Assert.IsTrue(r.HasFlag(MapObjectRole.Blocking));
        }

        [Test]
        public void Tall_cover_is_hide_not_cover()
        {
            var r = MapObjectClassifier.Classify(2.0f, isCover: true, breakable: false, movable: false);
            Assert.IsTrue(r.HasFlag(MapObjectRole.Hide));
            Assert.IsFalse(r.HasFlag(MapObjectRole.Cover));
        }

        [Test]
        public void Plain_obstacle_is_only_blocking()
        {
            var r = MapObjectClassifier.Classify(1.2f, isCover: false, breakable: false, movable: false);
            Assert.AreEqual(MapObjectRole.Blocking, r);
        }

        [Test]
        public void Breakable_movable_crate_carries_both_flags()
        {
            var r = MapObjectClassifier.Classify(0.7f, isCover: true, breakable: true, movable: true);
            Assert.IsTrue(r.HasFlag(MapObjectRole.Cover));
            Assert.IsTrue(r.HasFlag(MapObjectRole.Breakable));
            Assert.IsTrue(r.HasFlag(MapObjectRole.Movable));
        }
    }
}
