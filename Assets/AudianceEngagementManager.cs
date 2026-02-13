using System.Collections.Generic;
using UnityEngine;

public class AudienceEngagementManager : MonoBehaviour
{
    [Header("Scene References")]
    [Tooltip("Parent object that contains all audience characters.")]
    [SerializeField] private Transform charactersRoot; 

    [Tooltip("Teacher target transform: Camera Rig -> Tracking Space -> LeftEyeAnchor")]
    [SerializeField] private Transform teacherTarget;

    [Tooltip("Questionnaire panel root. When active, engagement tracking pauses.")]
    [SerializeField] private GameObject questionnairePanelRoot;

    [Header("Sampling")]
    [SerializeField] private float tickInterval = 0.10f;    
    [Range(0f, 1f)]
    [SerializeField] private float smoothing = 0.15f;     

    [Header("Focus rules")]
    [Tooltip("Character must be focused continuously for this long to be counted as focused.")]
    [SerializeField] private float focusDwellSeconds = 0.40f;

    [Header("Debug")]
    [SerializeField] private bool logToConsole = false;

    public float Engagement01 { get; private set; } = 1f;    // 0..1
    public int FocusedCount { get; private set; } = 0;
    public int TotalCount { get; private set; } = 0;

    private readonly List<HeadRotate> _headRotates = new List<HeadRotate>();
    private readonly Dictionary<HeadRotate, float> _focusTimers = new Dictionary<HeadRotate, float>();

    private float _nextTickTime;
    private bool _wasQuestionnaireActive = false;

    private void Awake()
    {
        RefreshAudienceList();
        _nextTickTime = Time.time + tickInterval;
    }

    private void OnEnable()
    {
        RefreshAudienceList();
    }

    [ContextMenu("Refresh Audience List")]
    public void RefreshAudienceList()
    {
        _headRotates.Clear();
        _focusTimers.Clear();

        if (!charactersRoot)
        {
            Debug.LogWarning("AudienceEngagementManager: charactersRoot is not assigned.");
            return;
        }

        charactersRoot.GetComponentsInChildren(true, _headRotates);

        if (_headRotates.Count == 0)
        {
            Debug.LogWarning("AudienceEngagementManager: No HeadRotate components found under Characters.");
            return;
        }

        // Initialize dwell timers
        for (int i = 0; i < _headRotates.Count; i++)
        {
            var hr = _headRotates[i];
            if (!hr) continue;
            _focusTimers[hr] = 0f;
        }
    }

    private void Update()
    {
        bool questionnaireActive = (questionnairePanelRoot && questionnairePanelRoot.activeInHierarchy);

        // If questionnaire just became active, reset timers so we resume cleanly later
        if (questionnaireActive && !_wasQuestionnaireActive)
        {
            ResetAllTimers();
        }
        _wasQuestionnaireActive = questionnaireActive;

        // Pause rule: during questionnaire, stop updating engagement
        if (questionnaireActive)
            return;

        if (Time.time < _nextTickTime)
            return;

        _nextTickTime = Time.time + tickInterval;

        int total = 0;
        int focused = 0;

        for (int i = 0; i < _headRotates.Count; i++)
        {
            var hr = _headRotates[i];
            if (!hr) continue;

            total++;

            bool targetMatches = (teacherTarget == null) || (hr.TargetObject == teacherTarget);

            bool focusedNow = hr.IsActiveEffective && hr.IsInRange && targetMatches;


            // Dwell accumulation
            float t = 0f;
            _focusTimers.TryGetValue(hr, out t);
            t = focusedNow ? (t + tickInterval) : 0f;
            _focusTimers[hr] = t;

            // Count focused only if dwell met
            if (t >= focusDwellSeconds)
                focused++;
        }

        TotalCount = total;
        FocusedCount = focused;

        float raw = (total <= 0) ? 1f : (float)focused / total;

        Engagement01 = Mathf.Lerp(Engagement01, raw, smoothing);

        if (logToConsole)
            Debug.Log($"Engagement={Engagement01:F2} Focused={focused}/{total} (dwell={focusDwellSeconds:0.00}s)");
    }

    private void ResetAllTimers()
    {
        var keys = new List<HeadRotate>(_focusTimers.Keys);
        for (int i = 0; i < keys.Count; i++)
            _focusTimers[keys[i]] = 0f;
    }
}
