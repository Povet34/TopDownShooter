using UnityEngine;

namespace TDS.Core
{
    /// <summary>오브젝트 풀 서비스. <c>ObjectPool</c>이 구현·등록한다.</summary>
    public interface IObjectPoolService
    {
        GameObject GetObject(GameObject prefab, Transform target);
        void ReturnObject(GameObject objectToReturn, float delay);
    }
}
