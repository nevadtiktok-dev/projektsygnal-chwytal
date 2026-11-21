using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ForestIntro : MonoBehaviour
{
    private static readonly string[] Lines =
    {
        "Obudziłeś się w lesie z jedną misją...",
        "Naprawić wieżę.",
        "Zbieraj zębatki i zanoś je do wieży sygnałowej.\nUważaj na to, co się czai w lesie..."
    };
    public static bool HideGameplayHud { get; set; }
    private const float LineDuration = 4.5f;
    private const float FadeDuration = 0.8f;

    private FirstPersonLook cameraLook;
    private bool restoreCameraLook;
    private bool introActive = true;
    private bool storyStarted;
    private float storyStartTime;

    private Rect StartButton => new Rect(
        (Screen.width - 240f) * 0.5f,
        (Screen.height - 60f) * 0.5f,
        240f, 60f);

    private void Awake()
    {
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        cameraLook = GetComponentInChildren<FirstPersonLook>();
        restoreCameraLook = cameraLook != null && cameraLook.enabled;

        if (restoreCameraLook)
            cameraLook.enabled = false;
    }

    private void Update()
    {
        if (!introActive)
            return;

        if (!storyStarted)
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
                return;

            Vector2 pointer = mouse.position.ReadValue();
#else
            if (!Input.GetMouseButtonDown(0))
                return;

            Vector2 pointer = Input.mousePosition;
#endif
            pointer.y = Screen.height - pointer.y;
            if (!StartButton.Contains(pointer))
                return;

            storyStarted = true;
            storyStartTime = Time.unscaledTime;
            return;
        }

        if (Time.unscaledTime - storyStartTime < Lines.Length * LineDuration)
            return;

        introActive = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (restoreCameraLook && cameraLook != null)
            cameraLook.enabled = true;
    }

    private void OnGUI()
    {
        if (!introActive)
            return;

        int previousDepth = GUI.depth;
        Color previousColor = GUI.color;

        GUI.depth = -100;
        GUI.color = Color.black;
        GUI.DrawTexture(
            new Rect(0f, 0f, Screen.width, Screen.height),
            Texture2D.whiteTexture);

        GUI.color = Color.white;

        if (!storyStarted)
        {
            var buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 25 };
            GUI.Box(StartButton, "Rozpocznij", buttonStyle);
        }
        else
        {
            float elapsed = Time.unscaledTime - storyStartTime;
            int index = Mathf.Clamp(Mathf.FloorToInt(elapsed / LineDuration), 0, Lines.Length - 1);
            float lineTime = elapsed - index * LineDuration;
            float alpha = Mathf.Clamp01(Mathf.Min(
                lineTime / FadeDuration,
                (LineDuration - lineTime) / FadeDuration));

            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                fontSize = Mathf.Clamp(Screen.height / 24, 18, 48)
            };
            style.normal.textColor = Color.white;

            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(
                new Rect(Screen.width * 0.1f, 0f, Screen.width * 0.8f, Screen.height),
                Lines[index], style);
        }

        GUI.color = previousColor;
        GUI.depth = previousDepth;
    }

    private void OnDestroy()
    {
        if (introActive)
            Time.timeScale = 1f;
    }
}