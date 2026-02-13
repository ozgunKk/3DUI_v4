using UnityEngine;

public class HammerEngagementDriver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AudienceEngagementManager engagementManager;
    [SerializeField] private HammerVFXStages hammerVFX;

    [Header("Thresholds (Engagement 0..1)")]
    [Tooltip("If engagement falls below this, go to HIGH sparks.")]
    [Range(0f, 1f)][SerializeField] private float highSparksBelow = 0.35f;

    [Tooltip("If engagement falls below this, go to LOW sparks (but above high threshold).")]
    [Range(0f, 1f)][SerializeField] private float lowSparksBelow = 0.60f;

    [Header("Stability")]
    [Tooltip("Prevents flicker around thresholds.")]
    [Range(0f, 0.2f)][SerializeField] private float hysteresis = 0.05f;

    [Tooltip("Minimum time to hold a stage after switching.")]
    [SerializeField] private float minHoldSeconds = 1.0f;

    private HammerVFXStages.Stage _current = HammerVFXStages.Stage.None;
    private float _lockUntil;

    private void Reset()
    {
        hammerVFX = GetComponent<HammerVFXStages>();
    }

    private void Update()
    {
        if (!engagementManager || !hammerVFX) return;
        if (Time.time < _lockUntil) return;

        float e = engagementManager.Engagement01;

        var desired = _current;

        // Low engagement => more sparks
        if (e < (highSparksBelow - hysteresis))
            desired = HammerVFXStages.Stage.High;
        else if (e < (lowSparksBelow - hysteresis))
            desired = HammerVFXStages.Stage.Low;
        else if (e > (lowSparksBelow + hysteresis))
            desired = HammerVFXStages.Stage.None;

        if (desired != _current)
        {
            hammerVFX.SetStage(desired);
            _current = desired;
            _lockUntil = Time.time + minHoldSeconds;
        }
    }
}
