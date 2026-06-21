using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TDS.Tests.PlayMode
{
    /// <summary>전장의 안개 마스크: 시야 안 텍셀은 밝게(1), 시야 밖/뒤는 어둡게(0).</summary>
    public class VisionMaskTests
    {
        private readonly List<GameObject> created = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var g in created)
                if (g != null) Object.DestroyImmediate(g);
            created.Clear();
            var fq = GameObject.Find("VisionFogQuad");
            if (fq != null) Object.DestroyImmediate(fq);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Mask_is_bright_in_view_and_dark_behind()
        {
            var go = new GameObject("FoV");
            go.transform.position = Vector3.zero;
            go.transform.forward = Vector3.forward;
            go.AddComponent<FieldOfView>();       // RequireComponent
            var vm = go.AddComponent<VisionMask>();
            created.Add(go);

            yield return null; // Awake가 초기 마스크 계산
            yield return null;

            Assert.IsNotNull(vm.Mask, "마스크 텍스처 없음");

            var front = vm.WorldToTexel(new Vector3(0, 0, 8));
            var back = vm.WorldToTexel(new Vector3(0, 0, -8));
            float mFront = vm.Mask.GetPixel(front.x, front.y).r;
            float mBack = vm.Mask.GetPixel(back.x, back.y).r;

            Assert.Greater(mFront, 0.5f, $"시야 안 텍셀이 밝아야(={mFront:0.00})");
            Assert.Less(mBack, 0.5f, $"시야 밖(뒤) 텍셀이 어두워야(={mBack:0.00})");
        }
    }
}
