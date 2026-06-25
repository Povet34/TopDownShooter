using UnityEngine;
using TDS.Core;

/// <summary>
/// 적 사망 시 전리품을 떨어뜨린다(글루). <see cref="Enemy.Die"/>가 <see cref="DropLoot"/>를 호출.
/// 드랍 확률/수량은 순수 <see cref="LootDrop"/>로 판정. 프리팹 미지정 시 프리미티브 코인 생성.
/// </summary>
[DisallowMultipleComponent]
public class LootDropper : MonoBehaviour
{
    [SerializeField] private GameObject lootPrefab;
    [SerializeField, Range(0f, 1f)] private float dropChance = 1f;
    [SerializeField] private int minAmount = 1;
    [SerializeField] private int maxAmount = 1;

    private static readonly System.Random rng = new System.Random();
    private bool dropped;

    public void Configure(GameObject prefab, float chance, int min, int max)
    {
        lootPrefab = prefab;
        dropChance = Mathf.Clamp01(chance);
        minAmount = Mathf.Max(1, min);
        maxAmount = Mathf.Max(minAmount, max);
    }

    public void DropLoot()
    {
        if (dropped) return;
        dropped = true;

        double roll, amtRoll;
        lock (rng) { roll = rng.NextDouble(); amtRoll = rng.NextDouble(); }
        if (!LootDrop.ShouldDrop(roll, dropChance)) return;

        int amount = LootDrop.Amount(amtRoll, minAmount, maxAmount);
        Vector3 pos = transform.position + Vector3.up * 0.5f;

        if (lootPrefab != null)
        {
            var go = Instantiate(lootPrefab, pos, Quaternion.identity);
            go.GetComponent<LootPickup>()?.Configure(amount);
        }
        else
        {
            LootPickup.CreatePrimitive(pos, amount);
        }
    }
}
