using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TDS.Tests.PlayMode
{
    /// <summary>수송선 탈출 존: 머무르면 탈출(승리) + 휴대 전리품 반출. 밖이면 진행 안 함.</summary>
    public class ExtractionZoneTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var z in Object.FindObjectsByType<ExtractionZone>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (z != null) Object.DestroyImmediate(z.gameObject);
            foreach (var pl in Object.FindObjectsByType<PlayerLoot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (pl != null) Object.DestroyImmediate(pl.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Player_dwelling_extracts_and_banks_loot()
        {
            var zone = new GameObject("Dropship").AddComponent<ExtractionZone>();
            zone.transform.position = Vector3.zero;
            zone.Configure(5f, 0.4f); // 반경 5, 0.4초 탑승

            var player = new GameObject("Player") { tag = "Player" };
            player.transform.position = Vector3.zero; // 존 안
            var loot = player.AddComponent<PlayerLoot>();
            loot.Wallet.Add(5);

            float t = 0f;
            while (t < 0.7f) { yield return null; t += Time.deltaTime; }

            Assert.IsTrue(zone.IsExtracted, "탑승 시간이 지났는데 탈출하지 않음");
            Assert.AreEqual(5, zone.BankedOnExtract, "반출량 불일치");
            Assert.AreEqual(0, loot.Wallet.Carried, "반출 후 휴대분이 비워지지 않음");
            Assert.AreEqual(5, loot.Wallet.Banked);

            Object.DestroyImmediate(player);
        }

        [UnityTest]
        public IEnumerator Player_outside_zone_does_not_extract()
        {
            var zone = new GameObject("Dropship").AddComponent<ExtractionZone>();
            zone.transform.position = Vector3.zero;
            zone.Configure(3f, 0.3f);

            var player = new GameObject("Player") { tag = "Player" };
            player.transform.position = new Vector3(50f, 0f, 0f); // 멀리

            float t = 0f;
            while (t < 0.6f) { yield return null; t += Time.deltaTime; }

            Assert.IsFalse(zone.IsExtracted, "존 밖인데 탈출됨");
            Assert.IsFalse(zone.PlayerInZone);

            Object.DestroyImmediate(player);
        }
    }
}
