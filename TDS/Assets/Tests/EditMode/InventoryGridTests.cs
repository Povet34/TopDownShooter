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
    }
}
