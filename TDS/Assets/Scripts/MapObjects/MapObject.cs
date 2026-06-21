using UnityEngine;

/// <summary>맵 오브젝트의 용도(분류) 태그. 맵 생성 시 부여 — 디버그/감사/쿼리용.</summary>
public class MapObject : MonoBehaviour
{
    public TDS.Core.MapObjectRole role;

    public bool Is(TDS.Core.MapObjectRole flag) => (role & flag) != 0;
}
