using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TDS.Tests.PlayMode
{
    /// <summary>breakable(누적 피해 파괴) + movable(힘에 밀림) 통합 검증.</summary>
    public class MapObjectBehaviourTests
    {
        private readonly List<GameObject> created = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var g in created)
                if (g != null) Object.DestroyImmediate(g);
            created.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Breakable_breaks_after_accumulated_damage()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            created.Add(go);
            var br = go.AddComponent<Breakable>();
            // 파편 생성 끄기(테스트 정리 단순화)
            typeof(Breakable).GetField("fragmentCount", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(br, 0);
            typeof(Breakable).GetField("maxHealth", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(br, 30);
            // Awake가 currentHealth=maxHealth 세팅하도록 한 프레임
            yield return null;

            var dmg = (IDamagable)br;
            dmg.TakeDamage(20);
            Assert.IsFalse(br.IsBroken, "부분 피해로 파괴됨");

            dmg.TakeDamage(20); // 누적 40 > 30 → 파괴
            Assert.IsTrue(br.IsBroken, "누적 피해로 파괴되지 않음");

            yield return null; // Destroy 적용
            Assert.IsTrue(go == null, "파괴 후 오브젝트가 제거되지 않음");
        }

        [UnityTest]
        public IEnumerator Movable_is_pushed_by_force()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(20f, 1f, 20f);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            created.Add(floor);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.position = new Vector3(0f, 0.5f, 0f);
            created.Add(go);
            var mv = go.AddComponent<Movable>();
            yield return null; // Awake(Rigidbody 구성)

            Vector3 before = go.transform.position;
            mv.Push(new Vector3(120f, 0f, 0f), go.transform.position);

            for (int i = 0; i < 80; i++)
                yield return new WaitForFixedUpdate();

            float moved = Vector3.Distance(before, go.transform.position);
            Assert.Greater(moved, 0.3f, $"Movable이 힘에 밀리지 않음(이동 {moved:0.00})");
        }
    }
}
