using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ForestCombat : MonoBehaviour
{
    [Header("Efekty strzału")]
    [SerializeField] private Transform recoilPivot;
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private ParticleSystem bloodHitPrefab;
    [SerializeField, Min(0f)] private float recoilDistance = 0.045f;
    [SerializeField, Min(0f)] private float recoilAngle = 6f;
    [SerializeField, Min(0.1f)] private float recoilReturnSpeed = 16f;

    [SerializeField] private Camera viewCamera;
    [SerializeField, Min(1)] private int maxHealth = 100;
    [SerializeField, Min(1)] private int magazineSize = 12;
    [SerializeField, Min(0)] private int reserveAmmo = 96;
    [SerializeField, Min(1)] private int damage = 25;
    [SerializeField, Min(1f)] private float range = 120f;
    [SerializeField, Min(0.05f)] private float shotInterval = 0.22f;
    [SerializeField, Min(0.1f)] private float reloadDuration = 1.2f;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip shotSound;
    [SerializeField] private AudioClip hurtSound;
    [SerializeField, Min(0.01f)] private float damageFlashDuration = 0.45f;
    [SerializeField] private Texture2D damagePortrait;

    private Vector3 recoilRestPosition;
    private Quaternion recoilRestRotation;
    private float recoil;
    private float damageFlashUntil;
    private int health;
    private int loadedAmmo;
    private float nextShot;
    private float reloadEnds;
    private bool reloading;

    public bool IsAlive => health > 0;

    private void Awake()
    {
        health = maxHealth;
        loadedAmmo = magazineSize;

        if (recoilPivot != null)
        {
            recoilRestPosition = recoilPivot.localPosition;
            recoilRestRotation = recoilPivot.localRotation;
        }

        if (viewCamera == null)
            viewCamera = GetComponentInChildren<Camera>();

        if (viewCamera == null)
            Debug.LogError("Forest Combat: przypisz kamerę gracza.", this);
    }

    private void Update()
    {
        if (!IsAlive || viewCamera == null)
            return;

        if (reloading && Time.time >= reloadEnds)
        {
            int rounds = Mathf.Min(magazineSize - loadedAmmo, reserveAmmo);
            loadedAmmo += rounds;
            reserveAmmo -= rounds;
            reloading = false;
        }

        if (Cursor.lockState != CursorLockMode.Locked)
            return;

#if ENABLE_INPUT_SYSTEM
        bool fire = Mouse.current?.leftButton.wasPressedThisFrame == true;
        bool reload = Keyboard.current?.rKey.wasPressedThisFrame == true;
#else
        bool fire = Input.GetMouseButtonDown(0);
        bool reload = Input.GetKeyDown(KeyCode.R);
#endif

        if (reloading)
            return;

        if ((reload || (fire && loadedAmmo == 0)) &&
            loadedAmmo < magazineSize && reserveAmmo > 0)
        {
            reloading = true;
            reloadEnds = Time.time + reloadDuration;
            return;
        }

        if (!fire || loadedAmmo == 0 || Time.time < nextShot)
            return;

        loadedAmmo--;
        nextShot = Time.time + shotInterval;
        PlayShotEffects();

        Ray ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit closestHit = default;
        float closestDistance = float.PositiveInfinity;

        foreach (RaycastHit hit in Physics.RaycastAll(
                     ray, range, Physics.DefaultRaycastLayers,
                     QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform) || hit.distance >= closestDistance)
                continue;

            closestHit = hit;
            closestDistance = hit.distance;
        }

        if (closestHit.collider != null)
        {
            ForestMonster monster = closestHit.collider.GetComponentInParent<ForestMonster>();

            if (monster != null)
            {
                if (bloodHitPrefab != null)
                {
                    Vector3 normal = closestHit.normal;
                    ParticleSystem burst = Instantiate(
                        bloodHitPrefab,
                        closestHit.point + normal * 0.02f,
                        Quaternion.LookRotation(normal));

                    burst.Play(true);
                    Destroy(burst.gameObject, 2f);
                }

                monster.TakeHit(damage);
            }
        }
    }

    private void PlayShotEffects()
    {
        recoil = 1f;

        if (muzzleFlash != null)
        {
            muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            muzzleFlash.Play(true);
        }

        if (audioSource != null && shotSound != null)
            audioSource.PlayOneShot(shotSound);
    }

    private void LateUpdate()
    {
        if (recoilPivot == null)
            return;

        recoilPivot.localPosition =
            recoilRestPosition + Vector3.back * (recoilDistance * recoil);

        recoilPivot.localRotation =
            Quaternion.Euler(-recoilAngle * recoil, 0f, 0f) * recoilRestRotation;

        recoil = Mathf.MoveTowards(recoil, 0f, recoilReturnSpeed * Time.deltaTime);
    }

    public void TakeDamage(int amount)
    {
        if (!IsAlive)
            return;

        health = Mathf.Max(0, health - amount);
        if (amount > 0 && audioSource != null && hurtSound != null)
            audioSource.PlayOneShot(hurtSound);
        damageFlashUntil = Time.unscaledTime + damageFlashDuration;

        if (IsAlive)
            return;

        if (TryGetComponent<PlayerMovement>(out var movement))
            movement.enabled = false;

        if (viewCamera != null && viewCamera.TryGetComponent<FirstPersonLook>(out var look))
            look.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnGUI()
    {
        if (ForestIntro.HideGameplayHud)
            return;
        float remaining = damageFlashUntil - Time.unscaledTime;

        if (remaining > 0f)
        {
            Color previousColor = GUI.color;
            float fade = Mathf.Clamp01(remaining / damageFlashDuration);

            GUI.color = new Color(1f, 0f, 0f, 0.45f * fade);
            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height),
                Texture2D.whiteTexture);

            if (damagePortrait != null)
            {
                float size = Mathf.Min(Screen.height * 0.55f, Screen.width * 0.8f);
                Rect portraitRect = new Rect(
                    (Screen.width - size) * 0.5f,
                    (Screen.height - size) * 0.5f,
                    size,
                    size);

                GUI.color = new Color(1f, 0.15f, 0.15f, fade);
                GUI.DrawTexture(portraitRect, damagePortrait, ScaleMode.ScaleToFit, true);
            }

            GUI.color = previousColor;
        }

        GUI.Box(new Rect(12, 12, 200, 62), GUIContent.none);
        GUI.Label(new Rect(22, 19, 180, 22), $"HP: {health} / {maxHealth}");
        string ammo = reloading
            ? "Przeładowanie..."
            : $"Ammo: {loadedAmmo} / {reserveAmmo}";
        GUI.Label(new Rect(22, 42, 180, 22), ammo);

        float x = Screen.width * 0.5f;
        float y = Screen.height * 0.5f;

        if (IsAlive)
        {
            GUI.DrawTexture(new Rect(x - 5, y - 1, 10, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x - 1, y - 5, 2, 10), Texture2D.whiteTexture);
        }
        else
        {
            GUI.Box(new Rect(x - 130, y - 25, 260, 50), "Koniec gry");
        }
    }
}