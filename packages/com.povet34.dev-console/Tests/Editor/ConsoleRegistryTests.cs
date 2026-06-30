using NUnit.Framework;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class ConsoleRegistryTests
    {
        [Test]
        public void Parse_splits_command_and_args()
        {
            var (cmd, args) = ConsoleRegistry.Parse("  tp  3   5 ");
            Assert.AreEqual("tp", cmd);
            Assert.AreEqual(new[] { "3", "5" }, args);
        }

        [Test]
        public void Parse_empty_yields_empty()
        {
            var (cmd, args) = ConsoleRegistry.Parse("   ");
            Assert.AreEqual("", cmd);
            Assert.AreEqual(0, args.Length);
        }

        [Test]
        public void Execute_runs_handler_with_args_and_returns_output()
        {
            var r = new ConsoleRegistry();
            string[] seen = null;
            r.Register("give", "give w h", a => { seen = a; return "ok"; });

            string outp = r.Execute("give 2 3");
            Assert.AreEqual("ok", outp);
            Assert.AreEqual(new[] { "2", "3" }, seen);
        }

        [Test]
        public void Execute_is_case_insensitive()
        {
            var r = new ConsoleRegistry();
            r.Register("Heal", "", _ => "healed");
            Assert.AreEqual("healed", r.Execute("HEAL"));
            Assert.IsTrue(r.Has("heal"));
        }

        [Test]
        public void Execute_unknown_command_reports()
        {
            var r = new ConsoleRegistry();
            StringAssert.Contains("unknown command: nope", r.Execute("nope arg"));
        }

        [Test]
        public void Execute_empty_input_is_empty()
        {
            Assert.AreEqual("", new ConsoleRegistry().Execute("   "));
        }

        [Test]
        public void Execute_catches_handler_exception()
        {
            var r = new ConsoleRegistry();
            r.Register("boom", "", _ => throw new System.InvalidOperationException("bad arg"));
            StringAssert.Contains("error: bad arg", r.Execute("boom"));
        }

        [Test]
        public void Register_overwrites_same_name_and_counts()
        {
            var r = new ConsoleRegistry();
            r.Register("a", "", _ => "1");
            r.Register("a", "", _ => "2");
            Assert.AreEqual(1, r.Count);
            Assert.AreEqual("2", r.Execute("a"));
        }
    }
}
