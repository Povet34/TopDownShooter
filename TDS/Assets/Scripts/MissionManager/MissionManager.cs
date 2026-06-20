using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TDS.Core;

public class MissionManager : MonoBehaviour, IMissionService
{
    public static MissionManager instance;


    public Mission currentMission;

    private void Awake()
    {
        instance = this;
        GameServices.Registry.Register<IMissionService>(this);
    }


    private void Update()
    {
        currentMission?.UpdateMission();
    }

    public void SetCurrentMission(Mission newMission)
    {
        currentMission = newMission;
    }

    public void StartMission() => currentMission.StartMission();

    public bool MissionCompleted() => currentMission.MissionCompleted();


}
