using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    private static GameManager _instance;
    public static GameManager Instance => _instance;
    [Header("Scene Names")]
    [SerializeField] private string titleSceneName = "Title";
    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private string gameOverSceneName = "GameOver";


    [Header("Bounds Settings")]
    [Tooltip("If provided, the player's position must remain within this collider's bounds. Leave empty to auto-compute from all Tilemaps in the scene.")]
    [SerializeField] private Collider2D levelBoundsCollider;
    [Tooltip("Padding added outside the computed bounds before a reload triggers. Positive values make the playable area slightly larger.")]
    [SerializeField] private float boundsPaddingWorld = 0.5f;

    [Header("Reload Settings")]
    [Tooltip("Seconds to wait after the player leaves bounds before reloading.")]
    [SerializeField] private float reloadDelaySeconds = 0.1f;

    [Header("Tilemap Color Cycle")] 
    [Tooltip("How fast the hue cycles each second (0..1 per second).")]
    [SerializeField, Range(0f, 1f)] private float hueSpeedPerSecond = 0.03f;
    [Tooltip("Global hue offset that persists across scene reloads. This value continuously changes at runtime.")]
    [SerializeField, Range(0f, 1f)] private float currentHueOffset01 = 0.0f;
    [Tooltip("Minimum saturation to use when tilemap original saturation is very low.")]
    [SerializeField, Range(0f, 1f)] private float minSaturation = 0.2f;
    [Tooltip("Minimum value/brightness to use when tilemap original value is very low.")]
    [SerializeField, Range(0f, 1f)] private float minValue = 0.8f;

    [Header("Post Processing")] 
    [Tooltip("Optional. If not set, the manager will automatically pick the highest-priority global Volume in the scene.")]
    [SerializeField] private Volume postProcessVolume;

    [Header("UI")] 
    [Tooltip("Optional player health slider. If assigned, it will be updated to reflect the player's current health each frame.")]
    [SerializeField] private Slider playerHealthSlider;

    [Header("Escalation (per exit)")]
    [Tooltip("How much the post-processing severity increases every time the player leaves bounds (scene reload).")]
    [SerializeField, Range(0f, 1f)] private float severityPerExit = 0.15f;
    [Tooltip("Severity value at which the effects reach their configured maximums.")]
    [SerializeField] private float severityMax = 1.0f;
    [Tooltip("Current accumulated severity. Persists across scene reloads.")]
    [SerializeField] private float currentSeverity = 0f;

    [Header("Post FX Maximums (at max severity)")]
    [SerializeField, Range(-100f, 100f)] private float maxSaturationDelta = 60f; // positive adds saturation
    [SerializeField, Range(-100f, 100f)] private float maxContrastDelta = 40f;
    [SerializeField, Range(-2f, 2f)] private float maxPostExposure = 0.4f;
    [SerializeField, Range(-1f, 1f)] private float maxLensDistortion = -0.5f; // barrel distortion
    [SerializeField, Range(0.5f, 3f)] private float maxGreenChannelMixer = 2.0f; // 1 -> 2 doubles green contribution

    [Header("Green Increase Over Time")]
    [Tooltip("Curve sampled over time to increase green intensity.")]
    [SerializeField] private AnimationCurve greenIntensityCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [Tooltip("Seconds for the green intensity curve to progress from 0 to 1 once.")]
    [SerializeField] private float greenCurveDurationSeconds = 30f;
    [Tooltip("If true, the green curve loops after reaching 1; otherwise it clamps at 1.")]
    [SerializeField] private bool greenCurveLoop = true;
    [Tooltip("Speeds up the green curve based on current severity (1 + severity * this).")]
    [SerializeField] private float greenCurveSeveritySpeedMultiplier = 0.0f;

    private readonly List<Tilemap> _tilemaps = new List<Tilemap>();
    private readonly List<Color> _originalTilemapColors = new List<Color>();

    private Transform _playerTransform;
    [Header("Player Spawning")]
    [SerializeField] private Player playerPrefab;
    private Player _currentPlayer;
    private Bounds _cachedWorldBounds;
    private bool _hasComputedBounds;
    private bool _reloadScheduled;
    private bool _hasExitedOnce;
    private int _exitCount;

    public bool HasExitedOnce => _hasExitedOnce;
    public int ExitCount => _exitCount;

    private VolumeProfile _activePostProfile;
    private ColorAdjustments _ppColorAdjustments;
    private LensDistortion _ppLensDistortion;
    private ChannelMixer _ppChannelMixer;
    private ColorCurves _ppColorCurves; // optional; fallback to ChannelMixer if unavailable

    // Cached baselines (captured from the Volume when scene loads)
    private bool _cachedPostFxBaselines;
    private float _baseSaturation;
    private float _baseContrast;
    private float _basePostExposure;
    private float _baseLensDistortion;
    private float _baseGreenOutGreenIn;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void Start()
    {
        InitializeSceneDependencies();
    }

    private void Update()
    {
        // Continuously cycle hue and apply tints
        AdvanceHue(Time.deltaTime);
        ApplyTilemapTintColors();

        // Only start post-processing changes after the first exit
        if (_hasExitedOnce)
        {
            AdvanceGreenCurve(Time.deltaTime);
            ApplyPostProcessing();
        }

        // Keep watching bounds
        TryComputeBoundsIfNeeded();
        CheckPlayerOutOfBounds();

        // Update UI
        UpdatePlayerHealthUI();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Rebuild references to tilemaps and player when a scene is loaded
        InitializeSceneDependencies();
    }

    private void InitializeSceneDependencies()
    {
        // Player
        EnsureScenePlayer();
        _playerTransform = _currentPlayer != null ? _currentPlayer.transform : FindPlayerTransform();

        // Tilemaps and their original colors
        _tilemaps.Clear();
        _originalTilemapColors.Clear();
        FindAllTilemapsAndCacheOriginalColors();

        // Bounds
        _hasComputedBounds = false;
        _reloadScheduled = false;
        TryComputeBoundsIfNeeded();

        // Post-processing
        ResolvePostProcessingOverrides();

        // Immediately apply current tint state to new scene
        ApplyTilemapTintColors();
        if (_hasExitedOnce)
        {
            ApplyPostProcessing();
        }

        // Refresh UI with current scene's player
        UpdatePlayerHealthUI(force: true);
    }

    private Transform FindPlayerTransform()
    {
        // Prefer explicit Player component if present
        var playerComponent = FindFirstObjectByType<Player>();
        if (playerComponent != null)
            return playerComponent.transform;

        // Fallback to tag
        var playerByTag = GameObject.FindGameObjectWithTag("Player");
        return playerByTag != null ? playerByTag.transform : null;
    }

    private void EnsureScenePlayer()
    {
        // If a player exists in the scene, use it; otherwise spawn from prefab
        var foundPlayers = FindObjectsByType<Player>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (foundPlayers != null && foundPlayers.Length > 0)
        {
            _currentPlayer = foundPlayers[0];
            return;
        }

        if (playerPrefab != null)
        {
            // Try to find a spawn point tagged or named appropriately
            Vector3 spawnPos = Vector3.zero;
            var spawnObj = GameObject.FindWithTag("PlayerSpawn");
            if (spawnObj != null) spawnPos = spawnObj.transform.position;
            _currentPlayer = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        }
    }

    private void FindAllTilemapsAndCacheOriginalColors()
    {
        var found = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var tm in found)
        {
            _tilemaps.Add(tm);
            _originalTilemapColors.Add(tm.color);
        }
    }

    private void TryComputeBoundsIfNeeded()
    {
        if (_hasComputedBounds)
            return;

        if (levelBoundsCollider != null)
        {
            _cachedWorldBounds = levelBoundsCollider.bounds;
            ExpandBoundsPadding(ref _cachedWorldBounds, boundsPaddingWorld);
            _hasComputedBounds = true;
            return;
        }

        // If no explicit collider, compute union of all tilemap bounds in world space
        if (_tilemaps.Count > 0)
        {
            bool first = true;
            Bounds union = default;

            foreach (var tm in _tilemaps)
            {
                // Use localBounds (in tilemap local space) and transform to world space
                Bounds local = tm.localBounds;
                var minWS = tm.transform.TransformPoint(local.min);
                var maxWS = tm.transform.TransformPoint(local.max);
                var centerWS = (minWS + maxWS) * 0.5f;
                var sizeWS = new Vector3(Mathf.Abs(maxWS.x - minWS.x), Mathf.Abs(maxWS.y - minWS.y), Mathf.Abs(maxWS.z - minWS.z));
                Bounds world = new Bounds(centerWS, sizeWS);

                if (first)
                {
                    union = world;
                    first = false;
                }
                else
                {
                    union.Encapsulate(world);
                }
            }

            ExpandBoundsPadding(ref union, boundsPaddingWorld);
            _cachedWorldBounds = union;
            _hasComputedBounds = true;
        }
    }

    private static void ExpandBoundsPadding(ref Bounds b, float padding)
    {
        b.Expand(new Vector3(padding * 2f, padding * 2f, padding * 2f));
    }

    private void CheckPlayerOutOfBounds()
    {
        if (_reloadScheduled)
            return;

        if (_playerTransform == null || !_hasComputedBounds)
            return;

        Vector3 pos = _playerTransform.position;
        if (!_cachedWorldBounds.Contains(pos))
        {
            _reloadScheduled = true;
            Invoke(nameof(ReloadCurrentScene), Mathf.Max(0f, reloadDelaySeconds));
        }
    }

    private void ReloadCurrentScene()
    {
        // Escalate severity per exit and mark that effects can begin
        _hasExitedOnce = true;
        _exitCount++;
        currentSeverity = Mathf.Min(currentSeverity + severityPerExit, severityMax);
        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.name);
        // Note: OnSceneLoaded will reinitialize references and reapply current hue state
    }

    private void UpdatePlayerHealthUI(bool force = false)
    {
        if (playerHealthSlider == null)
            return;

        // Try to find player if missing
        if (_playerTransform == null)
        {
            _playerTransform = FindPlayerTransform();
        }

        Player player = _playerTransform != null ? _playerTransform.GetComponent<Player>() : null;
        if (player == null)
        {
            playerHealthSlider.gameObject.SetActive(false);
            return;
        }

        if (!playerHealthSlider.gameObject.activeSelf)
        {
            playerHealthSlider.gameObject.SetActive(true);
        }

        float max = Mathf.Max(1f, player.MaxHealth);
        playerHealthSlider.minValue = 0f;
        playerHealthSlider.maxValue = max;
        playerHealthSlider.value = Mathf.Clamp(player.CurrentHealth, 0f, max);
    }

    public void OnPlayerDied()
    {
        // Reset state for a fresh run and return to Title
        ResetForNewRun();
        DestroyPersistentPlayer();
        // Ensure gameplay is unpaused
        Time.timeScale = 1f;
        SceneManager.LoadScene(string.IsNullOrEmpty(gameOverSceneName) ? "GameOver" : gameOverSceneName);
    }

    public void ResetForNewRun()
    {
        // Reset game-wide state that persists across scenes
        _hasExitedOnce = false;
        _exitCount = 0;
        currentSeverity = 0f;
        currentHueOffset01 = 0f;
        _greenCurveTime01 = 0f;
        _reloadScheduled = false;
        _cachedPostFxBaselines = false;

        // Reset player runtime state if we are keeping it around
        if (_currentPlayer != null)
        {
            _currentPlayer.ResetState();
        }
    }

    public void DestroyPersistentPlayer()
    {
        if (_currentPlayer != null)
        {
            try { Destroy(_currentPlayer.gameObject); }
            finally { _currentPlayer = null; _playerTransform = null; }
        }
    }

    private void AdvanceHue(float deltaTime)
    {
        if (hueSpeedPerSecond <= 0f)
            return;

        currentHueOffset01 += hueSpeedPerSecond * deltaTime;
        if (currentHueOffset01 > 1f)
            currentHueOffset01 -= Mathf.Floor(currentHueOffset01);
    }

    private float _greenCurveTime01;
    private void AdvanceGreenCurve(float deltaTime)
    {
        if (greenCurveDurationSeconds <= 0f)
            return;

        float severityFactor = 1f + Mathf.Clamp01(severityMax > 0f ? currentSeverity / severityMax : 0f) * greenCurveSeveritySpeedMultiplier;
        float rate = deltaTime / Mathf.Max(0.0001f, greenCurveDurationSeconds) * severityFactor;
        _greenCurveTime01 += rate;
        if (greenCurveLoop)
            _greenCurveTime01 = Mathf.Repeat(_greenCurveTime01, 1f);
        else
            _greenCurveTime01 = Mathf.Clamp01(_greenCurveTime01);
    }

    private void ApplyTilemapTintColors()
    {
        int count = Mathf.Min(_tilemaps.Count, _originalTilemapColors.Count);
        for (int i = 0; i < count; i++)
        {
            var tm = _tilemaps[i];
            if (tm == null)
                continue;

            Color orig = _originalTilemapColors[i];
            Color.RGBToHSV(orig, out float h, out float s, out float v);
            float newH = Mathf.Repeat(h + currentHueOffset01, 1f);
            float newS = Mathf.Max(s, minSaturation);
            float newV = Mathf.Max(v, minValue);
            Color tinted = Color.HSVToRGB(newH, newS, newV);
            tm.color = tinted;
        }
    }

    private void ResolvePostProcessingOverrides()
    {
        _activePostProfile = null;
        _ppColorAdjustments = null;

        if (postProcessVolume == null)
        {
            Volume selected = null;
            float bestPriority = float.NegativeInfinity;

            var volumes = FindObjectsByType<Volume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var vol in volumes)
            {
                if (vol == null || !vol.isActiveAndEnabled || vol.profile == null)
                    continue;

                bool prefer = selected == null
                               || (vol.isGlobal && !selected.isGlobal)
                               || (Mathf.Approximately(vol.priority, selected.priority) ? vol.isGlobal && selected.isGlobal : vol.priority > bestPriority);

                if (prefer)
                {
                    selected = vol;
                    bestPriority = vol.priority;
                }
            }

            postProcessVolume = selected;
        }

        if (postProcessVolume == null || postProcessVolume.profile == null)
            return;

        _activePostProfile = postProcessVolume.profile;
        _activePostProfile.TryGet(out _ppColorAdjustments);
        _activePostProfile.TryGet(out _ppLensDistortion);
        _activePostProfile.TryGet(out _ppChannelMixer);
        _activePostProfile.TryGet(out _ppColorCurves);

        // Cache baselines once per scene load
        _cachedPostFxBaselines = false;
        if (_ppColorAdjustments != null)
        {
            _baseSaturation = _ppColorAdjustments.saturation.value;
            _baseContrast = _ppColorAdjustments.contrast.value;
            _basePostExposure = _ppColorAdjustments.postExposure.value;
            _cachedPostFxBaselines = true;
        }
        if (_ppLensDistortion != null)
        {
            _baseLensDistortion = _ppLensDistortion.intensity.value;
            _cachedPostFxBaselines = true;
        }
        if (_ppChannelMixer != null)
        {
            _baseGreenOutGreenIn = _ppChannelMixer.greenOutGreenIn.value;
            if (_baseGreenOutGreenIn == 0f)
                _baseGreenOutGreenIn = 1f; // URP default identity
            _cachedPostFxBaselines = true;
        }
    }

    private void ApplyPostProcessing()
    {
        if (_activePostProfile == null)
            ResolvePostProcessingOverrides();

        if (!_hasExitedOnce)
            return;

        float t = severityMax > 0f ? Mathf.Clamp01(currentSeverity / severityMax) : 0f;

        if (_ppColorAdjustments != null)
        {
            float hueDegrees = Mathf.Lerp(-180f, 180f, currentHueOffset01);
            _ppColorAdjustments.hueShift.overrideState = true;
            _ppColorAdjustments.hueShift.value = hueDegrees;

            _ppColorAdjustments.saturation.overrideState = true;
            // Ensure we only increase saturation relative to the authored baseline
            float targetSatDelta = Mathf.Max(0f, maxSaturationDelta);
            _ppColorAdjustments.saturation.value = Mathf.Lerp(_baseSaturation, _baseSaturation + targetSatDelta, t);

            _ppColorAdjustments.contrast.overrideState = true;
            _ppColorAdjustments.contrast.value = Mathf.Lerp(_baseContrast, _baseContrast + maxContrastDelta, t);

            _ppColorAdjustments.postExposure.overrideState = true;
            _ppColorAdjustments.postExposure.value = Mathf.Lerp(_basePostExposure, _basePostExposure + maxPostExposure, t);
        }

        if (_ppLensDistortion != null)
        {
            _ppLensDistortion.intensity.overrideState = true;
            _ppLensDistortion.intensity.value = Mathf.Lerp(_baseLensDistortion, maxLensDistortion, t);
        }

        // Prefer ColorCurves (green) if available; else, fall back to ChannelMixer for green intensity
        if (_ppColorCurves != null)
        {
            // We can't easily author keys safely across URP versions; approximate with master saturation via ChannelMixer below.
            // If you already authored a base green curve in the profile, enable override and nudge its overall strength by blending via ChannelMixer too.
        }

        if (_ppChannelMixer != null)
        {
            // Combine per-exit severity with time-based green increase
            float greenTime01 = greenIntensityCurve != null ? Mathf.Clamp01(greenIntensityCurve.Evaluate(_greenCurveTime01)) : _greenCurveTime01;
            float baseVal = Mathf.Lerp(_baseGreenOutGreenIn, Mathf.Max(_baseGreenOutGreenIn, maxGreenChannelMixer), t);
            float timeVal = Mathf.Lerp(_baseGreenOutGreenIn, Mathf.Max(_baseGreenOutGreenIn, maxGreenChannelMixer), greenTime01);
            float finalGreen = Mathf.Max(baseVal, timeVal);

            _ppChannelMixer.greenOutGreenIn.overrideState = true;
            _ppChannelMixer.greenOutGreenIn.value = finalGreen;
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Draw computed bounds in editor for convenience
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        if (levelBoundsCollider != null)
        {
            Bounds b = levelBoundsCollider.bounds;
            ExpandBoundsPadding(ref b, boundsPaddingWorld);
            Gizmos.DrawCube(b.center, b.size);
        }
        else if (_hasComputedBounds)
        {
            Gizmos.DrawCube(_cachedWorldBounds.center, _cachedWorldBounds.size);
        }
    }
}
