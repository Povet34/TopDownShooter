using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerFog : MonoBehaviour
{
    [SerializeField] VolumetricFogAndMist2.VolumetricFog fog;

    public float fogHoleRadius = 8f;
    public float clearDuration = 0.2f;
    public float distanceCheck = 1f;

    Vector3 lastPos = new Vector3(float.MaxValue, 0, 0);

    private void Update()
    {
        if ((transform.position - lastPos).magnitude > distanceCheck)
        {
            lastPos = transform.position;
            fog.SetFogOfWarAlpha(transform.position, radius: fogHoleRadius, fogNewAlpha: 0, duration: clearDuration);
        }
    }
}
