using UnityEngine;

public class SessionPerformanceScorer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private VideoPanelSequence videoPanelSequence;
    [SerializeField] private PresenterActivityTracker presenterTracker;
    [SerializeField] private AudienceEngagementManager audienceEngagement;
    [SerializeField] private QuestionnaireResultsPresenter resultsPresenter;

    [Header("Final Score Weights")]
    [Tooltip("How much presenter activity influences final score.")]
    [Range(0f, 1f)][SerializeField] private float wPresenter = 0.55f;

    [Tooltip("How much audience engagement influences final score.")]
    [Range(0f, 1f)][SerializeField] private float wAudience = 0.45f;

    [Header("Sampling (session averages)")]
    [Tooltip("Averages update frequency (seconds).")]
    [SerializeField] private float sampleInterval = 0.25f;

    [Header("Debug")]
    [SerializeField] private bool printBreakdownToConsole = false;

    // Session aggregates
    private bool _sessionRunning = true;
    private bool _finalComputed = false;

    private float _nextSampleTime;

    private float _presenterSum;
    private float _audienceSum;
    private int _samples;

    private float _spotlightTime;
    private float _sessionStartTime;

    // Output
    public float FinalScore01 { get; private set; }
    public float AvgPresenter01 { get; private set; }
    public float AvgAudience01 { get; private set; }
    public float SessionDurationSec { get; private set; }

    private void Awake()
    {
        _sessionStartTime = Time.time;

        if (videoPanelSequence)
            videoPanelSequence.OnQuestionnaireShown += OnQuestionnaireShown;
        else
            Debug.LogWarning("SessionPerformanceScorer: videoPanelSequence not assigned.");

        _nextSampleTime = Time.time + sampleInterval;
    }

    private void OnDestroy()
    {
        if (videoPanelSequence)
            videoPanelSequence.OnQuestionnaireShown -= OnQuestionnaireShown;
    }

    private void Update()
    {
        if (!_sessionRunning || _finalComputed) return;
        if (Time.time < _nextSampleTime) return;

        _nextSampleTime = Time.time + sampleInterval;

        float p = presenterTracker ? presenterTracker.PresenterScore01 : 0f;
        float a = audienceEngagement ? audienceEngagement.Engagement01 : 0f;

        _presenterSum += p;
        _audienceSum += a;
        _samples++;

    }

    private void OnQuestionnaireShown()
    {
        if (_finalComputed) return;

        _sessionRunning = false;
        _finalComputed = true;

        SessionDurationSec = Time.time - _sessionStartTime;

        AvgPresenter01 = (_samples > 0) ? (_presenterSum / _samples) : 0f;
        AvgAudience01 = (_samples > 0) ? (_audienceSum / _samples) : 0f;

        float wSum = wPresenter + wAudience;
        if (wSum < 0.0001f) wSum = 1f;

        FinalScore01 = Mathf.Clamp01((AvgPresenter01 * wPresenter + AvgAudience01 * wAudience) / wSum);

        // Show results on the in-world panel
        if (resultsPresenter)
            resultsPresenter.ShowResults(BuildResultsString());
        else
            Debug.LogWarning("SessionPerformanceScorer: resultsPresenter not assigned (can't show results on panel).");

        if (printBreakdownToConsole)
        {
            Debug.Log(
                $"=== SESSION SCORE (computed at questionnaire) ===\n" +
                $"FinalScore: {FinalScore01:F2}\n" +
                $"AvgPresenter: {AvgPresenter01:F2}\n" +
                $"AvgAudience: {AvgAudience01:F2}\n" +
                $"Samples: {_samples}\n" +
                $"Duration: {SessionDurationSec:F1}s\n" +
                $"SpotlightTime (approx): {_spotlightTime:F1}s"
            );
        }
    }

    private string BuildResultsString()
    {
        int finalPct = Mathf.RoundToInt(FinalScore01 * 100f);
        int presPct = Mathf.RoundToInt(AvgPresenter01 * 100f);
        int audPct = Mathf.RoundToInt(AvgAudience01 * 100f);

        int total = audienceEngagement ? audienceEngagement.TotalCount : 0;
        int e = audienceEngagement ? audienceEngagement.EngagedCount : 0;
        int c = audienceEngagement ? audienceEngagement.ConfusedCount : 0;
        int d = audienceEngagement ? audienceEngagement.DisengagedCount : 0;

        float eP = (total > 0) ? (100f * e / total) : 0f;
        float cP = (total > 0) ? (100f * c / total) : 0f;
        float dP = (total > 0) ? (100f * d / total) : 0f;

        int tF = audienceEngagement ? audienceEngagement.TeacherFocusedCount : 0;
        int bF = audienceEngagement ? audienceEngagement.BoardFocusedCount : 0;

        return
            $"<b>Session Results</b>\n\n" +
            $"Overall: <b>{finalPct}%</b>\n\n" +
            $"Presenter Activity: {presPct}%\n" +
            $"Audience Engagement: {audPct}%\n\n" +
            $"<b>Audience Lenses</b>\n" +
            $"Engaged: {e} ({eP:0}%)\n" +
            $"Confused: {c} ({cP:0}%)\n" +
            $"Disengaged: {d} ({dP:0}%)\n\n" +
            $"Teacher Focus (dwell): {tF}\n" +
            $"Board Focus (dwell): {bF}\n\n" +
            $"Duration: {SessionDurationSec:F1}s";
    }

}
