using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerStamina : MonoBehaviour
{
    [SerializeField] private AudioClip mushroomEatSound;
    [SerializeField, Min(0f)] private float regenPercentPerSecond = 5f;
    [SerializeField, Min(0f)] private float regenDelay = 2f;
    [SerializeField] private AudioSource staminaAudioSource;
    [SerializeField] private AudioClip staminaEmptySound;
    private float regenAfter;
    [SerializeField] private Camera viewCamera;
    [SerializeField] private Transform mushroomParent;
    [SerializeField, Min(1f)] private float maxStamina = 100f;
    [SerializeField, Min(1f)] private float sprintMultiplier = 1.6f;
    [SerializeField, Min(1f)] private float drainPerSecond = 22f;

    private float stamina;
    private bool exhausted;
    private bool canEat;

    private void Awake()
    {
        stamina = maxStamina;
        if (viewCamera == null)
            viewCamera = GetComponentInChildren<Camera>();
    }

    public float GetSpeed(float walkSpeed, bool moving, bool grounded)
    {
#if ENABLE_INPUT_SYSTEM
        bool shift = Keyboard.current?.leftShiftKey.isPressed == true;
#else
    bool shift = Input.GetKey(KeyCode.LeftShift);
#endif

        if (shift && moving && grounded && !exhausted && stamina > 0f)
        {
            stamina = Mathf.Max(0f, stamina - drainPerSecond * Time.deltaTime);
            regenAfter = Time.time + regenDelay;

            if (stamina <= 0f)
            {
                exhausted = true;

                if (staminaAudioSource != null && staminaEmptySound != null)
                    staminaAudioSource.PlayOneShot(staminaEmptySound);
            }

            return walkSpeed * sprintMultiplier;
        }

        if (Time.time >= regenAfter)
        {
            float regenAmount = maxStamina * regenPercentPerSecond / 100f;
            stamina = Mathf.MoveTowards(stamina, maxStamina, regenAmount * Time.deltaTime);
        }

        if (!shift && stamina >= maxStamina * 0.2f)
            exhausted = false;

        return walkSpeed;
    }

    private void Update()
    {
        canEat = false;
        if (viewCamera == null || mushroomParent == null ||
            Cursor.lockState != CursorLockMode.Locked)
            return;

        Ray ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Transform nearestHit = null;
        float nearestDistance = 3f;

        foreach (RaycastHit hit in Physics.RaycastAll(ray, 3f,
                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform) || hit.distance >= nearestDistance)
                continue;

            nearestDistance = hit.distance;
            nearestHit = hit.transform;
        }

        if (nearestHit == null)
            return;

        foreach (Transform mushroom in mushroomParent)
        {
            if (!nearestHit.IsChildOf(mushroom))
                continue;

            canEat = stamina < maxStamina;
#if ENABLE_INPUT_SYSTEM
            bool eat = Keyboard.current?.eKey.wasPressedThisFrame == true;
#else
            bool eat = Input.GetKeyDown(KeyCode.E);
#endif
            if (canEat && eat)
            {
                stamina = maxStamina;
                if (staminaAudioSource != null && mushroomEatSound != null)
                    staminaAudioSource.PlayOneShot(mushroomEatSound);
                exhausted = false;
                canEat = false;

                GameObject eatenMushroom = mushroom.gameObject;
                eatenMushroom.SetActive(false);
                Destroy(eatenMushroom);
                return;
            }

            return;
        }
    }

    private void OnGUI()
    {
        if (ForestIntro.HideGameplayHud)
            return;
        GUI.Box(new Rect(12, 82, 200, 30),
            $"Stamina: {Mathf.CeilToInt(100f * stamina / maxStamina)}%");

        if (canEat)
            GUI.Box(new Rect((Screen.width - 220f) * 0.5f,
                Screen.height - 50f, 220f, 30f), "[E] Zjedz grzybka");
    }
}