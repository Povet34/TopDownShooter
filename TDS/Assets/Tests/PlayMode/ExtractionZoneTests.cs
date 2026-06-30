using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TDS.Tests.PlayMode
{
    /// <summary>수송선 탈출 존: 호출 → 강하·착륙 후 머무르면 탈출(승리) + 전리품 반출. 호출 전/존 밖이면 진행 안 함.</summary>
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
        public IEnumerator Calling_then_landing_then_dwelling_extracts_and_banks()
        {
            var zone = new GameObject("Dropship").AddComponent<ExtractionZone>();
            zone.transform.position = Vector3.zero;
            zone.Configure(5f, 0.4f);   // 반경 5, 0.4초 탑승
            zone.DescendTime = 0.05f;   // 빠른 강하(테스트)

            var player = new GameObject("Player") { tag = "Player" };
            player.transform.position = Vector3.zero; // 착륙 지점
            var loot = player.AddComponent<PlayerLoot>();
            loot.Wallet.Add(5);

            zone.Call();

            float t = 0f;
            while (t < 1.0f && !zone.IsExtracted) { yield return null; t += Time.deltaTime; }

            Assert.IsTrue(zone.IsExtracted, "호출·착륙·탑승 후에도 탈출하지 않음");
            Assert.AreEqual(5, zone.BankedOnExtract, "반출량 불일치");
            Assert.AreEqual(0, loot.Wallet.Carried, "반출 후 휴대분이 비워지지 않음");
            Assert.AreEqual(5, loot.Wallet.Banked);

            Object.DestroyImmediate(player);
        }

        [UnityTest]
        public IEnumerator Not_called_does_not_extract_even_in_zone()
        {
            var zone = new GameObject("Dropship").AddComponent<ExtractionZone>();
            zone.transform.position = Vector3.zero;
            zone.Configure(5f, 0.3f); // 호출하지 않음 → 상공 대기

            var player = new GameObject("Player") { tag = "Player" };
            player.transform.position = Vector3.zero;

            float t = 0f;
            while (t < 0.6f) { yield return null; t += Time.deltaTime; }

            Assert.AreEqual(ExtractionZone.Stage.Hovering, zone.CurrentStage, "호출 안 했는데 강하/착륙함");
            Assert.IsFalse(zone.IsExtracted, "호출 전인데 탈출됨");

            Object.DestroyImmediate(player);
        }

        [UnityTest]
        public IEnumerator Outside_zone_does_not_extract_after_landing()
        {
            var zone = new GameObject("Dropship").AddComponent<ExtractionZone>();
            zone.transform.position = Vector3.zero;
            zone.Configure(3f, 0.3f);
            zone.DescendTime = 0.05f;

            var player = new GameObject("Player") { tag = "Player" };
            player.transform.position = new Vector3(50f, 0f, 0f); // 멀리

            zone.Call();

            float t = 0f;
            while (t < 0.6f) { yield return null; t += Time.deltaTime; }

            Assert.IsTrue(zone.IsLanded, "강하가 완료되지 않음");
            Assert.IsFalse(zone.IsExtracted, "존 밖인데 탈출됨");
            Assert.IsFalse(zone.PlayerInZone);

            Object.DestroyImmediate(player);
        }
    }
}
