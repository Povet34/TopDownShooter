using System.Collections.Generic;
using UnityEngine;
using TDS.Core;

/// <summary>
/// 플레이어가 낸 소리(§6.2.1)의 전역 홀더. 종류별로 가장 최근 1건을 보관한다. 적은 자기 위치에서
/// 들리는지(거리 ≤ 소음 테이블 loudness, 최근) 검사해 조사한다. **플레이어 소리만 여기로 발신** —
/// 적끼리는 소리에 반응하지 않는다(적 무기/총알은 발신 안 함).
/// </summary>
public static class NoisePing
{
    public struct Ping
    {
        public NoiseType type;
        public Vector3 noisePos;   // 소음이 난 위치
        public Vector3 sourcePos;  // 발생자(플레이어) 위치 — 폭발/발포음은 이걸로 플레이어를 알림
        public float time;
        public bool set;
    }

    // 종류별(enum 인덱스) 최근 1건.
    private static readonly Ping[] channels = new Ping[16];

    public static void Emit(NoiseType type, Vector3 noisePos, Vector3 sourcePos)
    {
        int i = (int)type;
        if (i < 0 || i >= channels.Length)
            return;
        channels[i] = new Ping { type = type, noisePos = noisePos, sourcePos = sourcePos, time = Time.time, set = true };
    }

    // 편의 발신기 — 플레이어 코드에서만 호출.
    public static void EmitGunshot(Vector3 playerPos) => Emit(NoiseType.Gunshot, playerPos, playerPos);
    public static void EmitImpact(Vector3 impactPos) => Emit(NoiseType.BulletImpact, impactPos, impactPos);
    public static void EmitExplosion(Vector3 explosionPos, Vector3 playerPos) => Emit(NoiseType.Explosion, explosionPos, playerPos);
    public static void EmitFootstep(Vector3 playerPos) => Emit(NoiseType.Footstep, playerPos, playerPos);
    public static void EmitReload(Vector3 playerPos) => Emit(NoiseType.Reload, playerPos, playerPos);

    /// <summary>적이 거리/나이를 채워 NoiseModel.Resolve에 넘길 수 있도록 발신된 채널들을 읽는다.</summary>
    public static IReadOnlyList<Ping> ActiveChannels
    {
        get
        {
            var list = new List<Ping>();
            for (int i = 0; i < channels.Length; i++)
                if (channels[i].set)
                    list.Add(channels[i]);
            return list;
        }
    }

    // 플레이/테스트 재시작 시 정적 상태 초기화(도메인 리로드 끔 대비).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        for (int i = 0; i < channels.Length; i++)
            channels[i] = default;
    }

    /// <summary>테스트용 명시 초기화(static 상태가 테스트 간 남는 것 방지).</summary>
    public static void ClearForTests() => Reset();
}
