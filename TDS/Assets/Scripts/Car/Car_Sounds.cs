using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Car_Sounds : MonoBehaviour
{
    private Car_Controller car;

    [SerializeField] private float engineVolume = .07f;
    [SerializeField] private AudioSource engineStart;
    [SerializeField] private AudioSource workingEngine;
    [SerializeField] private AudioSource engineOff;

    private float maxSpeed = 10;

    public float minPitch = .75f;
    public float maxPitch = 1.5f;

    private bool allowCarSounds;

    private void Start()
    {
        car = GetComponent<Car_Controller>();
        Invoke(nameof(AllowCarSounds), 1);
    }

    private void Update()
    {
        UpdateEngineSound();
    }

    private void UpdateEngineSound()
    {
        if (car == null || workingEngine == null) // 프리팹에 오디오 소스 미할당 시(맵/사운드 보류) 안전
            return;

        float currentSpeed = car.speed;
        float pitch = Mathf.Lerp(minPitch, maxPitch, currentSpeed / maxSpeed);
        workingEngine.pitch = pitch;
    }

    public void ActivateCarSFX(bool activate)
    {
        if (allowCarSounds == false)
            return;

        // 맵 씬엔 AudioManager 싱글톤 없음(사운드 보류) + 프리팹에 오디오 소스 미할당일 수 있음 → 가드.
        if (activate)
        {
            if (engineStart != null) engineStart.Play();
            if (AudioManager.instance != null && workingEngine != null)
                AudioManager.instance.SFXDelayAndFade(workingEngine, true, engineVolume, 1);
        }
        else
        {
            if (AudioManager.instance != null && workingEngine != null)
                AudioManager.instance.SFXDelayAndFade(workingEngine, false, 0f, .25f);
            if (engineOff != null) engineOff.Play();
        }
    }

    private void AllowCarSounds() => allowCarSounds = true;
}
