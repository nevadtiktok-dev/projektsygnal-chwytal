using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using System.Collections;
#endif
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class TowerRepairMission : MonoBehaviour
{

    [SerializeField] private Texture2D endingImage;
    private float endingStartedAt;
    private bool failed;
    [Header("Scene references")]
    [SerializeField] private Camera viewCamera;
    [SerializeField] private Transform towerRepairTarget;
    [SerializeField] private GameObject wrenchModel;
    [SerializeField] private GameObject pistolModel;
    [SerializeField] private GameObject gearPrefab;
    [SerializeField] private AudioSource gearPickupSource;
    [SerializeField] private AudioClip gearPickupSound;
    [SerializeField] private Transform[] gearSpawnPoints;

    [Header("Repair balance")]
    [SerializeField, Range(0, 99)] private int startingPermanent = 20;
    [SerializeField, Min(1)] private int repairPerGear = 20;
    [SerializeField, Range(0, 99)] private int maintenanceBuffer = 12;
    [SerializeField, Min(0f)] private float decayGrace = 35f;
    [SerializeField, Min(1f)] private float decayInterval = 20f;
    [SerializeField, Min(0.1f)] private float repairSeconds = 1.5f;
    [SerializeField, Min(0.1f)] private float interactDistance = 3f;

    private readonly List<GameObject> spawnedGears = new List<GameObject>();
    private ForestCombat combat;
    private int permanent;
    private int condition;
    private int carried;
    private int installed;
    private int requiredGears;
    private float nextDecay;
    private float repairProgress;
    private string prompt;
    private bool complete;
    private bool showingWrench;
    private bool pistolWasActive;

    private readonly Dictionary<GameObject, Transform> gearOrigins =
    new Dictionary<GameObject, Transform>();

    private Transform lastCollectedPoint;

    private void SpawnGear(Transform point)
    {
        GameObject gear = Instantiate(gearPrefab, point.position, point.rotation);
        spawnedGears.Add(gear);
        gearOrigins.Add(gear, point);
    }
    private void Start()
    {
        combat = GetComponent<ForestCombat>();
        if (viewCamera == null)
            viewCamera = GetComponentInChildren<Camera>();

        permanent = Mathf.Clamp(startingPermanent, 0, 99);
        int gearValue = Mathf.Max(1, repairPerGear);
        requiredGears = Mathf.CeilToInt((100 - permanent) / (float)gearValue);

        var points = new List<Transform>();
        if (gearSpawnPoints != null)
        {
            foreach (Transform point in gearSpawnPoints)
                if (point != null && !points.Contains(point))
                    points.Add(point);
        }

        if (viewCamera == null || towerRepairTarget == null || gearPrefab == null ||
            points.Count < requiredGears)
        {
            Debug.LogError($"Tower Repair Mission: przypisz kamere, punkt naprawy, prefab zebatki i co najmniej {requiredGears} roznych punktow spawnu.", this);
            enabled = false;
            return;
        }

        if (wrenchModel != null)
            wrenchModel.SetActive(false);

        for (int i = 0; i < requiredGears; i++)
        {
            int choice = Random.Range(0, points.Count);
            Transform point = points[choice];
            points.RemoveAt(choice);
            SpawnGear(point);
        }

        RestoreCondition();
    }
    private void OnDestroy()
    {
        if (failed)
            Time.timeScale = 1f;
    }
    private void Update()
    {
        if (!complete && (failed || (combat != null && !combat.IsAlive)))
        {
#if ENABLE_INPUT_SYSTEM
            bool restartPressed = Keyboard.current?.rKey.wasPressedThisFrame == true;
#else
    bool restartPressed = Input.GetKeyDown(KeyCode.R);
#endif

            if (restartPressed)
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }

            return;
        }
        if (complete || (combat != null && !combat.IsAlive))
        {
            StopRepair();
            prompt = null;
            return;
        }

        if (failed)
            return;

        if (Time.time >= nextDecay)
        {
            condition = Mathf.Max(0, condition - 1);
            nextDecay = Time.time + decayInterval;

            if (condition == 0)
            {
                failed = true;
                prompt = null;
                StopRepair();

                if (TryGetComponent<PlayerMovement>(out var movement))
                    movement.enabled = false;

                if (viewCamera != null &&
                    viewCamera.TryGetComponent<FirstPersonLook>(out var look))
                    look.enabled = false;

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                Time.timeScale = 0f;
                return;
            }
        }

        prompt = null;
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            StopRepair();
            return;
        }

#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        bool pressed = keyboard?.eKey.wasPressedThisFrame == true;
        bool held = keyboard?.eKey.isPressed == true;
#else
        bool pressed = Input.GetKeyDown(KeyCode.E);
        bool held = Input.GetKey(KeyCode.E);
#endif

        FindInteraction(out GameObject gear, out bool atTower);

        if (gear != null)
        {
            prompt = "[E] Podnieś zębatkę";
            if (pressed && gearOrigins.TryGetValue(gear, out Transform point))
            {
                carried++;
                if (gearPickupSource != null && gearPickupSound != null)
                    gearPickupSource.PlayOneShot(gearPickupSound);
                spawnedGears.Remove(gear);
                gearOrigins.Remove(gear);
                Destroy(gear);

                if (lastCollectedPoint != null)
                    StartCoroutine(RespawnGear(lastCollectedPoint));

                lastCollectedPoint = point;
            }
        }
        else if (atTower)
        {
            if (carried > 0)
                prompt = "[Przytrzymaj E] Zamontuj zębatkę";
            else if (condition < MaintenanceLimit)
                prompt = "[Przytrzymaj E] Dokręć wieżę kluczem";
            else
                prompt = "Poszukaj zębatek w lesie";
        }

        if (!atTower || !held || (carried == 0 && condition >= MaintenanceLimit))
        {
            StopRepair();
            return;
        }

        ShowWrench(true);
        repairProgress += Time.deltaTime;
        if (repairProgress < repairSeconds)
            return;

        if (carried > 0)
        {
            carried--;
            installed++;
            permanent = Mathf.Min(100, permanent + Mathf.Max(1, repairPerGear));
        }

        complete = permanent == 100;
        if (complete)
        {
            ForestIntro.HideGameplayHud = true;
            endingStartedAt = Time.unscaledTime;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (viewCamera.TryGetComponent<FirstPersonLook>(out var look))
                look.enabled = false;
        }
        RestoreCondition();
        StopRepair();
    }

    private int MaintenanceLimit => Mathf.Min(99, permanent + maintenanceBuffer);

    private void RestoreCondition()
    {
        condition = complete ? 100 : MaintenanceLimit;
        nextDecay = Time.time + decayGrace + decayInterval;
    }

    private void FindInteraction(out GameObject gear, out bool atTower)
    {
        gear = null;
        atTower = false;
        float nearest = float.PositiveInfinity;
        Ray ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        foreach (RaycastHit hit in Physics.RaycastAll(ray, interactDistance,
                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            if (hit.distance >= nearest || hit.transform.IsChildOf(transform))
                continue;

            GameObject hitGear = spawnedGears.Find(item =>
                item != null && hit.transform.IsChildOf(item.transform));
            bool hitTower = hit.transform.IsChildOf(towerRepairTarget);
            if (hit.collider.isTrigger && hitGear == null && !hitTower)
                continue;

            nearest = hit.distance;
            gear = hitGear;
            atTower = hitTower;
        }
    }

    private void StopRepair()
    {
        repairProgress = 0f;
        ShowWrench(false);
    }

    private void ShowWrench(bool show)
    {
        if (wrenchModel == null || showingWrench == show)
            return;

        showingWrench = show;
        wrenchModel.SetActive(show);
        if (pistolModel == null)
            return;

        if (show)
        {
            pistolWasActive = pistolModel.activeSelf;
            pistolModel.SetActive(false);
        }
        else
        {
            pistolModel.SetActive(pistolWasActive);
        }
    }

    private void OnDisable()
    {
        StopRepair();
    }

    

    private IEnumerator RespawnGear(Transform point)
    {
        yield return new WaitForSeconds(30f);

        if (!complete && point != null && (combat == null || combat.IsAlive))
            SpawnGear(point);
    }
    private void DrawGearRadar()
    {
        if (viewCamera == null || complete || spawnedGears.Count == 0)
            return;

        float width = Mathf.Min(420f, Screen.width - 24f);
        float left = (Screen.width - width) * 0.5f;
        float top = 12f;
        float center = left + width * 0.5f;

        GUI.Box(new Rect(left, top, width, 28f), "ZĘBATKI");
        GUI.DrawTexture(
            new Rect(center - 1f, top + 26f, 2f, 8f),
            Texture2D.whiteTexture);

        // Sam kierunek w poziomie, niezależnie od patrzenia w górę lub w dół.
        Vector3 forward = Quaternion.Euler(
            0f, viewCamera.transform.eulerAngles.y, 0f) * Vector3.forward;

        int row = 0;
        foreach (GameObject gear in spawnedGears)
        {
            if (gear == null)
                continue;

            Vector3 offset = gear.transform.position - viewCamera.transform.position;
            float distance = offset.magnitude;
            offset.y = 0f;

            float angle = Vector3.SignedAngle(forward, offset, Vector3.up);
            float markerX = center + Mathf.Clamp(angle / 90f, -1f, 1f)
                * (width * 0.5f - 42f);

            string marker = angle < -90f ? "◀" : angle > 90f ? "▶" : "◆";
            GUI.Box(
                new Rect(markerX - 40f, top + 36f + row * 22f, 80f, 22f),
                $"{marker} {distance:F0} m");

            row++;
        }
    }
    private void DrawTowerCompass()
    {
        if (viewCamera == null || towerRepairTarget == null)
            return;

        Vector3 direction = towerRepairTarget.position - viewCamera.transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            return;

        float towerYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        float angle = Mathf.DeltaAngle(viewCamera.transform.eulerAngles.y, towerYaw)
            * Mathf.Deg2Rad;

        Vector2 center = new Vector2(52f, Screen.height - 52f);
        Vector2 forward = new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle));
        Vector2 right = new Vector2(-forward.y, forward.x);

        Vector2 tip = center + forward * 22f;
        Vector2 tail = center - forward * 18f;
        Vector2 headBase = tip - forward * 13f;

        GUI.Box(new Rect(center.x - 36f, center.y - 36f, 72f, 72f), GUIContent.none);

        Color previousColor = GUI.color;
        GUI.color = new Color(0.25f, 1f, 0.25f);

        DrawCompassLine(tail, tip);
        DrawCompassLine(tip, headBase - right * 10f);
        DrawCompassLine(tip, headBase + right * 10f);

        GUI.color = previousColor;
    }

    private void DrawCompassLine(Vector2 start, Vector2 end)
    {
        Vector2 delta = end - start;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        Matrix4x4 previousMatrix = GUI.matrix;

        GUIUtility.RotateAroundPivot(angle, start);
        GUI.DrawTexture(
            new Rect(start.x - 2f, start.y - 2f, delta.magnitude + 4f, 4f),
            Texture2D.whiteTexture);

        GUI.matrix = previousMatrix;
    }
    private void DrawEnding()
    {
        const float lineDuration = 5f;
        const float fadeDuration = 1f;

        float elapsed = Time.unscaledTime - endingStartedAt;
        float width = Screen.width;
        float height = Screen.height;

        Color oldColor = GUI.color;
        int oldDepth = GUI.depth;

        GUI.depth = -100;
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture);

        var textStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.Clamp(Mathf.RoundToInt(height * 0.045f), 18, 56),
            wordWrap = true
        };
        textStyle.normal.textColor = Color.white;

        if (elapsed < lineDuration * 2f)
        {
            bool firstLine = elapsed < lineDuration;
            float timeInLine = firstLine ? elapsed : elapsed - lineDuration;
            float fade = Mathf.Clamp01(Mathf.Min(
                timeInLine / fadeDuration,
                (lineDuration - timeInLine) / fadeDuration));

            GUI.color = new Color(1f, 1f, 1f, fade);
            string line = firstLine
                ? "Udało ci się naprawić wieżę, która była przez cały czas poza zasięgiem..."
                : "Odzyskałeś ten zasięg.";

            GUI.Label(new Rect(width * 0.1f, 0, width * 0.8f, height), line, textStyle);
        }
        else
        {
            float fade = Mathf.Clamp01((elapsed - lineDuration * 2f) / fadeDuration);
            GUI.color = new Color(1f, 1f, 1f, fade);

            if (endingImage != null)
            {
                GUI.DrawTexture(
                    new Rect(width * 0.1f, height * 0.08f, width * 0.8f, height * 0.72f),
                    endingImage, ScaleMode.ScaleToFit, true);
            }

            float captionY = endingImage != null ? height * 0.82f : height * 0.4f;
            GUI.Label(new Rect(0, captionY, width, height * 0.16f),
                "Budżet był równy zero, a Rockstar Games i tak nam buty wiąże.", textStyle);
        }

        GUI.color = oldColor;
        GUI.depth = oldDepth;
    }
    private void OnGUI()
    {
        if (complete)
        {
            DrawEnding();
            return;
        }
        if (failed)
        {
            GUI.Box(
                new Rect((Screen.width - 360f) * 0.5f,
                         (Screen.height - 70f) * 0.5f, 360f, 70f),
                "PRZEGRANA\nWieża uległa awarii (0%)");
            return;
        }
        DrawTowerCompass();
        DrawGearRadar();
        float x = Mathf.Max(4f, Screen.width - 282f);
        GUI.Box(new Rect(x, 12, 270, 83), "Wieża");
        GUI.Label(new Rect(x + 10, 34, 250, 22),
            $"Stan: {condition}%   |   Trwały: {permanent}%");
        GUI.Label(new Rect(x + 10, 57, 250, 22),
            $"Zębatki: {carried}   |   Zamontowane: {installed}/{requiredGears}");

        string message = complete ? "Wieża naprawiona — 100%" : prompt;
        if (!string.IsNullOrEmpty(message))
        {
            if (repairProgress > 0f)
                message += $"   {Mathf.Clamp01(repairProgress / repairSeconds):P0}";
            GUI.Box(new Rect((Screen.width - 380f) * 0.5f, Screen.height - 86f, 380f, 32f), message);
        }
    }
}