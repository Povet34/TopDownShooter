using System;
using System.Collections.Generic;

namespace TDS.Core
{
    /// <summary>그리드에 놓인 아이템 1개 — 앵커 좌표(X,Y) + 회전 여부. 회전 시 유효 W/H가 swap된다.</summary>
    public class PlacedItem
    {
        public InventoryItem Item { get; }
        public int X { get; }
        public int Y { get; }
        public bool Rotated { get; }

        public int Width => Rotated ? Item.Height : Item.Width;
        public int Height => Rotated ? Item.Width : Item.Height;

        public PlacedItem(InventoryItem item, int x, int y, bool rotated)
        {
            Item = item;
            X = x;
            Y = y;
            Rotated = rotated;
        }

        /// <summary>(cx,cy) 셀을 이 아이템이 덮는가.</summary>
        public bool Covers(int cx, int cy) =>
            cx >= X && cx < X + Width && cy >= Y && cy < Y + Height;
    }

    /// <summary>
    /// 타르코프/디아블로식 멀티셀 그리드 인벤토리(순수, 테스트 가능). 아이템은 W×H 셀을 차지하며
    /// 서로 겹치거나 경계를 벗어날 수 없다. 회전(W/H swap)과 자동 배치(첫 빈자리)를 지원.
    /// MonoBehaviour/UI/픽업은 이 위에 얇게 얹는다(슬라이스: I키 패널 + 루트→수납).
    /// </summary>
    public class InventoryGrid
    {
        public int Width { get; }
        public int Height { get; }

        private readonly PlacedItem[,] cells;          // null = 빈 칸
        private readonly List<PlacedItem> items = new List<PlacedItem>();
        public IReadOnlyList<PlacedItem> Items => items;

        public InventoryGrid(int width, int height)
        {
            if (width < 1 || height < 1)
                throw new ArgumentOutOfRangeException(nameof(width), "그리드는 최소 1×1");

            Width = width;
            Height = height;
            cells = new PlacedItem[width, height];
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        /// <summary>해당 셀을 차지하는 아이템(없으면 null).</summary>
        public PlacedItem ItemAt(int x, int y) => InBounds(x, y) ? cells[x, y] : null;

        public bool IsOccupied(int x, int y) => ItemAt(x, y) != null;

        public int FreeCellCount
        {
            get
            {
                int n = 0;
                for (int x = 0; x < Width; x++)
                    for (int y = 0; y < Height; y++)
                        if (cells[x, y] == null) n++;
                return n;
            }
        }

        /// <summary>아이템을 (x,y)에 (회전 포함) 놓을 수 있는가 — 경계 안 + 모든 대상 셀이 비어있어야.</summary>
        public bool CanPlace(InventoryItem item, int x, int y, bool rotated = false)
        {
            if (item == null) return false;

            int w = rotated ? item.Height : item.Width;
            int h = rotated ? item.Width : item.Height;

            if (x < 0 || y < 0 || x + w > Width || y + h > Height) return false;

            for (int dx = 0; dx < w; dx++)
                for (int dy = 0; dy < h; dy++)
                    if (cells[x + dx, y + dy] != null) return false;

            return true;
        }

        /// <summary>배치 시도 — 성공 시 PlacedItem, 불가 시 null(그리드 불변).</summary>
        public PlacedItem Place(InventoryItem item, int x, int y, bool rotated = false)
        {
            if (!CanPlace(item, x, y, rotated)) return null;

            var placed = new PlacedItem(item, x, y, rotated);
            for (int dx = 0; dx < placed.Width; dx++)
                for (int dy = 0; dy < placed.Height; dy++)
                    cells[x + dx, y + dy] = placed;

            items.Add(placed);
            return placed;
        }

        /// <summary>
        /// 첫 빈자리에 자동 배치(좌상→우하 행 우선 스캔). 정사각형이 아니면 회전도 시도.
        /// 루트 픽업 시 호출(수납 위치를 자동 결정).
        /// </summary>
        public bool TryAutoPlace(InventoryItem item, out PlacedItem placed)
        {
            placed = null;
            if (item == null) return false;

            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    if (CanPlace(item, x, y, false)) { placed = Place(item, x, y, false); return true; }
                    if (item.Width != item.Height && CanPlace(item, x, y, true)) { placed = Place(item, x, y, true); return true; }
                }

            return false;
        }

        /// <summary>아이템 제거 — 차지하던 셀을 비운다.</summary>
        public bool Remove(PlacedItem placed)
        {
            if (placed == null || !items.Remove(placed)) return false;

            for (int dx = 0; dx < placed.Width; dx++)
                for (int dy = 0; dy < placed.Height; dy++)
                {
                    int cx = placed.X + dx, cy = placed.Y + dy;
                    if (InBounds(cx, cy) && cells[cx, cy] == placed) cells[cx, cy] = null;
                }

            return true;
        }

        public void Clear()
        {
            Array.Clear(cells, 0, cells.Length);
            items.Clear();
        }
    }
}
