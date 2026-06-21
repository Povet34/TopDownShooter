namespace TDS.Core
{
    /// <summary>
    /// 소음 가청 판정(순수, §6.2). 소음은 "발각"이 아니라 그쪽으로 고개를 돌리게 하는 트리거 —
    /// 최근(maxAge 이내)에 난 소음이 가청 반경 안이면 들린다.
    /// </summary>
    public static class NoiseModel
    {
        public static bool Heard(float distanceToNoise, float noiseRadius, float ageSeconds, float maxAgeSeconds)
        {
            if (ageSeconds < 0f || ageSeconds > maxAgeSeconds)
                return false;
            return distanceToNoise <= noiseRadius;
        }
    }
}
