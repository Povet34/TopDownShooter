using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum AxelType { Front, Back}

[RequireComponent(typeof(WheelCollider))]
public class Car_Wheel : MonoBehaviour
{
    public AxelType axelType;
    public WheelCollider cd { get; private set; }
    public TrailRenderer trail { get; private set; }
    public GameObject model;

    private float defaultSideStiffnes;

    private void Awake()
    {
        cd = GetComponent<WheelCollider>();
        trail = GetComponentInChildren<TrailRenderer>();

        if (trail != null) // 스키드 트레일이 없는 휠 프리팹도 안전
            trail.emitting = false;

        if (model == null)
        {
            var mr = GetComponentInChildren<MeshRenderer>();
            if (mr != null) model = mr.gameObject;
        }
    }

    public void SetDefaultStiffnes(float newValue)
    {
        defaultSideStiffnes = newValue;
        RestoreDefaultStiffnes();
    }

    public void RestoreDefaultStiffnes()
    {
        WheelFrictionCurve sidewayFriction = cd.sidewaysFriction;

        sidewayFriction.stiffness = defaultSideStiffnes;
        cd.sidewaysFriction = sidewayFriction;
    }
}
