using System;
using NUnit.Framework;
using TDS.Core;

namespace TDS.Tests.EditMode
{
    public class InventoryGridTests
    {
        private static InventoryItem Item(int w, int h, string id = "x") => new InventoryItem(id, w, h);

        [Test]
        public void New_grid_is_empty()
        {
            var g = new InventoryGrid(4, 4);
            Assert.AreEqual(16, g.FreeCellCount);
            Assert.AreEqual(0, g.Items.Count);
            Assert.IsNull(g.ItemAt(0, 0));
            Assert.IsFalse(g.IsOccupied(2, 3));
        }

        [Test]
        public void Place_1x1_occupies_one_cell()
        {
            var g = new InventoryGrid(4, 4);
            var p = g.Place(Item(1, 1), 2, 1);
            Assert.IsNotNull(p);
            Assert.AreSame(p, g.ItemAt(2, 1));
            Assert.AreEqual(15, g.FreeCellCount);
            Assert.AreEqual(1, g.Items.Count);
        }

        [Test]
        public void Place_2x2_occupies_four_cells()
        {
            var g = new InventoryGrid(4, 4);
            var p = g.Place(Item(2, 2), 1, 1);
            Assert.IsNotNull(p);
            Assert.AreSame(p, g.ItemAt(1, 1));
            Assert.AreSame(p, g.ItemAt(2, 2));
            Assert.IsNull(g.ItemAt(3, 3));
            Assert.AreEqual(12, g.FreeCellCount);
        }

        [Test]
        public void CanPlace_false_out_of_bounds()
        {
            var g = new InventoryGrid(4, 4);
            Assert.IsFalse(g.CanPlace(Item(2, 2), 3, 3)); // 3+2 = 5 > 4
            Assert.IsFalse(g.CanPlace(Item(1, 1), -1, 0));
            Assert.IsFalse(g.CanPlace(Item(1, 1), 0, 4));
        }

        [Test]
        public void CanPlace_false_when_overlapping()
        {
            var g = new InventoryGrid(4, 4);
            g.Place(Item(2, 2), 0, 0);
            Assert.IsFalse(g.CanPlace(Item(1, 1), 1, 1));
            Assert.IsTrue(g.CanPlace(Item(1, 1), 2, 2));
        }

        [Test]
        public void Place_returns_null_and_leaves_grid_intact_on_overlap()
        {
            var g = new InventoryGrid(4, 4);
            g.Place(Item(2, 2), 0, 0);
            int free = g.FreeCellCount;
            Assert.IsNull(g.Place(Item(2, 2), 1, 1));
            Assert.AreEqual(free, g.FreeCellCount);
            Assert.AreEqual(1, g.Items.Count);
        }

        [Test]
        public void Remove_frees_cells()
        {
            var g = new InventoryGrid(4, 4);
            var p = g.Place(Item(2, 2), 1, 1);
            Assert.IsTrue(g.Remove(p));
            Assert.AreEqual(16, g.FreeCellCount);
            Assert.IsNull(g.ItemAt(1, 1));
            Assert.AreEqual(0, g.Items.Count);
            Assert.IsFalse(g.Remove(p)); // 두 번째 제거는 false
        }

        [Test]
        public void TryAutoPlace_uses_first_free_cell_row_major()
        {
            var g = new InventoryGrid(3, 3);
            g.Place(Item(1, 1), 0, 0); // (0,0) 점유
            Assert.IsTrue(g.TryAutoPlace(Item(1, 1), out var p));
            // 행 우선 스캔 → 다음 빈칸 (1,0)
            Assert.AreEqual(1, p.X);
            Assert.AreEqual(0, p.Y);
        }

        [Test]
        public void TryAutoPlace_fails_when_full()
        {
            var g = new InventoryGrid(2, 2);
            for (int i = 0; i < 4; i++)
                Assert.IsTrue(g.TryAutoPlace(Item(1, 1, "i" + i), out _));
            Assert.AreEqual(0, g.FreeCellCount);
            Assert.IsFalse(g.TryAutoPlace(Item(1, 1), out var none));
            Assert.IsNull(none);
        }

        [Test]
        public void Rotation_fits_where_unrotated_cannot()
        {
            var g = new InventoryGrid(3, 1);   // 3 wide, 1 tall
            var longItem = Item(1, 2);          // 1 wide, 2 tall → 세로로는 안 들어감
            Assert.IsFalse(g.CanPlace(longItem, 0, 0, rotated: false));
            Assert.IsTrue(g.CanPlace(longItem, 0, 0, rotated: true)); // 2×1 로 회전 → 들어감
        }

        [Test]
        public void TryAutoPlace_rotates_when_needed()
        {
            var g = new InventoryGrid(3, 1);
            Assert.IsTrue(g.TryAutoPlace(Item(1, 2), out var p));
            Assert.IsTrue(p.Rotated);
            Assert.AreEqual(2, p.Width);
            Assert.AreEqual(1, p.Height);
        }

        [Test]
        public void Rotated_placement_occupies_swapped_footprint()
        {
            var g = new InventoryGrid(3, 2);
            var p = g.Place(Item(1, 2), 0, 0, rotated: true); // 2×1
            Assert.IsNotNull(p);
            Assert.AreSame(p, g.ItemAt(0, 0));
            Assert.AreSame(p, g.ItemAt(1, 0));
            Assert.IsNull(g.ItemAt(0, 1)); // 세로로는 1칸만
        }

        [Test]
        public void PlacedItem_Covers_reports_footprint()
        {
            var g = new InventoryGrid(4, 4);
            var p = g.Place(Item(2, 2), 1, 1);
            Assert.IsTrue(p.Covers(1, 1));
            Assert.IsTrue(p.Covers(2, 2));
            Assert.IsFalse(p.Covers(3, 3));
            Assert.IsFalse(p.Covers(0, 0));
        }

        [Test]
        public void Multiple_items_tracked_and_clear_resets()
        {
            var g = new InventoryGrid(4, 4);
            g.TryAutoPlace(Item(2, 1, "a"), out _);
            g.TryAutoPlace(Item(1, 2, "b"), out _);
            g.TryAutoPlace(Item(1, 1, "c"), out _);
            Assert.AreEqual(3, g.Items.Count);
            g.Clear();
            Assert.AreEqual(0, g.Items.Count);
            Assert.AreEqual(16, g.FreeCellCount);
        }

        [Test]
        public void Constructors_reject_bad_dimensions()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new InventoryGrid(0, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => new InventoryGrid(4, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new InventoryItem("x", 0, 1));
            Assert.Throws<ArgumentException>(() => new InventoryItem("", 1, 1));
        }

        // ---- free placement: CanPlaceIgnoring / TryMove ----

        [Test]
        public void CanPlaceIgnoring_treats_ignored_item_cells_as_free()
        {
            var g = new InventoryGrid(4, 4);
            var p = g.Place(Item(2, 2), 0, 0);
            // 같은 자리(겹침)인데 자기 자신을 무시하면 놓을 수 있음
            Assert.IsFalse(g.CanPlace(Item(2, 2), 0, 0));
            Assert.IsTrue(g.CanPlaceIgnoring(p.Item, 0, 0, false, p));
            // 한 칸 옆으로(자기와 겹침)도 무시 시 가능
            Assert.IsTrue(g.CanPlaceIgnoring(p.Item, 1, 1, false, p));
        }

        [Test]
        public void TryMove_relocates_and_frees_old_cells()
        {
            var g = new InventoryGrid(5, 5);
            var p = g.Place(Item(2, 1), 0, 0);
            Assert.IsTrue(g.TryMove(p, 3, 2, false));
            Assert.IsNull(g.ItemAt(0, 0));           // 옛 자리 비움
            Assert.IsNotNull(g.ItemAt(3, 2));        // 새 자리 점유
            Assert.IsNotNull(g.ItemAt(4, 2));
            Assert.AreEqual(1, g.Items.Count);
        }

        [Test]
        public void TryMove_can_overlap_its_own_previous_cells()
        {
            var g = new InventoryGrid(4, 4);
            var p = g.Place(Item(2, 2), 0, 0);
            Assert.IsTrue(g.TryMove(p, 1, 1, false)); // 옛 자리와 겹치는 위치로 이동
            Assert.IsNotNull(g.ItemAt(2, 2));
            Assert.IsNull(g.ItemAt(0, 0));
        }

        [Test]
        public void TryMove_with_rotation()
        {
            var g = new InventoryGrid(3, 3);
            var p = g.Place(Item(1, 2), 0, 0);        // 세로 1x2
            Assert.IsTrue(g.TryMove(p, 0, 0, true));  // 가로 2x1 로 회전
            Assert.IsNotNull(g.ItemAt(1, 0));
            Assert.IsNull(g.ItemAt(0, 1));
        }

        [Test]
        public void TryMove_fails_on_overlap_or_oob_and_leaves_grid_intact()
        {
            var g = new InventoryGrid(4, 4);
            var a = g.Place(Item(2, 2), 0, 0);
            var b = g.Place(Item(1, 1), 3, 3);
            int free = g.FreeCellCount;
            Assert.IsFalse(g.TryMove(b, 0, 0, false));  // a와 겹침
            Assert.IsFalse(g.TryMove(a, 3, 3, false));  // 경계 밖(3+2>4)
            Assert.AreSame(b, g.ItemAt(3, 3));          // 둘 다 그대로
            Assert.AreSame(a, g.ItemAt(0, 0));
            Assert.AreEqual(free, g.FreeCellCount);
        }

        [Test]
        public void TrySwap_exchanges_two_items_positions()
        {
            var g = new InventoryGrid(4, 4);
            var a = g.Place(Item(1, 1, "a"), 0, 0);
            var b = g.Place(Item(1, 1, "b"), 2, 2);
            Assert.IsTrue(g.TrySwap(a, b));
            Assert.AreEqual("b", g.ItemAt(0, 0).Item.Id);
            Assert.AreEqual("a", g.ItemAt(2, 2).Item.Id);
        }

        [Test]
        public void TrySwap_fails_when_sizes_dont_fit_and_reverts()
        {
            // a(2x2)@0,0 ; b(1x1)@3,3 ; c(1x1)@2,2 막아서 a가 b자리(3,3)에 못 들어감
            var g = new InventoryGrid(4, 4);
            var a = g.Place(Item(2, 2, "a"), 0, 0);
            var b = g.Place(Item(1, 1, "b"), 3, 3);
            var c = g.Place(Item(1, 1, "c"), 2, 2);
            Assert.IsFalse(g.TrySwap(a, b));     // a는 (3,3)서 경계 밖
            Assert.AreSame(a, g.ItemAt(0, 0));   // 전부 그대로
            Assert.AreSame(b, g.ItemAt(3, 3));
            Assert.AreSame(c, g.ItemAt(2, 2));
        }

        [Test]
        public void TrySwap_preserves_each_rotation()
        {
            var g = new InventoryGrid(4, 4);
            var a = g.Place(Item(2, 1, "a"), 0, 0);          // 가로
            var b = g.Place(Item(1, 2, "b"), 0, 2, false);   // 세로
            Assert.IsTrue(g.TrySwap(a, b));
            // a는 b자리(0,2)에 가로로, b는 a자리(0,0)에 세로로
            Assert.AreEqual("a", g.ItemAt(0, 2).Item.Id);
            Assert.AreEqual("a", g.ItemAt(1, 2).Item.Id);
            Assert.AreEqual("b", g.ItemAt(0, 0).Item.Id);
            Assert.AreEqual("b", g.ItemAt(0, 1).Item.Id);
        }
    }
}
