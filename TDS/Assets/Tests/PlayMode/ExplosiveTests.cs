using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TDS.Core;

namespace TDS.Tests.PlayMode
{
    /// <summary>폭발 배럴: 부서지면 범위 피해(거리 falloff) + 폭발음(90m) + 연쇄.</summary>
    public class ExplosiveTests
    {
        // 피해를 기록하는 더미 타깃(IDamagable).
        private class DamageProbe : MonoBehaviour, IDamagable
        {
            public int taken;
            public void TakeDamage(int damage) => taken += damage;
        }

        private DamageProbe MakeProbe(Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); // 콜라이더 포함(OverlapSphere가 찾음)
            go.transform.position = pos;
            return go.AddComponent<DamageProbe>();
        }

        private GameObject MakeBarrel(Vector3 pos)
        {
            var go = new GameObject("Barrel");
            go.SetActive(false);
            go.transform.position = pos;
            go.AddComponent<Breakable>();
            go.AddComponent<Explosive>(); // 기본 radius 6, maxDamage 80
            go.SetActive(true);
            return go;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var p in Object.FindObjectsByType<DamageProbe>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(p.gameObject);
            foreach (var b in Object.FindObjectsByType<Breakable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(b.gameObject);
            NoisePing.ClearForTests();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Explosion_damages_nearby_not_far_and_emits_explosion_noise()
        {
            NoisePing.ClearForTests();
            var barrel = MakeBarrel(Vector3.zero);
            var near = MakeProbe(new Vector3(2f, 0f, 0f));  // 반경 6 안
            var far = MakeProbe(new Vector3(12f, 0f, 0f));  // 반경 6 밖
            yield return null;

            ((IDamagable)barrel.GetComponent<Breakable>()).TakeDamage(999); // 파괴 → 폭발
            yield return null;

            Assert.Greater(near.taken, 0, "근처 대상이 폭발 피해를 안 받음");
            Assert.AreEqual(0, far.taken, "사거리 밖 대상이 피해를 받음");

            bool explosionHeard = false;
            foreach (var ch in NoisePing.ActiveChannels)
                if (ch.type == NoiseType.Explosion) explosionHeard = true;
            Assert.IsTrue(explosionHeard, "폭발음(Explosion)이 발신되지 않음");
        }

        [UnityTest]
        public IEnumerator Explosion_spawns_configured_fx()
        {
            var barrel = MakeBarrel(Vector3.zero);
            var fxPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fxPrefab.name = "ExplosionFX_TEST";
            fxPrefab.transform.position = new Vector3(100f, 0f, 0f); // 폭발 반경 밖(연쇄 무관)
            barrel.GetComponent<Explosive>().ExplosionFX = fxPrefab;
            yield return null;

            ((IDamagable)barrel.GetComponent<Breakable>()).TakeDamage(999); // 파괴 → 폭발
            yield return null;

            bool spawned = false;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name.StartsWith("ExplosionFX_TEST") && t.gameObject != fxPrefab) { spawned = true; break; }
            Assert.IsTrue(spawned, "폭발 FX 인스턴스가 스폰되지 않음");

            // 정리(FX 원본 + 클론)
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t != null && t.name.StartsWith("ExplosionFX_TEST")) Object.DestroyImmediate(t.gameObject);
        }

        [UnityTest]
        public IEnumerator Explosion_chains_to_nearby_barrel()
        {
            var barrelA = MakeBarrel(Vector3.zero);
            var barrelB = MakeBarrel(new Vector3(2.5f, 0f, 0f)); // A 폭발 반경 안 → 연쇄로 파괴
            // B에 콜라이더가 있어야 A의 OverlapSphere가 잡음
            barrelB.AddComponent<BoxCollider>();
            yield return null;

            ((IDamagable)barrelA.GetComponent<Breakable>()).TakeDamage(999);
            yield return null; yield return null;

            Assert.IsTrue(barrelB == null, "옆 배럴로 폭발이 연쇄되지 않음(파괴 안 됨)");
        }
    }
}
