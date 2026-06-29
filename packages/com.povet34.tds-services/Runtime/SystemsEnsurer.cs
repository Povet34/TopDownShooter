using System;

namespace TDS.Core
{
    /// <summary>
    /// "있으면 재사용 / 없으면 생성"(멱등) 보장 로직의 순수 코어(Phase 0.1, 결정 D6).
    /// 어떤 씬으로 진입하든 Systems를 정확히 한 번 띄우기 위한 시임. 실제 존재확인/생성은 주입한다(테스트 가능).
    /// </summary>
    public static class SystemsEnsurer
    {
        /// <returns>새로 생성했으면 true, 이미 존재해 건너뛰면 false.</returns>
        public static bool Ensure(Func<bool> exists, Action spawn)
        {
            if (exists == null) throw new ArgumentNullException(nameof(exists));
            if (spawn == null) throw new ArgumentNullException(nameof(spawn));

            if (exists()) return false;
            spawn();
            return true;
        }
    }
}
