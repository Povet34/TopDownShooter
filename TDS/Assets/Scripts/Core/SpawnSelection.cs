using System.Collections.Generic;
using UnityEngine;

namespace TDS.Core
{
    /// <summary>
    /// 가중치 기반 선택(순수, 테스트 가능). 스폰 테이블에서 어떤 몬스터를 뽑을지 결정.
    /// </summary>
    public static class SpawnSelection
    {
        /// <summary>weights를 누적분포로 보고 roll∈[0,1)로 인덱스 선택. 합이 0이면 0, 빈 리스트면 -1.</summary>
        public static int PickIndex(IReadOnlyList<float> weights, float roll)
        {
            if (weights == null || weights.Count == 0)
                return -1;

            float total = 0f;
            for (int i = 0; i < weights.Count; i++)
                total += Mathf.Max(0f, weights[i]);

            if (total <= 0f)
                return 0;

            float target = Mathf.Clamp(roll, 0f, 0.999999f) * total;
            float acc = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                acc += Mathf.Max(0f, weights[i]);
                if (target < acc)
                    return i;
            }
            return weights.Count - 1;
        }
    }
}
