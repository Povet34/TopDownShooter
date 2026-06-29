using NUnit.Framework;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class ServiceRegistryTests
    {
        private interface IFoo { }
        private class Foo : IFoo { }

        [Test]
        public void Register_then_Resolve_returns_same_instance()
        {
            var reg = new ServiceRegistry();
            var foo = new Foo();
            reg.Register<IFoo>(foo);
            Assert.AreSame(foo, reg.Resolve<IFoo>());
        }

        [Test]
        public void Resolve_unregistered_returns_null()
        {
            var reg = new ServiceRegistry();
            Assert.IsNull(reg.Resolve<IFoo>());
            Assert.IsFalse(reg.TryResolve<IFoo>(out _));
        }

        [Test]
        public void Register_replaces_existing_keeping_single_entry()
        {
            var reg = new ServiceRegistry();
            var a = new Foo();
            var b = new Foo();
            reg.Register<IFoo>(a);
            reg.Register<IFoo>(b);
            Assert.AreSame(b, reg.Resolve<IFoo>());
            Assert.AreEqual(1, reg.Count);
        }

        [Test]
        public void Unregister_and_Clear_remove_services()
        {
            var reg = new ServiceRegistry();
            reg.Register<IFoo>(new Foo());
            Assert.IsTrue(reg.IsRegistered<IFoo>());

            Assert.IsTrue(reg.Unregister<IFoo>());
            Assert.IsFalse(reg.IsRegistered<IFoo>());

            reg.Register<IFoo>(new Foo());
            reg.Clear();
            Assert.AreEqual(0, reg.Count);
        }
    }
}
