using UnityEngine;

public class AudienceBehaviorAgent : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PresenterActivityTracker presenter;
    [SerializeField] private HeadRotate headRotate;
    [SerializeField] private CharacterAnimationSwitcher animSwitcher;

    [Header("Behavior Profile")]
    [Tooltip("Minimum presenter score needed before this character tends to focus.")]
    [Range(0f, 1f)][SerializeField] private float focusThreshold = 0.70f;

    [Tooltip("How strongly this character reacts to presenter score above threshold (0=weak, 1=strong).")]
    [Range(0f, 1f)][SerializeField] private float responsiveness = 0.75f;

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

    // internal state
    private bool _desiredFocus = true;         
    private float _nextDecisionTime = 0f;
    private float _stateLockedUntil = 0f;

    private void Reset()
    {
        headRotate = GetComponentInChildren<HeadRotate>(true);
        animSwitcher = GetComponentInChildren<CharacterAnimationSwitcher>(true);
    }

    private void Awake()
    {
        if (!headRotate) headRotate = GetComponentInChildren<HeadRotate>(true);
        if (!animSwitcher) animSwitcher = GetComponentInChildren<CharacterAnimationSwitcher>(true);

        ScheduleNextDecision(0.5f);
    }

    private void Update()
    {
        if (!presenter || !headRotate) return;

        // Spotlight override ALWAYS wins: if forcedActive, don't change anything.
        if (headRotate.ForcedActive)
            return;

        if (Time.time < _nextDecisionTime)
            return;

        if (Time.time < _stateLockedUntil)
        {
            ScheduleNextDecision();
            return;
        }

        float score = presenter.PresenterScore01;

        
        float baseP = Mathf.InverseLerp(focusThreshold, 1f, score); // 0..1
        baseP = Mathf.Clamp01(baseP);

        
        float shapedP = Mathf.Lerp(baseP, baseP * baseP, responsiveness);

        float noise = (Random.value - 0.5f) * 2f * randomness; 
        float pFocus = Mathf.Clamp01(shapedP + noise);

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
                // unfocused: maybe start writing
                bool write = Random.value < writeChanceWhenUnfocused;
                animSwitcher.SetWritingMode(write);
            }
            else
            {
                // focused: typically not writing
                animSwitcher.SetWritingMode(false);
            }
        }

        if (logDecisions)
        {
            Debug.Log($"{name} decision => Focus={_desiredFocus} | score={score:F2} | pFocus={pFocus:F2}");
        }
    }

    private void ScheduleNextDecision(float extraDelay = 0f)
    {
        float dt = Random.Range(decisionIntervalRange.x, decisionIntervalRange.y);
        _nextDecisionTime = Time.time + dt + extraDelay;
    }
}
