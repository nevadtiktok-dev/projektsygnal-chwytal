using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class FirstPersonLook : MonoBehaviour
{
    public float sensitivity = 1f;

    private Transform player;
    private float pitch;

    private void Awake()
    {
        player = transform.parent;
    }

    private void Update()
    {
        Vector2 look;
        bool clicked;
        bool escapePressed;

#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse == null)
            return;

        look = mouse.delta.ReadValue() * 0.1f;
        clicked = mouse.leftButton.wasPressedThisFrame;
        escapePressed = Keyboard.current?.escapeKey.wasPressedThisFrame == true;
#else
        look = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 2f;
        clicked = Input.GetMouseButtonDown(0);
        escapePressed = Input.GetKeyDown(KeyCode.Escape);
#endif

        if (escapePressed)
        {
            Cursor.lockState = CursorLockMode.None;
            return;
        }

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            if (clicked)
                Cursor.lockState = CursorLockMode.Locked;
            return;
        }

        player.Rotate(0f, look.x * sensitivity, 0f);
        pitch = Mathf.Clamp(pitch - look.y * sensitivity, -85f, 85f);
        transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}