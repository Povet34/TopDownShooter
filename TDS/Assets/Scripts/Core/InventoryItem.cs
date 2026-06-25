using System;

namespace TDS.Core
{
    /// <summary>
    /// 인벤토리 아이템 정의(순수, 테스트 가능). 셀 단위 풋프린트(Width×Height)를 가진다.
    /// 타르코프/디아블로식 그리드 인벤토리의 한 칸/여러 칸 아이템. UI/픽업 글루는 이 위에 얹는다.
    /// </summary>
    public class InventoryItem
    {
        public string Id { get; }
        public string DisplayName { get; }
        public int Width { get; }
        public int Height { get; }

        public InventoryItem(string id, int width, int height, string displayName = null)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("id required", nameof(id));
            if (width < 1 || height < 1)
                throw new ArgumentOutOfRangeException(nameof(width), "풋프린트는 최소 1×1");

            Id = id;
            Width = width;
            Height = height;
            DisplayName = string.IsNullOrEmpty(displayName) ? id : displayName;
        }

        /// <summary>차지하는 셀 수(W×H).</summary>
        public int CellCount => Width * Height;
    }
}
