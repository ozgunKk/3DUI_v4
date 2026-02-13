using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;

public class InGameLogger : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text logText;

    [Header("Filter (show logs that contain ANY of these)")]
    [SerializeField]
    private string[] includeAnyContains = new[]
    {
        "BoardSlideDeck",
        "PdfUploadPanel",
        "Slides",
        "deck",
        "page"
    };

    [Header("Buffer")]
    [SerializeField] private int maxLines = 80;

    [Header("Startup")]
    [SerializeField] private bool startHidden = true;

    private readonly Queue<string> _lines = new Queue<string>();
    private readonly StringBuilder _sb = new StringBuilder(8_192);
    private bool _visible;

    private void Awake()
    {
        _visible = !startHidden;
        ApplyVisibleState();

        Application.logMessageReceived += HandleLog;
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= HandleLog;
    }

    // Hook to your hidden button
    public void ToggleVisible()
    {
        _visible = !_visible;
        ApplyVisibleState();

        if (_visible)
            RebuildText();
    }

    public void Clear()
    {
        _lines.Clear();
        if (logText) logText.text = "";
    }

    private void ApplyVisibleState()
    {
        if (panelRoot) panelRoot.SetActive(_visible);
    }

    private void HandleLog(string condition, string stackTrace, LogType type)
    {
        if (!MatchesFilter(condition))
            return;

        string color = type switch
        {
            LogType.Warning => "#FFD166",
            LogType.Error => "#EF476F",
            LogType.Exception => "#EF476F",
            LogType.Assert => "#EF476F",
            _ => "#E6E6E6"
        };

        string line = $"<color={color}>[{type}]</color> {Escape(condition)}";

        _lines.Enqueue(line);
        while (_lines.Count > Mathf.Max(10, maxLines))
            _lines.Dequeue();

        if (_visible)
            RebuildText();
    }

    private bool MatchesFilter(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return false;
        if (includeAnyContains == null || includeAnyContains.Length == 0) return true;

        // Case-insensitive contains
        string lower = msg.ToLowerInvariant();
        for (int i = 0; i < includeAnyContains.Length; i++)
        {
            string k = includeAnyContains[i];
            if (string.IsNullOrEmpty(k)) continue;
            if (lower.Contains(k.ToLowerInvariant()))
                return true;
        }
        return false;
    }

    private void RebuildText()
    {
        if (!logText) return;

        _sb.Clear();
        foreach (var l in _lines)
        {
            _sb.Append(l);
            _sb.Append("\n\n");
        }
        logText.text = _sb.ToString();
    }

    private static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
