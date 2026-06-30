using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TDS.Core;

namespace TDS.Tests.PlayMode
{
    /// <summary>인벤토리 글루: 루트 픽업이 그리드에 수납되고, 가득 차면 픽업이 보류된다(지갑·제거 모두 안 됨).</summary>
    public class InventoryGlueTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var p in Object.FindObjectsByType<LootPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (p != null) Object.DestroyImmediate(p.gameObject);
            foreach (var inv in Object.FindObjectsByType<PlayerInventory>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (inv != null) Object.DestroyImmediate(inv.gameObject);
            foreach (var pl in Object.FindObjectsByType<PlayerLoot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (pl != null) Object.DestroyImmediate(pl.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Pickup_stores_item_in_grid_and_wallet()
        {
            var player = new GameObject("Player") { tag = "Player" };
            var pickup = LootPickup.CreatePrimitive(Vector3.zero, 2);

            pickup.Collect(player);
            yield return null;

            var inv = player.GetComponent<PlayerInventory>();
            Assert.IsNotNull(inv, "픽업이 PlayerInventory를 부착하지 않음");
            Assert.AreEqual(1, inv.Grid.Items.Count, "그리드에 아이템이 수납되지 않음");
            Assert.AreEqual(2, player.GetComponent<PlayerLoot>().Wallet.Carried, "지갑에 더해지지 않음");
        }

        [UnityTest]
        public IEnumerator Full_inventory_blocks_pickup()
        {
            var player = new GameObject("Player") { tag = "Player" };
            var inv = PlayerInventory.Ensure(player);

            // 1x1로 가득 채움
            int guard = 0;
            while (inv.TryStore(new InventoryItem("fill", 1, 1)) && guard++ < 10000) { }
            Assert.AreEqual(0, inv.Grid.FreeCellCount, "그리드가 가득 차지 않음");
            Assert.IsTrue(inv.IsFull);

            var pickup = LootPickup.CreatePrimitive(Vector3.zero, 5);
            pickup.Collect(player);
            yield return null;

            // 보류 → 지갑 변화 없음 + 픽업 미제거
            var loot = player.GetComponent<PlayerLoot>();
            Assert.IsTrue(loot == null || loot.Wallet.Carried == 0, "가득 찼는데 지갑에 더해짐");
            Assert.IsTrue(pickup != null, "가득 찼는데 픽업이 제거됨");
        }

        [UnityTest]
        public IEnumerator Death_loses_all_carried_items()
        {
            var player = new GameObject("Player") { tag = "Player" };
            var inv = PlayerInventory.Ensure(player);
            inv.TryStore(new InventoryItem("a", 2, 2));
            inv.TryStore(new InventoryItem("b", 1, 1));
            Assert.AreEqual(2, inv.Grid.Items.Count);

            int lost = inv.LoseAll(); // 사망 시 휴대품 전손
            Assert.AreEqual(2, lost, "잃은 아이템 수가 틀림");
            Assert.AreEqual(0, inv.Grid.Items.Count, "그리드가 비워지지 않음");
            Assert.IsFalse(inv.IsFull);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MoveItem_relocates_stored_item()
        {
            var player = new GameObject("Player") { tag = "Player" };
            var inv = PlayerInventory.Ensure(player);
            inv.TryStore(new InventoryItem("a", 2, 1));
            var placed = inv.Grid.Items[0];

            Assert.IsTrue(inv.MoveItem(placed, 4, 3, false), "자유 이동 실패");
            Assert.IsNotNull(inv.Grid.ItemAt(4, 3), "새 위치 미점유");
            Assert.IsNull(inv.Grid.ItemAt(0, 0), "옛 위치 미해제");

            // 겹치는 곳으로는 이동 실패(그리드 불변)
            inv.TryStore(new InventoryItem("b", 1, 1)); // (0,0)에 자동배치
            var b = inv.Grid.ItemAt(0, 0);
            Assert.IsFalse(inv.MoveItem(placed, 0, 3, false) && inv.Grid.ItemAt(0, 0) != b);
            yield return null;
        }
    }
}
