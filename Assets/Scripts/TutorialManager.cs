using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class TutorialManager : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private CanvasGroup canvasGroup; // optional, for fades
    [SerializeField] private GameObject panelRoot; // optional, enable/disable container

    [Header("Flow")]
    [SerializeField] private bool autoStart = true;
    [SerializeField] private bool pauseGameDuringTutorial = true;
    [SerializeField] private bool clickToAdvance = true;
    [SerializeField] private KeyCode advanceKey = KeyCode.Space;
    [SerializeField] private KeyCode skipKey = KeyCode.Escape;
    [SerializeField] private float minTimePerStep = 0.25f;
    [SerializeField] private float fadeDuration = 0.2f;

    [Header("Show Limits")]
    [Tooltip("Show the return tutorial on exits 1..N. Set to 0 to never show returns; -1 shows on all exits.")]
    [SerializeField] private int maxReturnShows = 1;

    [Header("Steps")]
    [TextArea(2, 5)]
    [SerializeField] private List<string> initialSteps = new List<string>
    {
        "Welcome!",
        "Use WASD to move.",
        "Aim with the mouse. Press Space to dash.",
        "Pick up cards and weapons to grow stronger.",
        "Stay alert... enemies will chase and shoot.",
        "Whatever you do... don't try to leave."
    };

    [TextArea(2, 5)]
    [SerializeField] private List<string> returnSteps = new List<string>
    {
        "You shouldn't have left...",
        "The world changes each time you try.",
        "It gets worse out there.",
        "Stay inside the bounds... if you can.",
    };

    [System.Serializable]
    public class ReturnStage
    {
        [Tooltip("Which exact exit count this stage should trigger on (1 = first return, 2 = second return, etc.)")]
        public int exitCount = 1;
        [TextArea(2, 5)] public List<string> lines = new List<string>();
    }

    [Header("Return Stages (optional)")]
    [Tooltip("If set, these override the default returnSteps for the matching exit count.")]
    [SerializeField] private List<ReturnStage> returnStages = new List<ReturnStage>();

    private Coroutine _runner;
    private bool _isRunning;
    private int _stepIndex;
    private float _stepTimer;
    private float _prevTimeScale = 1f;
    private bool _timeScaleCaptured;
    private List<string> _activeSteps = new List<string>();

    public bool IsRunning => _isRunning;

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (messageText != null) messageText.text = string.Empty;
    }

    private void Start()
    {
        if (autoStart)
        {
            StartTutorial();
        }
    }

    public void StartTutorial()
    {
        if (_isRunning)
            return;

        // Choose steps based on exit count
        _activeSteps.Clear();
        var gm = FindFirstObjectByType<GameManager>();
        int exitCount = gm != null ? gm.ExitCount : 0;

        List<string> source;
        if (exitCount <= 0)
        {
            source = initialSteps;
        }
        else
        {
            if (maxReturnShows == 0)
                return;
            if (maxReturnShows > 0 && exitCount > maxReturnShows)
                return;

            source = GetReturnStepsForExitCount(exitCount);
        }

        if (source != null && source.Count > 0)
            _activeSteps.AddRange(source);

        if (_activeSteps.Count == 0)
            return;

        _runner = StartCoroutine(RunTutorial());
    }

    public void StopTutorial()
    {
        if (_runner != null)
        {
            StopCoroutine(_runner);
            _runner = null;
        }
        SetVisible(false);
        _isRunning = false;
        ResumeIfPaused();
    }

    private IEnumerator RunTutorial()
    {
        _isRunning = true;
        _stepIndex = 0;
        _stepTimer = 0f;
        SetVisible(true);
        if (pauseGameDuringTutorial)
            PauseGame();
        yield return Fade(1f);

        while (_stepIndex < _activeSteps.Count)
        {
            string text = _activeSteps[_stepIndex];
            if (messageText != null) messageText.text = text;

            _stepTimer = 0f;
            bool advanced = false;

            while (!advanced)
            {
                _stepTimer += Time.unscaledDeltaTime;

                bool canAdvance = _stepTimer >= minTimePerStep;
                bool advanceRequested = false;

                if (canAdvance)
                {
                    if (IsAdvancePressed())
                        advanceRequested = true;
                }

                if (IsSkipPressed())
                {
                    // Skip the rest
                    _stepIndex = _activeSteps.Count - 1; // move to last line
                    break;
                }

                if (advanceRequested)
                {
                    advanced = true;
                }

                yield return null;
            }

            _stepIndex++;
        }

        yield return Fade(0f);
        SetVisible(false);
        _isRunning = false;
        _runner = null;
        ResumeIfPaused();
    }

    private void SetVisible(bool value)
    {
        if (panelRoot != null) panelRoot.SetActive(value);
        if (canvasGroup != null) canvasGroup.blocksRaycasts = value;
    }

    private IEnumerator Fade(float target)
    {
        if (canvasGroup == null || fadeDuration <= 0f)
            yield break;

        float start = canvasGroup.alpha;
        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / fadeDuration);
            canvasGroup.alpha = Mathf.Lerp(start, target, t);
            yield return null;
        }
        canvasGroup.alpha = target;
    }

    private bool IsAdvancePressed()
    {
#if ENABLE_INPUT_SYSTEM
        bool click = clickToAdvance && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool key = Keyboard.current != null && WasKeyPressed(Keyboard.current, advanceKey);
        return click || key;
#else
        bool click = clickToAdvance && Input.GetMouseButtonDown(0);
        bool key = Input.GetKeyDown(advanceKey);
        return click || key;
#endif
    }

    private bool IsSkipPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && WasKeyPressed(Keyboard.current, skipKey);
#else
        return Input.GetKeyDown(skipKey);
#endif
    }

    private List<string> GetReturnStepsForExitCount(int exitCount)
    {
        if (returnStages != null && returnStages.Count > 0)
        {
            for (int i = 0; i < returnStages.Count; i++)
            {
                var s = returnStages[i];
                if (s != null && s.exitCount == exitCount && s.lines != null && s.lines.Count > 0)
                {
                    return s.lines;
                }
            }
        }
        return returnSteps;
    }

#if ENABLE_INPUT_SYSTEM
    private static bool WasKeyPressed(Keyboard keyboard, KeyCode keyCode)
    {
        if (keyboard == null) return false;
        switch (keyCode)
        {
            case KeyCode.Space: return keyboard.spaceKey.wasPressedThisFrame;
            case KeyCode.Escape: return keyboard.escapeKey.wasPressedThisFrame;
            case KeyCode.Return: return keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
            case KeyCode.Tab: return keyboard.tabKey.wasPressedThisFrame;
            case KeyCode.BackQuote: return keyboard.backquoteKey.wasPressedThisFrame;
            default:
                // Common alphanumerics (A-Z)
                if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                {
                    int offset = (int)keyCode - (int)KeyCode.A;
                    var keyControl = keyboard[(Key)((int)Key.A + offset)];
                    return keyControl != null && keyControl.wasPressedThisFrame;
                }
                // Digits 0-9
                if (keyCode >= KeyCode.Alpha0 && keyCode <= KeyCode.Alpha9)
                {
                    int offset = (int)keyCode - (int)KeyCode.Alpha0;
                    var keyControl = keyboard[(Key)((int)Key.Digit0 + offset)];
                    return keyControl != null && keyControl.wasPressedThisFrame;
                }
                return false;
        }
    }
#endif

    private void PauseGame()
    {
        if (_timeScaleCaptured)
            return;
        _prevTimeScale = Time.timeScale;
        _timeScaleCaptured = true;
        Time.timeScale = 0f;
        // Optional: pause audio if needed
        // AudioListener.pause = true;
    }

    private void ResumeIfPaused()
    {
        if (!_timeScaleCaptured)
            return;
        Time.timeScale = _prevTimeScale;
        _timeScaleCaptured = false;
        // AudioListener.pause = false;
    }
}


