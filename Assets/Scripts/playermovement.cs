using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    private PlayerStamina stamina;
    public float speed = 8f;
    public float jumpHeight = 1.5f;
    public float gravity = -20f;

    [Header("Camera Bob")]
    [SerializeField] private Transform playerCamera;
    [SerializeField] private float bobHeight = 0.035f;
    [SerializeField] private float stepsPerSecond = 2f;

    private CharacterController controller;
    private float verticalSpeed;
    private Vector3 cameraRestPosition;
    private float stepPhase;

    private void Awake()
    {
        stamina = GetComponent<PlayerStamina>();
        controller = GetComponent<CharacterController>();

        if (playerCamera != null)
            cameraRestPosition = playerCamera.localPosition;
    }

    private void Update()
    {
        Vector2 input = Vector2.zero;
        bool jumpPressed = false;

#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            input.x = (keyboard.dKey.isPressed ? 1 : 0)
                    - (keyboard.aKey.isPressed ? 1 : 0);
            input.y = (keyboard.wKey.isPressed ? 1 : 0)
                    - (keyboard.sKey.isPressed ? 1 : 0);
            jumpPressed = keyboard.spaceKey.wasPressedThisFrame;
        }
#else
        input.x = Input.GetAxisRaw("Horizontal");
        input.y = Input.GetAxisRaw("Vertical");
        jumpPressed = Input.GetKeyDown(KeyCode.Space);
#endif

        if (controller.isGrounded)
        {
            verticalSpeed = jumpPressed
                ? Mathf.Sqrt(-2f * gravity * jumpHeight)
                : -2f;
        }

        verticalSpeed += gravity * Time.deltaTime;

        Vector3 direction = transform.right * input.x + transform.forward * input.y;
        float currentSpeed = stamina != null
    ? stamina.GetSpeed(speed, input.sqrMagnitude > 0f, controller.isGrounded)
    : speed;

        Vector3 movement = Vector3.ClampMagnitude(direction, 1f) * currentSpeed;
        movement.y = verticalSpeed;

        controller.Move(movement * Time.deltaTime);
    }

    private void LateUpdate()
    {
        if (playerCamera == null)
            return;

        Vector3 velocity = controller.velocity;
        velocity.y = 0f;

        float offset = 0f;
        if (controller.isGrounded && velocity.sqrMagnitude > 0.04f)
        {
            stepPhase += Time.deltaTime * stepsPerSecond * 2f * Mathf.PI;
            offset = Mathf.Sin(stepPhase) * bobHeight;
        }
        else
        {
            stepPhase = 0f;
        }

        Vector3 target = cameraRestPosition + Vector3.up * offset;
        float blend = 1f - Mathf.Exp(-18f * Time.deltaTime);
        playerCamera.localPosition = Vector3.Lerp(playerCamera.localPosition, target, blend);
    }
}