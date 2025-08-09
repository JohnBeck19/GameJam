using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Drop this on any GameObject in your scene to see logs inside builds.
// Toggle with F1 (or set showOnStart = true).
public class InGameLogOverlay : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private bool showOnStart = false;
    [SerializeField] private int maxLines = 300;
    [SerializeField] private int fontSize = 12;
    [SerializeField] private KeyCode toggleKey = KeyCode.F1;

    [Header("Content")] 
    [SerializeField] private bool showTimestamps = true;
    [SerializeField] private bool showStackTraceOnError = true;

    private readonly object _lock = new object();
    private readonly List<LogEntry> _entries = new List<LogEntry>();
    private Vector2 _scroll;
    private bool _visible;

    struct LogEntry
    {
        public DateTime Time;
        public string Message;
        public string Stack;
        public LogType Type;
    }

    void OnEnable()
    {
        Application.logMessageReceivedThreaded += HandleLogThreaded;
        _visible = showOnStart;
        LogLocal("InGameLogOverlay ready. Press " + toggleKey + " to toggle.");
    }

    void OnDisable()
    {
        Application.logMessageReceivedThreaded -= HandleLogThreaded;
    }

    void Update()
    {
        if (WasKeyPressedThisFrame(toggleKey))
        {
            _visible = !_visible;
        }
    }

    bool WasKeyPressedThisFrame(KeyCode keyCode)
    {
#if ENABLE_INPUT_SYSTEM
        // New Input System path
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null)
            return false;
        // Try to map KeyCode name to InputSystem.Key enum (e.g., F1, F2, A, B, etc.)
        if (System.Enum.TryParse<UnityEngine.InputSystem.Key>(keyCode.ToString(), out var inputSystemKey))
        {
            var control = keyboard[inputSystemKey];
            return control != null && control.wasPressedThisFrame;
        }
        // Fallback for common toggle default (F1)
        if (keyCode == KeyCode.F1) return keyboard.f1Key.wasPressedThisFrame;
        return false;
#else
        // Legacy Input Manager path
        return Input.GetKeyDown(keyCode);
#endif
    }

    void HandleLogThreaded(string condition, string stackTrace, LogType type)
    {
        var entry = new LogEntry
        {
            Time = DateTime.Now,
            Message = condition,
            Stack = stackTrace,
            Type = type
        };
        lock (_lock)
        {
            _entries.Add(entry);
            if (_entries.Count > maxLines)
            {
                _entries.RemoveRange(0, _entries.Count - maxLines);
            }
        }
    }

    void OnGUI()
    {
        if (!_visible)
            return;

        var prevFontSize = GUI.skin.label.fontSize;
        GUI.skin.label.fontSize = fontSize;

        const int margin = 8;
        Rect panel = new Rect(margin, margin, Screen.width - margin * 2, Screen.height / 2f);
        GUI.Box(panel, "Console (F1 to hide)");

        GUILayout.BeginArea(new Rect(panel.x + 8, panel.y + 24, panel.width - 16, panel.height - 32));
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear", GUILayout.Width(80)))
        {
            lock (_lock) _entries.Clear();
        }
        if (GUILayout.Button("Copy", GUILayout.Width(80)))
        {
            GUIUtility.systemCopyBuffer = BuildText();
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        List<LogEntry> snapshot;
        lock (_lock)
        {
            snapshot = new List<LogEntry>(_entries);
        }

        try
        {
            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (var e in snapshot)
            {
                string prefix = e.Type switch
                {
                    LogType.Error => "[Error] ",
                    LogType.Exception => "[Exception] ",
                    LogType.Warning => "[Warn] ",
                    LogType.Assert => "[Assert] ",
                    _ => ""
                };
                string time = showTimestamps ? ("[" + e.Time.ToString("HH:mm:ss.fff") + "] ") : string.Empty;
                GUILayout.Label(time + prefix + e.Message);
                if (showStackTraceOnError && (e.Type == LogType.Error || e.Type == LogType.Exception))
                {
                    if (!string.IsNullOrEmpty(e.Stack))
                    {
                        var c = GUI.color;
                        GUI.color = new Color(1f, 1f, 1f, 0.8f);
                        GUILayout.Label(e.Stack);
                        GUI.color = c;
                    }
                }
            }
        }
        finally
        {
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        GUI.skin.label.fontSize = prevFontSize;
    }

    string BuildText()
    {
        var sb = new StringBuilder();
        lock (_lock)
        {
            foreach (var e in _entries)
            {
                string time = showTimestamps ? ("[" + e.Time.ToString("HH:mm:ss.fff") + "] ") : string.Empty;
                sb.AppendLine(time + e.Type + ": " + e.Message);
                if (showStackTraceOnError && (e.Type == LogType.Error || e.Type == LogType.Exception))
                {
                    if (!string.IsNullOrEmpty(e.Stack))
                    {
                        sb.AppendLine(e.Stack);
                    }
                }
            }
        }
        return sb.ToString();
    }

    void LogLocal(string message)
    {
        HandleLogThreaded(message, string.Empty, LogType.Log);
    }
}


