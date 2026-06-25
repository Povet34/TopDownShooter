using UnityEngine;
using TDS.Core;

/// <summary>플레이어가 보유한 전리품 지갑(글루). 순수 <see cref="LootWallet"/>를 들고 있음.</summary>
[DisallowMultipleComponent]
public class PlayerLoot : MonoBehaviour
{
    public LootWallet Wallet { get; } = new LootWallet();

    /// <summary>플레이어 오브젝트에 PlayerLoot 보장(없으면 추가). 프리팹 수정 없이 런타임 부착.</summary>
    public static PlayerLoot Ensure(GameObject player)
    {
        if (player == null) return null;
        return player.GetComponent<PlayerLoot>() ?? player.AddComponent<PlayerLoot>();
    }
}
