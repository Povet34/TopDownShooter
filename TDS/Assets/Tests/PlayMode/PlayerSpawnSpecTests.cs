using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TDS.Core;

namespace TDS.Tests.PlayMode
{
    /// <summary>
    /// Phase 0.2 명세: Player 프리팹이 맵 단독 컨텍스트(시스템만 있고 UI/원래 씬 없음)에서
    /// NullRef 없이 스폰돼야 한다. 실패가 나면 그 NullRef 하나만 가드/배선으로 잡는다(조각별 격리).
    /// </summary>
    public class PlayerSpawnSpecTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameBootstrap.Instance != null)
                Object.DestroyImmediate(GameBootstrap.Instance.gameObject);
            GameServices.ResetForTests();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Player_prefab_spawns_without_nullrefs()
        {
            GameServices.ResetForTests();
            GameBootstrap.EnsureSystems(); // 플레이어보다 먼저 시스템(ControlsManager 등) 보장
            yield return null;

            var prefab = Resources.Load<GameObject>("Player");
            Assert.IsNotNull(prefab, "Resources/Player 로드 실패");

            var pos = PlayerSpawnPoint.Resolve(Vector3.zero);
            var player = Object.Instantiate(prefab, pos, Quaternion.identity);

            // 몇 프레임 — Start/Update가 돌며 결합된 NullRef가 표면화된다(있으면 테스트 실패).
            yield return null;
            yield return null;
            yield return null;

            Assert.IsNotNull(player);

            Object.DestroyImmediate(player);
        }
    }
}
