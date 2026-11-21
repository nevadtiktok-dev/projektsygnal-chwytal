using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Light))]
public class FlashlightToggle : MonoBehaviour
{
    private Light flashlight;

    private void Awake()
    {
        flashlight = GetComponent<Light>();
        flashlight.enabled = false;
    }

    private void Update()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
            return;

#if ENABLE_INPUT_SYSTEM
        bool pressed = Keyboard.current?.fKey.wasPressedThisFrame == true;
#else
        bool pressed = Input.GetKeyDown(KeyCode.F);
#endif

        if (pressed)
            flashlight.enabled = !flashlight.enabled;
    }
}