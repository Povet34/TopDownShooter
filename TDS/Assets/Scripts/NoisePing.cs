using UnityEngine;

/// <summary>
/// 가장 최근의 소음 2채널(§6.2). 적은 자기 위치에서 들리는지 검사해 경계 상태로 들어간다
/// (소음은 발각이 아니라 조사 트리거). 우선순위는 적이 NoiseModel.Investigate로 정한다(총구음 > 피격음).
/// - Muzzle: 플레이어 발사 위치(큼) — 들으면 플레이어 쪽으로 곧장.
/// - Impact: 총알이 땅·벽에 박힌 위치(작음) — 발사음은 못 들었어도 근처에 박히면 그쪽으로 수색.
/// </summary>
public static class NoisePing
{
    public struct Ping
    {
        public Vector3 position;
        public float time;
        public float radius;
    }

    public static Ping Muzzle { get; private set; }
    public static Ping Impact { get; private set; }

    public static void EmitMuzzle(Vector3 position, float radius)
        => Muzzle = new Ping { position = position, time = UnityEngine.Time.time, radius = radius };

    public static void EmitImpact(Vector3 position, float radius)
        => Impact = new Ping { position = position, time = UnityEngine.Time.time, radius = radius };

    // 플레이/테스트 재시작 시 정적 상태 초기화(도메인 리로드 끔 대비). time=-999로 "오래됨" 처리.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Muzzle = new Ping { position = Vector3.zero, time = -999f, radius = 0f };
        Impact = new Ping { position = Vector3.zero, time = -999f, radius = 0f };
    }
}
