using Unity.Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
    public static CameraManager instance;


    private CinemachineCamera virtualCamera;
    private CinemachinePositionComposer composer;


    [Header("Camera distance")]
    [SerializeField] private bool canChangeCameraDistance;
    [SerializeField] private float distanceChangeRate;
    [SerializeField] private float targetCameraDistance;

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
        {
            Debug.LogWarning("You had more than one Camera Manager");
            Destroy(gameObject);
        }


        virtualCamera = GetComponentInChildren<CinemachineCamera>();
        composer = virtualCamera.GetComponent<CinemachinePositionComposer>();

    }

    private void Update()
    {
        UpdateCameraDistance();
    }

    private void UpdateCameraDistance()
    {
        if (canChangeCameraDistance == false)
            return;

        float currentDistnace = composer.CameraDistance;

        if (Mathf.Abs(targetCameraDistance - currentDistnace) < .01f)
            return;

        composer.CameraDistance =
            Mathf.Lerp(currentDistnace, targetCameraDistance, distanceChangeRate * Time.deltaTime);
    }

    public void ChangeCameraDistance(float distance, float newChangeRate = 0.25f)
    {
        targetCameraDistance = distance;
        distanceChangeRate = newChangeRate;
    }

    public void ChangeCameraTarget(Transform target,float cameraDistance = 10,float newLookAheadTime = 0)
    {
        virtualCamera.Follow = target;

        LookaheadSettings lookahead = composer.Lookahead;
        lookahead.Time = newLookAheadTime;
        lookahead.Enabled = newLookAheadTime > 0f;
        composer.Lookahead = lookahead;

        ChangeCameraDistance(cameraDistance);
    }

}
