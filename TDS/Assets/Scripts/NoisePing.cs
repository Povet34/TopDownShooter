using UnityEngine;

/// <summary>
/// 가장 최근의 소음(총성 등) 한 건을 담는 전역 홀더. 플레이어 발사 시 Emit하고, 적은 자기 위치에서
/// 들리는지 검사해 경계 상태로 들어간다(§6.2 — 소음은 발각이 아니라 조사 트리거).
/// </summary>
public static class NoisePing
{
    public static Vector3 Position { get; private set; }
    public static float Time { get; private set; } = -999f;
    public static float Radius { get; private set; }

    public static void Emit(Vector3 position, float radius)
    {
        Position = position;
        Time = UnityEngine.Time.time;
        Radius = radius;
    }

    // 플레이/테스트 재시작 시 정적 상태 초기화(도메인 리로드 끔 대비).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Time = -999f;
        Radius = 0f;
        Position = Vector3.zero;
    }
}
