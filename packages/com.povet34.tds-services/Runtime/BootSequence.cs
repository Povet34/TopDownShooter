using System.Collections.Generic;

namespace TDS.Core
{
    /// <summary>
    /// 부팅 시 로드할 씬 순서를 결정하는 순수 로직(테스트 가능, Phase 0.1).
    /// 영속 Systems 씬을 먼저, 가산 Map 씬을 다음에 로드하도록 순서를 보장한다.
    /// </summary>
    public static class BootSequence
    {
        public static IReadOnlyList<string> Plan(string systemsScene, string mapScene)
        {
            var list = new List<string>();
            if (!string.IsNullOrEmpty(systemsScene)) list.Add(systemsScene);
            if (!string.IsNullOrEmpty(mapScene)) list.Add(mapScene);
            return list;
        }
    }
}
