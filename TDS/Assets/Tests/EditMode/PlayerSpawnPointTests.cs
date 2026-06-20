using NUnit.Framework;
using UnityEngine;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class PlayerSpawnPointTests
    {
        [Test]
        public void Uses_center_xz_and_ground_y()
        {
            var p = PlayerSpawnPoint.Resolve(new Vector3(10f, 99f, -4f), 0.5f);
            Assert.AreEqual(10f, p.x, 1e-4f);
            Assert.AreEqual(0.5f, p.y, 1e-4f);
            Assert.AreEqual(-4f, p.z, 1e-4f);
        }

        [Test]
        public void Default_ground_is_zero()
        {
            var p = PlayerSpawnPoint.Resolve(new Vector3(3f, 50f, 7f));
            Assert.AreEqual(0f, p.y, 1e-4f);
        }
    }
}
