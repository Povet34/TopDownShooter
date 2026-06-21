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
    private float speed;
    private float verticalVelocity;

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

            characterController.Move(movementDirection * Time.deltaTime * speed);
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