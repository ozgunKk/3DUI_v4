using UnityEngine;

public class AudienceBehaviorAgent : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PresenterActivityTracker presenter;
    [SerializeField] private HeadRotate headRotate;
    [SerializeField] private CharacterAnimationSwitcher animSwitcher;

    [Header("Behavior Profile")]
    [Range(0f, 1f)][SerializeField] private float focusThreshold = 0.70f;

    [Tooltip("Adds personality randomness. Higher = more unpredictable.")]
    [Range(0f, 1f)][SerializeField] private float randomness = 0.20f;

    [Header("Timing")]
    [Tooltip("How often this character re-evaluates attention state.")]
    [SerializeField] private Vector2 decisionIntervalRange = new Vector2(2.5f, 5.0f);

    [Tooltip("Minimum time to stay in a chosen state before changing again.")]
    [SerializeField] private float minStateHoldSeconds = 2.0f;

    [Header("Optional: animation behavior")]
    [Tooltip("If true, when unfocused the character may switch to writing.")]
    [SerializeField] private bool allowWritingWhenUnfocused = true;

    [Tooltip("Probability to start writing when unfocused (0..1).")]
    [Range(0f, 1f)][SerializeField] private float writeChanceWhenUnfocused = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool logDecisions = false;

    [Tooltip("How often to print logs (seconds).")]
    [SerializeField] private float logIntervalSeconds = 3f;

    // internal state
    private bool _desiredFocus = true;
    private float _nextDecisionTime = 0f;
    private float _stateLockedUntil = 0f;

    private float _nextLogTime = 0f;

    private void Reset()
    {
        headRotate = GetComponentInChildren<HeadRotate>(true);
        animSwitcher = GetComponentInChildren<CharacterAnimationSwitcher>(true);
    }

    private void Awake()
    {
        if (!headRotate) headRotate = GetComponentInChildren<HeadRotate>(true);
        if (!animSwitcher) animSwitcher = GetComponentInChildren<CharacterAnimationSwitcher>(true);

        _nextLogTime = Time.time + Mathf.Max(0.1f, logIntervalSeconds);
        ScheduleNextDecision(0.5f);
    }

    private void Update()
    {
        if (!presenter || !headRotate) return;

        // Spotlight override ALWAYS wins: if forcedActive, don't change anything.
        if (headRotate.ForcedActive)
        {
            ThrottledStatusLog();
            return;
        }

        // Periodic status log (does not change behavior)
        ThrottledStatusLog();

        if (Time.time < _nextDecisionTime)
            return;

        if (Time.time < _stateLockedUntil)
        {
            ScheduleNextDecision();
            return;
        }

        float score = presenter.PresenterScore01;

        // base probability driven by presenter score
        float baseP = Mathf.InverseLerp(focusThreshold, 1f, score);
        baseP = Mathf.Clamp01(baseP);

        // randomness: symmetric noise in [-randomness, +randomness]
        float noise = (Random.value - 0.5f) * 2f * randomness;

        // final probability of focusing
        float pFocus = Mathf.Clamp01(baseP + noise);

        bool newFocus = Random.value < pFocus;

        ApplyState(newFocus, score, pFocus);

        // lock state for a while so it doesn't flicker
        _stateLockedUntil = Time.time + minStateHoldSeconds;

        ScheduleNextDecision();
    }

    private void ApplyState(bool focus, float score, float pFocus)
    {
        if (focus == _desiredFocus) return;
        _desiredFocus = focus;

        // Focus state -> enable head rotate normal state
        headRotate.SetActive(_desiredFocus);

        // Optional animation reaction
        if (animSwitcher && allowWritingWhenUnfocused)
        {
            if (!_desiredFocus)
            {
                bool write = Random.value < writeChanceWhenUnfocused;
                animSwitcher.SetWritingMode(write);
            }
            else
            {
                animSwitcher.SetWritingMode(false);
            }
        }

        // Throttle decision logs (so it won't spam)
        if (logDecisions && Time.time >= _nextLogTime)
        {
            Debug.Log($"{name} decision => Focus={_desiredFocus} | score={score:F2} | pFocus={pFocus:F2}");
            _nextLogTime = Time.time + Mathf.Max(0.1f, logIntervalSeconds);
        }
    }

    private void ThrottledStatusLog()
    {
        if (!logDecisions) return;
        if (Time.time < _nextLogTime) return;

        float score = presenter ? presenter.PresenterScore01 : -1f;
        Debug.Log($"{name} status => Focus={_desiredFocus} | PresenterScore={score:F2} | Forced={headRotate.ForcedActive}");

        _nextLogTime = Time.time + Mathf.Max(0.1f, logIntervalSeconds);
    }

    private void ScheduleNextDecision(float extraDelay = 0f)
    {
        float dt = Random.Range(decisionIntervalRange.x, decisionIntervalRange.y);
        _nextDecisionTime = Time.time + dt + extraDelay;
    }
}
