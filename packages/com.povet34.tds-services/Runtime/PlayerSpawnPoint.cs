using UnityEngine;

namespace TDS.Core
{
    /// <summary>
    /// 플레이어 스폰 위치 계산(순수, 테스트 가능). 맵 중앙(스폰존)을 지면 높이로 투영한다.
    /// 부트/맵 스포너가 절차 맵의 중앙 빈 공간에 플레이어를 배치할 때 사용.
    /// </summary>
    public static class PlayerSpawnPoint
    {
        public static Vector3 Resolve(Vector3 mapCenter, float groundY = 0f)
            => new Vector3(mapCenter.x, groundY, mapCenter.z);
    }
}
