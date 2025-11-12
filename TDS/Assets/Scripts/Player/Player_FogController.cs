using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VolumetricFogAndMist2;

public class Player_FogController : MonoBehaviour
{
    [SerializeField] VolumetricFog fogVolume;

    [SerializeField] float fogHoleRadius = 8f;
    [SerializeField] float clearDuration = 0.2f;
    [SerializeField] float distanceCheck = 1f;
    Vector3 lastPos = new Vector3(float.MaxValue, 0, 0);

    void Update()
    {
        if ((transform.position - lastPos).magnitude > distanceCheck)
        {
            lastPos = transform.position;
            fogVolume.SetFogOfWarAlpha(transform.position, radius: fogHoleRadius, fogNewAlpha: 0, duration: clearDuration);
        }
    }
}
