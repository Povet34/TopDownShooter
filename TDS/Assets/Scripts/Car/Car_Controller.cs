using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public enum DriveType { FrontWheelDrive, RearWheelDrive, AllWheelDrive}

[RequireComponent(typeof(NavMeshObstacle))]
[RequireComponent(typeof(Car_HealthController))]
[RequireComponent(typeof(Car_Interaction))]
[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class Car_Controller : MonoBehaviour
{
    public Car_Sounds carSounds { get; private set; }
    public Rigidbody rb { get; private set; }
    public bool carActive { get; private set; }
    private PlayerControls controls;
    private float moveInput;
    private float steerInput;

    [SerializeField] LayerMask whatIsGround;

    public float speed;

    [Range(30,60)]
    [SerializeField] private float turnSensetivity = 30;
    [Header("Car Settings")]
    [SerializeField] private DriveType driveType;
    [SerializeField] private Transform centerOfMass;
    [Range(350,1000)]
    [SerializeField] private float carMass = 400;
    [Range(20,80)]
    [SerializeField] private float wheelsMass = 30;
    [Range(.5f, 2f)]
    [SerializeField] private float frontWheelTraction = 1;
    [Range(.5f, 2f)]
    [SerializeField] private float backWheelTraction = 1;

    [Header("Engine Settings")]
    [SerializeField] private float currentSpeed;
    [Range(7,30)]
    [SerializeField] private float maxSpeed = 14;
    [Range(.5f,10)]
    [SerializeField] private float accleerationSpeed = 2;
    [Range(1500,5000)]
    [SerializeField] private float motorForce = 1500f;

    [Header("Brakes Settings")]
    [Range(0,10)]
    [SerializeField] private float frontBrakesSensetivity = 5;
    [Range(0,10)]
    [SerializeField] private float backBrakesSensetivity = 5;
    [Range(4000,6000)]
    [SerializeField] private float brakePower = 5000;
    private bool isBraking;

    [Header("Drift Settings")]
    [Range(0, 1)]
    [SerializeField] private float frontDriftFactor = .5f;
    [Range(0, 1)]
    [SerializeField] private float backDriftFactor = .5f;
    [SerializeField] private float driftDuration = 1f;
    private float driftTimer;
    private bool isDrifting;
    //private bool canEmitTrails = true;


    private Car_Wheel[] wheels;
    private UI ui;
    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        wheels = GetComponentsInChildren<Car_Wheel>();
        carSounds = GetComponent<Car_Sounds>();
        ui = UI.instance;

        controls = ControlsManager.instance.controls;
        //ControlsManager.instance.SwitchToCarControls();

        AssignInputEvents();
        SetupDefaultValues();
        ActivateCar(false);
    }

    private void SetupDefaultValues()
    {
        rb.centerOfMass = centerOfMass.localPosition;
        rb.mass = carMass;

        int wheelCount = Mathf.Max(1, wheels.Length);
        foreach (var wheel in wheels)
        {
            wheel.cd.mass = wheelsMass;

            if (wheel.axelType == AxelType.Front)
                wheel.SetDefaultStiffnes(frontWheelTraction);

            if (wheel.axelType == AxelType.Back)
                wheel.SetDefaultStiffnes(backWheelTraction);

            // 서스펜션을 질량/바퀴수에 비례해 충분히 단단하게. 약하면 무거운 차가 가라앉아 차체가 바닥에
            // 끌리고(바퀴 파묻힘) AddForce로도 안 움직였다 — 차체를 바퀴 위로 지지해 주행 가능하게.
            JointSpring spring = wheel.cd.suspensionSpring;
            spring.spring = carMass * 150f / wheelCount;
            spring.damper = spring.spring * 0.25f;
            spring.targetPosition = 0.5f;
            wheel.cd.suspensionSpring = spring;
            wheel.cd.forceAppPointDistance = 0.1f; // 힘 적용점을 살짝 올려 전복/끌림 줄임
        }

    }

    private void Update()
    {
        if (carActive == false)
            return;


        speed = rb.linearVelocity.magnitude;
        if (ui != null) // 맵 씬엔 UI 싱글톤 없음 — MapHUD가 Speed를 읽어 표시
            ui.inGameUI.UpdateSpeedText(Mathf.RoundToInt(speed * 5) + "km/h");

        driftTimer -= Time.deltaTime;

        if (driftTimer < 0)
            isDrifting = false;
    }

    private void FixedUpdate()
    {
        if(carActive == false)
            return;

        ApplyTrailOnThGround();
        ApplyAnimationToWheels();
        ApplyDrive();
        ApplySteering();
        ApplyBrakes();
        ApplySpeedLimit();

        if (isDrifting)
            ApplyDrift();
        else
            StopDrift();
    }

    private void ApplyTrailOnThGround()
    {
        //if (!canEmitTrails)
        //    return;

        foreach(var wheel in wheels)
        {
            if (wheel.trail == null) continue; // 트레일 없는 휠 프리팹 안전

            WheelHit hit;

            if(wheel.cd.GetGroundHit(out hit))
            {
                bool isGrounded = whatIsGround == (whatIsGround | (1 << hit.collider.gameObject.layer));
                wheel.trail.emitting = isGrounded;
            }
        }
    }

    private void ApplyDrive()
    {
        // 아케이드 추진 — 전진 속도를 '직접' 설정한다. AddForce/휠토크는 차가 살짝 가라앉아 차체가 바닥에
        // 끌리면 마찰에 묶여 안 움직였다(사용자 "WASD 눌러도 안 감"). 속도를 직접 주면 끌림·질량·트랙션과
        // 무관하게 항상 전진/후진하고 속도를 정확히 제어한다. 벽 충돌은 물리가 막아준다(ContinuousDynamic).
        foreach (var wheel in wheels)
            wheel.cd.motorTorque = 0f;

        float fwdSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        currentSpeed = fwdSpeed; // 인스펙터 표시용(전진 방향 속력, 후진이면 음수)

        float target = moveInput * (moveInput > 0f ? maxSpeed : maxSpeed * 0.5f); // 후진 절반 속도
        float accel = (Mathf.Abs(moveInput) > 0.01f ? accleerationSpeed * 3f : accleerationSpeed * 1.5f); // 가속 vs 코스트다운
        float newFwd = Mathf.MoveTowards(fwdSpeed, target, accel * Time.fixedDeltaTime);

        // 전진분만 직접 설정. 측면속도는 그립으로 약감쇠(미끄러짐↓), 수직(중력/서스펜션)은 보존.
        Vector3 v = rb.linearVelocity;
        Vector3 lateral = v - transform.forward * fwdSpeed;
        lateral.y = 0f;
        Vector3 horiz = transform.forward * newFwd + lateral * 0.9f;
        rb.linearVelocity = new Vector3(horiz.x, v.y, horiz.z);
    }

    private void ApplySpeedLimit()
    {
        if (rb.linearVelocity.magnitude > maxSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
    }

    private void ApplySteering()
    {
        // 앞바퀴 시각 조향각
        foreach (var wheel in wheels)
        {
            if (wheel.axelType == AxelType.Front)
            {
                float targetSteerAngle = steerInput * turnSensetivity;
                wheel.cd.steerAngle = Mathf.Lerp(wheel.cd.steerAngle, targetSteerAngle, .5f);
            }
        }

        // 아케이드 조향: 차체를 직접 yaw(휠 마찰만으론 약하거나 안 돌 수 있어 확실하게). 전진 속도에
        // 비례, 정지 시엔 안 돎, 후진 시 반대 방향. 속도 벡터도 헤딩을 따라가게 해 과도한 미끄러짐 방지.
        float fwdSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        if (Mathf.Abs(fwdSpeed) > 0.4f)
        {
            float speedFactor = Mathf.Clamp01(Mathf.Abs(fwdSpeed) / 6f); // 저속에서 회전 둔하게
            float yaw = steerInput * turnSensetivity * speedFactor * Mathf.Sign(fwdSpeed) * Time.fixedDeltaTime;
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            rb.MoveRotation(rb.rotation * rot);

            // 수평 속도를 새 헤딩으로 정렬(그립감 — 옆으로 안 미끄러지게). 수직 속도 보존.
            Vector3 v = rb.linearVelocity;
            Vector3 flat = rot * new Vector3(v.x, 0f, v.z);
            rb.linearVelocity = new Vector3(flat.x, v.y, flat.z);
        }
    }

    private void ApplyBrakes()
    {

        foreach (var wheel in wheels)
        {
            bool frontBrakes = wheel.axelType == AxelType.Front;
            float brakeSensetivity = frontBrakes ? frontBrakesSensetivity : backBrakesSensetivity;

            float newBrakeTorque = brakePower * brakeSensetivity * Time.deltaTime;
            float currentBrakeTorque = isBraking ? newBrakeTorque : 0;

            wheel.cd.brakeTorque = currentBrakeTorque;
        }
    }

    private void ApplyDrift()
    {
        foreach (var wheel in wheels)
        {
            bool frontWheel = wheel.axelType == AxelType.Front;
            float driftFactor = frontWheel ? frontDriftFactor : backDriftFactor;

            WheelFrictionCurve sidewaysFriction = wheel.cd.sidewaysFriction;

            sidewaysFriction.stiffness *= (1 - driftFactor);
            wheel.cd.sidewaysFriction = sidewaysFriction;
        }
    }

    private void StopDrift()
    {
        foreach (var wheel in wheels)
        {
            wheel.RestoreDefaultStiffnes();
        }
    }


    private void ApplyAnimationToWheels()
    {
        foreach (var wheel in wheels)
        {
            Quaternion rotation;
            Vector3 position;

            wheel.cd.GetWorldPose(out position, out rotation);

            if (wheel.model != null)
            {
                wheel.model.transform.position = position;
                wheel.model.transform.rotation = rotation;
            }
        }
    }

    public void ActivateCar(bool activate)
    {
        carActive = activate;

        if (carSounds != null)
            carSounds.ActivateCarSFX(activate);

        // 주차 중(비활성)엔 완전 고정. 주행 중엔 X/Z 회전 고정 → 피격/충돌에 뒤집히거나 들썩이지 않고
        // 똑바로 유지(위치 이동 + Y축 조향은 자유).
        if (rb != null)
            rb.constraints = activate
                ? (RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ)
                : RigidbodyConstraints.FreezeAll;
    }

    public void BrakeTheCar()
    {
        //canEmitTrails = false;

        foreach(var wheel in wheels)
        {
            if (wheel.trail != null) wheel.trail.emitting = false;
        }

        rb.linearDamping = 1;
        motorForce = 0;
        isDrifting = true;
        frontDriftFactor = .9f;
        backDriftFactor = .9f;
    }

    private void AssignInputEvents()
    {
        controls.Car.Movement.performed += ctx =>
        {
            Vector2 input = ctx.ReadValue<Vector2>();

            moveInput = input.y;
            steerInput = input.x;
        };

        controls.Car.Movement.canceled += ctx =>
        {
            moveInput = 0;
            steerInput = 0;
        };

        controls.Car.Brake.performed += ctx =>
        {
            isBraking = true;
            isDrifting = true;
            driftTimer = driftDuration;
        }; 
        controls.Car.Brake.canceled += ctx => isBraking = false;

        

        controls.Car.CarExit.performed += ctx => GetComponent<Car_Interaction>().GetOutOfTheCar();
    }

    [ContextMenu("Focus camera and enable")]
    public void TestThisCar()
    {
        ActivateCar(true);
        if (CameraManager.instance != null)
            CameraManager.instance.ChangeCameraTarget(transform, 12);
    }

    /// <summary>현재 속도(km/h 표기용 원시값). MapHUD 등이 읽음.</summary>
    public float Speed => speed;
}
