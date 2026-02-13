using System.Collections.Generic;
using UnityEngine;

public class AudienceEngagementManager : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Transform charactersRoot;

    [Tooltip("Teacher target transform: Camera Rig -> Tracking Space -> LeftEyeAnchor")]
    [SerializeField] private Transform teacherTarget;

    [Tooltip("Board target transform (place an empty at board center). Looking at board counts as focused.")]
    [SerializeField] private Transform boardTarget;

    [Tooltip("Questionnaire panel root. When active, engagement tracking pauses.")]
    [SerializeField] private GameObject questionnairePanelRoot;

    [Header("Sampling")]
    [SerializeField] private float tickInterval = 0.10f; // 10 Hz
    [Range(0f, 1f)]
    [SerializeField] private float smoothing = 0.15f;    // EMA smoothing for global Engagement01

    [Header("Focus Detection")]
    [Tooltip("Max angle (degrees) between head-forward and target direction to count as 'looking at'.")]
    [SerializeField] private float lookAngleDeg = 25f;

    [Tooltip("Must keep looking continuously for this long to count as focused (reduces flicker).")]
    [SerializeField] private float focusDwellSeconds = 0.40f;

    [Header("Head Motion (Fidget)")]
    [Tooltip("Approx window length (seconds) used for per-character rolling metrics.")]
    [SerializeField] private float rollingWindowSeconds = 8f;

    [Tooltip("Angular speed (deg/sec) above this starts being considered fidgety.")]
    [SerializeField] private float fidgetStartDegPerSec = 35f;

    [Tooltip("Angular speed (deg/sec) at/above this is considered fully fidgety (1.0).")]
    [SerializeField] private float fidgetMaxDegPerSec = 110f;

    [Tooltip("How much fidget reduces engagement (0..1).")]
    [Range(0f, 1f)]
    [SerializeField] private float fidgetPenaltyWeight = 0.35f;

    [Header("Cluster Rules")]
    [Tooltip("Engaged if focusFrac >= this and fidget <= mid.")]
    [Range(0f, 1f)]
    [SerializeField] private float engagedFocusFrac = 0.70f;

    [Tooltip("Disengaged if focusFrac <= this.")]
    [Range(0f, 1f)]
    [SerializeField] private float disengagedFocusFrac = 0.35f;

    [Tooltip("Confused if switching rate is high while still somewhat focused.")]
    [SerializeField] private float confusedSwitchRatePerSec = 0.35f; // switches/sec in rolling window

    [Header("Debug")]
    [SerializeField] private bool logToConsole = false;

    [Tooltip("Print debug logs at most this often (seconds).")]
    [SerializeField] private float logIntervalSeconds = 3f;

    // Public outputs
    public float Engagement01 { get; private set; } = 1f;

    public int TotalCount { get; private set; }
    public int TeacherFocusedCount { get; private set; }
    public int BoardFocusedCount { get; private set; }
    public int AwayCount { get; private set; }

    public int EngagedCount { get; private set; }
    public int ConfusedCount { get; private set; }
    public int DisengagedCount { get; private set; }

    // Internal
    private readonly List<HeadRotate> _headRotates = new List<HeadRotate>();

    private class CharState
    {
        public float dwell;
        public Quaternion lastHeadRot;
        public bool hasLastRot;

        // Rolling (decayed) times (approx last rollingWindowSeconds)
        public float tTeacher;
        public float tBoard;
        public float tAway;

        // Rolling (decayed) switch count between teacher/board/away
        public float switches;

        public LookLabel lastLook = LookLabel.Away;

        // Rolling (decayed) fidget metric (0..1)
        public float fidget01;
    }

    private enum LookLabel { Teacher, Board, Away }

    private readonly Dictionary<HeadRotate, CharState> _state = new Dictionary<HeadRotate, CharState>();

    private float _nextTickTime;
    private bool _wasQuestionnaireActive;

    private float _nextLogTime; // NEW

    private void Awake()
    {
        RefreshAudienceList();
        _nextTickTime = Time.time + tickInterval;

        _nextLogTime = Time.time + Mathf.Max(0.1f, logIntervalSeconds); // NEW
    }

    private void OnEnable()
    {
        RefreshAudienceList();
    }

    [ContextMenu("Refresh Audience List")]
    public void RefreshAudienceList()
    {
        _headRotates.Clear();
        _state.Clear();

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

        foreach (var hr in _headRotates)
        {
            if (!hr) continue;
            _state[hr] = new CharState();
        }
    }

    private void Update()
    {
        bool questionnaireActive = (questionnairePanelRoot && questionnairePanelRoot.activeInHierarchy);

        // If questionnaire just became active, reset dwell timers so we resume cleanly later
        if (questionnaireActive && !_wasQuestionnaireActive)
            ResetDwellTimers();

        _wasQuestionnaireActive = questionnaireActive;

        // Pause rule: during questionnaire, stop updating engagement
        if (questionnaireActive)
            return;

        if (Time.time < _nextTickTime)
            return;

        _nextTickTime = Time.time + tickInterval;

        Tick();
    }

    private void Tick()
    {
        int total = 0;

        int teacherFocused = 0;
        int boardFocused = 0;
        int away = 0;

        int engaged = 0;
        int confused = 0;
        int disengaged = 0;

        float engagementSum = 0f;

        float decay = ComputeDecayFactor();

        foreach (var hr in _headRotates)
        {
            if (!hr) continue;
            if (!_state.TryGetValue(hr, out var st)) continue;

            total++;

            // Decay rolling history
            st.tTeacher *= decay;
            st.tBoard *= decay;
            st.tAway *= decay;
            st.switches *= decay;
            st.fidget01 *= decay;

            // If refs missing, treat as away
            if (!hr.HeadObject || !hr.HeafForward)
            {
                st.dwell = 0f;
                st.tAway += tickInterval;
                away++;
                disengaged++;
                continue;
            }

            Vector3 headPos = hr.HeadObject.position;
            Vector3 headFwd = hr.HeafForward.forward;

            bool lookTeacherNow = IsLookingAt(headPos, headFwd, teacherTarget, lookAngleDeg);
            bool lookBoardNow = IsLookingAt(headPos, headFwd, boardTarget, lookAngleDeg);

            LookLabel lookLabel;
            if (lookTeacherNow) lookLabel = LookLabel.Teacher;
            else if (lookBoardNow) lookLabel = LookLabel.Board;
            else lookLabel = LookLabel.Away;

            // Switch tracking (rolling)
            if (lookLabel != st.lastLook)
                st.switches += 1f;
            st.lastLook = lookLabel;

            // Update rolling time buckets
            switch (lookLabel)
            {
                case LookLabel.Teacher: st.tTeacher += tickInterval; break;
                case LookLabel.Board: st.tBoard += tickInterval; break;
                case LookLabel.Away: st.tAway += tickInterval; break;
            }

            // Fidget: angular speed of the head object
            Quaternion curRot = hr.HeadObject.rotation;
            if (st.hasLastRot)
            {
                float deg = Quaternion.Angle(st.lastHeadRot, curRot);
                float degPerSec = deg / Mathf.Max(0.0001f, tickInterval);

                float f = Mathf.InverseLerp(fidgetStartDegPerSec, fidgetMaxDegPerSec, degPerSec);
                f = Mathf.Clamp01(f);

                // rolling fidget in [0..1]
                st.fidget01 += f * (1f - decay); // small injection each tick
            }
            st.lastHeadRot = curRot;
            st.hasLastRot = true;

            // Dwell: only count as "focused" if sustained
            bool focusedNow = (lookLabel == LookLabel.Teacher || lookLabel == LookLabel.Board);
            st.dwell = focusedNow ? (st.dwell + tickInterval) : 0f;

            bool dwellMet = st.dwell >= focusDwellSeconds;

            // Per-character focus fraction over rolling window
            float denom = Mathf.Max(0.0001f, st.tTeacher + st.tBoard + st.tAway);
            float focusFrac = (st.tTeacher + st.tBoard) / denom;

            // Switch rate over rolling window (approx)
            float switchRate = st.switches / Mathf.Max(0.0001f, rollingWindowSeconds);

            // Engagement per character (0..1):
            float engagement = focusFrac * (1f - st.fidget01 * fidgetPenaltyWeight);
            engagement = Mathf.Clamp01(engagement);

            engagementSum += engagement;

            // Count focused targets (only when dwell met to stabilize)
            if (dwellMet)
            {
                if (lookLabel == LookLabel.Teacher) teacherFocused++;
                else if (lookLabel == LookLabel.Board) boardFocused++;
                else away++;
            }
            else
            {
                away++;
            }

            // Cluster classification
            if (focusFrac >= engagedFocusFrac && st.fidget01 < 0.45f)
            {
                engaged++;
            }
            else if (focusFrac <= disengagedFocusFrac)
            {
                disengaged++;
            }
            else
            {
                if (switchRate >= confusedSwitchRatePerSec || st.fidget01 >= 0.60f)
                    confused++;
                else
                    engaged++;
            }
        }

        TotalCount = total;
        TeacherFocusedCount = teacherFocused;
        BoardFocusedCount = boardFocused;
        AwayCount = away;

        EngagedCount = engaged;
        ConfusedCount = confused;
        DisengagedCount = disengaged;

        // Global engagement
        float raw = (total <= 0) ? 1f : (engagementSum / total);
        Engagement01 = Mathf.Lerp(Engagement01, raw, smoothing);

        // Throttled logging (every ~logIntervalSeconds)
        if (logToConsole && Time.time >= _nextLogTime)
        {
            Debug.Log(
                $"AudienceEngagement: Engagement={Engagement01:F2} " +
                $"Teacher={TeacherFocusedCount} Board={BoardFocusedCount} Away={AwayCount} / Total={TotalCount} | " +
                $"Clusters E={EngagedCount} C={ConfusedCount} D={DisengagedCount}"
            );

            _nextLogTime = Time.time + Mathf.Max(0.1f, logIntervalSeconds);
        }
    }

    private float ComputeDecayFactor()
    {
        float w = Mathf.Max(0.5f, rollingWindowSeconds);
        return Mathf.Exp(-tickInterval / w);
    }

    private static bool IsLookingAt(Vector3 headPos, Vector3 headFwd, Transform target, float maxAngleDeg)
    {
        if (!target) return false;

        Vector3 to = (target.position - headPos);
        if (to.sqrMagnitude < 0.00001f) return true;

        float ang = Vector3.Angle(headFwd, to.normalized);
        return ang <= maxAngleDeg;
    }

    private void ResetDwellTimers()
    {
        foreach (var kv in _state)
            kv.Value.dwell = 0f;
    }
}
