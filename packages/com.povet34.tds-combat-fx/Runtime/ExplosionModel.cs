namespace TDS.Core
{
    /// <summary>폭발 피해 계산(순수). 거리에 따른 선형 falloff.</summary>
    public static class ExplosionModel
    {
        /// <summary>중심에서 distance만큼 떨어진 대상의 피해. dist 0=maxDamage, dist≥radius=0, 그 사이 선형.</summary>
        public static float DamageAt(float distance, float radius, float maxDamage)
        {
            if (radius <= 0f || distance >= radius)
                return 0f;
            if (distance < 0f)
                distance = 0f;
            return maxDamage * (1f - distance / radius);
        }
    }
}
