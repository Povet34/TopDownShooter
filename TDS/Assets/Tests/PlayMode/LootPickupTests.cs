using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TDS.Tests.PlayMode
{
    /// <summary>전리품 글루: 적 사망 드랍이 픽업을 만들고, 픽업 획득이 플레이어 지갑에 더한다.</summary>
    public class LootPickupTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var p in Object.FindObjectsByType<LootPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (p != null) Object.DestroyImmediate(p.gameObject);
            foreach (var d in Object.FindObjectsByType<LootDropper>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (d != null) Object.DestroyImmediate(d.gameObject);
            foreach (var pl in Object.FindObjectsByType<PlayerLoot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (pl != null) Object.DestroyImmediate(pl.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Dropper_spawns_pickup_when_it_drops()
        {
            var enemyGo = new GameObject("EnemyStub");
            var dropper = enemyGo.AddComponent<LootDropper>();
            dropper.Configure(null, 1f, 1, 1); // 확률 100%
            dropper.DropLoot();
            yield return null;

            var pickups = Object.FindObjectsByType<LootPickup>(FindObjectsSortMode.None);
            Assert.Greater(pickups.Length, 0, "드랍 확률 100%인데 픽업이 생성되지 않음");
        }

        [UnityTest]
        public IEnumerator Pickup_adds_value_to_player_wallet()
        {
            var player = new GameObject("Player") { tag = "Player" };
            var pickup = LootPickup.CreatePrimitive(Vector3.zero, 3);

            pickup.Collect(player);

            var loot = player.GetComponent<PlayerLoot>();
            Assert.IsNotNull(loot, "픽업이 PlayerLoot를 부착하지 않음");
            Assert.AreEqual(3, loot.Wallet.Carried, "지갑에 전리품이 더해지지 않음");

            yield return null; // 픽업 자기 제거 확인용 프레임
        }

        [UnityTest]
        public IEnumerator Pickup_collects_only_once()
        {
            var player = new GameObject("Player") { tag = "Player" };
            var pickup = LootPickup.CreatePrimitive(Vector3.zero, 2);

            pickup.Collect(player);
            pickup.Collect(player); // 중복 호출은 무시되어야

            Assert.AreEqual(2, player.GetComponent<PlayerLoot>().Wallet.Carried, "중복 획득됨");
            yield return null;
        }
    }
}
