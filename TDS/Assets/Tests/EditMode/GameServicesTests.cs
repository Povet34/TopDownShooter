using NUnit.Framework;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class GameServicesTests
    {
        private interface IThing { }
        private class Thing : IThing { }

        [SetUp]
        public void Setup() => GameServices.ResetForTests();

        [Test]
        public void Register_and_Resolve_via_global_registry()
        {
            var t = new Thing();
            GameServices.Registry.Register<IThing>(t);
            Assert.AreSame(t, GameServices.Registry.Resolve<IThing>());
        }

        [Test]
        public void ResetForTests_clears_registry()
        {
            GameServices.Registry.Register<IThing>(new Thing());
            GameServices.ResetForTests();
            Assert.IsFalse(GameServices.Registry.IsRegistered<IThing>());
        }
    }
}
