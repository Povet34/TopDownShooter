using UnityEngine;
using UnityEngine.UIElements;

public class Player_Movement : MonoBehaviour
{
    private Player player;

    private CharacterController characterController;
    private PlayerControls controls;
    private Animator animator;

    [Header("Movement info")]
    [SerializeField] private float walkSpeed;
    [SerializeField] private float runSpeed;
    [SerializeField] private float turnSpeed;
    [Tooltip("사격 중 이동속도 배수(0~1). 정조준하려면 멈춰야 함 — §MovingSpread")]
    [SerializeField] private float shootingMoveFactor = 0.5f;
    private float speed;
    private float verticalVelocity;

    /// <summary>현재 평면 이동 속도(탄퍼짐 계산용). 사격 감속이 반영된 실제 속도.</summary>
    public float CurrentPlanarSpeed
    {
        get
        {
            if (characterController == null) return 0f;
            Vector3 v = characterController.velocity; v.y = 0f;
            return v.magnitude;
        }
    }
    /// <summary>최대 이동 속도(달리기) — 탄퍼짐 정규화 기준.</summary>
    public float MaxSpeed => runSpeed;

    public Vector2 moveInput { get; private set; }
    private Vector3 movementDirection;

    private bool isRunning;

    private AudioSource walkSFX;
    private AudioSource runSFX;
    private bool canPlayFootsteps;

    private void Start()
    {
        player = GetComponent<Player>();

        walkSFX = player.sound.walkSFX;
        runSFX = player.sound.runSFX;
        Invoke(nameof(AllowfootstepsSFX), 1f);

        characterController = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();

        speed = walkSpeed;


        AssignInputEvents();
    }


    private void Update()
    {
        if (player.health.isDead)
            return;

        if (player.controlsEnabled == false) // 컨트롤 비활성(UI/사망연출/탑승 등) → 이동·Move 호출 안 함
            return;

        ApplyMovement();
        ApplyRotation();
        AnimatorControllers();
    }

    private void AnimatorControllers()
    {
        float xVelocity = Vector3.Dot(movementDirection.normalized, transform.right);
        float zVelocity = Vector3.Dot(movementDirection.normalized, transform.forward);

        animator.SetFloat("xVelocity", xVelocity, .1f, Time.deltaTime);
        animator.SetFloat("zVelocity", zVelocity, .1f, Time.deltaTime);

        bool playRunAnimation = isRunning & movementDirection.magnitude > 0;
        animator.SetBool("isRunning", playRunAnimation);
    }
    private void ApplyRotation()
    {
        Vector3 aimPoint = player.aim.GetMouseHitInfo().point;

        // 테스트된 순수 시임 사용: 조준점이 거의 자기 위치면 현재 회전 유지(0벡터 LookRotation 경고 회피).
        Quaternion desiredRotation =
            TDS.Core.AimRotation.FaceHorizontal(transform.position, aimPoint, transform.rotation);

        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, turnSpeed * Time.deltaTime);
    }
    private void ApplyMovement()
    {
        movementDirection = new Vector3(moveInput.x, 0, moveInput.y);
        ApplyGravity();

        if (movementDirection.magnitude > 0)
        {
            PlayFootstepsSFX();

            // 사격 중이면 감속(정조준하려면 멈춰야 함) — §MovingSpread.
            bool shooting = player.weapon != null && player.weapon.IsShooting();
            float factor = TDS.Core.MovingSpread.MoveSpeedFactor(shooting, shootingMoveFactor);

            // 평면 탑다운: 적/낮은 prop을 타고 위로 솟구치는 것 차단 — 이동 후 위로 오른 수직분은 되돌린다
            // (중력 하강·낙하는 허용). 점프/수직 게임플레이가 없으므로 안전.
            float yBefore = transform.position.y;
            characterController.Move(movementDirection * Time.deltaTime * speed * factor);
            if (transform.position.y > yBefore)
            {
                Vector3 p = transform.position; p.y = yBefore;
                transform.position = p;
            }
        }
    }

    private void PlayFootstepsSFX()
    {
        if (canPlayFootsteps == false)
            return;

        if (isRunning)
        {
            if (runSFX.isPlaying == false)
                runSFX.Play();
        }
        else
        {
            if (walkSFX.isPlaying == false)
                walkSFX.Play();
        }
    }
    private void StopFootstepsSFX()
    {
        walkSFX.Stop();
        runSFX.Stop();
    }
    private void AllowfootstepsSFX() => canPlayFootsteps = true;

    private void ApplyGravity()
    {
        if (characterController.isGrounded == false)
        {
            verticalVelocity -= 9.81f * Time.deltaTime;
            movementDirection.y = verticalVelocity;
        }
        else
            verticalVelocity = -.5f;
    }
    private void AssignInputEvents()
    {
        controls = player.controls;

        controls.Character.Movement.performed += context => moveInput = context.ReadValue<Vector2>();
        controls.Character.Movement.canceled += context =>
        {
            StopFootstepsSFX();
            moveInput = Vector2.zero;
        };

        controls.Character.Run.performed += context =>
        {
            speed = runSpeed;
            isRunning = true;
        };


        controls.Character.Run.canceled += context =>
        {
            speed = walkSpeed;
            isRunning = false;
        };
    }
}