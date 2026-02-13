using UnityEngine;

public class HeadRotate : MonoBehaviour
{
    [Header("References")]
    public Transform HeadObject;
    public Transform TargetObject;   // authored/default target (still used if natural attention off)
    public Transform HeafForward;

    [Header("Settings")]
    public float RotateSpeed = 7f;
    public float MaxAngle = 80f;
    public float MinAngle = -80f;

    [Tooltip("How long we keep blending back after leaving the allowed angle.")]
    public float returnDuration = 0.5f;

    [Header("Runtime")]
    [SerializeField] private bool isActive = true;          // normal / authored state
    public bool IsActive => isActive;

    [SerializeField] private bool forcedActive = false;     // spotlight override etc.
    public bool ForcedActive => forcedActive;

    public bool IsActiveEffective => isActive || forcedActive;

    [SerializeField] private bool isInRange = false;        // debug-visible
    public bool IsInRange => isInRange;

    public bool IsFocusedNow => IsActiveEffective && isInRange;

    [Header("Natural Attention Mode (Presenter vs Board)")]
    [Tooltip("If enabled, when active the character will pick either presenter or board and STAY there for a while.")]
    [SerializeField] private bool enableNaturalAttention = true;

    [Tooltip("Presenter target (teacher head / LeftEyeAnchor). Required if natural attention is enabled.")]
    [SerializeField] private Transform presenterTarget;

    [Tooltip("Board target (empty transform at board center). Required if natural attention is enabled.")]
    [SerializeField] private Transform boardTarget;

    [Tooltip("How long the character stays looking at the chosen target before possibly switching again.")]
    [SerializeField] private Vector2 modeHoldRange = new Vector2(6f, 18f);

    [Tooltip("Chance to choose BOARD when selecting a new mode (0..1). Otherwise chooses PRESENTER.")]
    [Range(0f, 1f)]
    [SerializeField] private float boardModeChance = 0.35f;

    [Tooltip("If true, natural attention switching pauses while forcedActive (spotlight override).")]
    [SerializeField] private bool pauseSwitchingWhileForced = true;

    [Header("Debug")]
    [SerializeField] private bool logModeSwitches = false;

    private bool isLooking = false;

    private Quaternion lookRotation; // smoothed rotation while tracking
    private Quaternion restRotation; // rotation to return to when target out of range
    private float returnTimer = 0f;

    // Natural attention state
    private enum AttentionMode { Presenter, Board }
    [SerializeField] private AttentionMode currentMode = AttentionMode.Presenter;

    private Transform _authoredTarget;
    private float _modeUntilTime;

    private void Start()
    {
        if (HeadObject)
        {
            lookRotation = HeadObject.rotation;
            restRotation = HeadObject.rotation;
        }

        _authoredTarget = TargetObject;

        // Start with a stable mode duration so it doesn’t flicker at scene start
        PickNewMode(force: true);
    }

    private void LateUpdate()
    {
        UpdateNaturalAttentionMode();

        if (!IsActiveEffective)
        {
            isInRange = false;
            return;
        }

        if (!HeadObject || !TargetObject || !HeafForward)
        {
            isInRange = false;
            return;
        }

        Vector3 dir = (TargetObject.position - HeadObject.position).normalized;

        float angle = Vector3.SignedAngle(dir, HeafForward.forward, HeafForward.up);

        bool inRange = (angle < MaxAngle && angle > MinAngle);
        isInRange = inRange;

        if (inRange)
        {
            if (!isLooking)
            {
                restRotation = HeadObject.rotation;
                lookRotation = HeadObject.rotation;
                isLooking = true;
            }

            Quaternion targetRot = Quaternion.LookRotation(TargetObject.position - HeadObject.position);
            lookRotation = Quaternion.Slerp(lookRotation, targetRot, Time.deltaTime * RotateSpeed);
            HeadObject.rotation = lookRotation;

            returnTimer = returnDuration;
        }
        else if (isLooking)
        {
            returnTimer -= Time.deltaTime;

            float t = (returnDuration <= 0.0001f) ? 1f : (1f - (returnTimer / returnDuration));
            t = Mathf.Clamp01(t);

            HeadObject.rotation = Quaternion.Slerp(lookRotation, restRotation, t);

            if (returnTimer <= 0f)
            {
                HeadObject.rotation = restRotation;
                isLooking = false;
            }
        }
    }

    private void UpdateNaturalAttentionMode()
    {
        if (!enableNaturalAttention) return;
        if (!IsActiveEffective) return;

        if (pauseSwitchingWhileForced && forcedActive) return;

        if (!presenterTarget || !boardTarget) return;

        // Keep current mode until time expires
        if (Time.time < _modeUntilTime) return;

        PickNewMode(force: false);
    }

    private void PickNewMode(bool force)
    {
        if (!presenterTarget || !boardTarget)
            return;

        // Keep authored target if user disables natural mode later
        _authoredTarget = TargetObject;

        // Choose next mode
        bool chooseBoard = Random.value < boardModeChance;

        // If force==true, don’t re-roll a bunch; just accept currentMode initial value if you want:
        // But simplest: always pick randomly even on start.
        currentMode = chooseBoard ? AttentionMode.Board : AttentionMode.Presenter;

        TargetObject = (currentMode == AttentionMode.Board) ? boardTarget : presenterTarget;

        float hold = Random.Range(modeHoldRange.x, modeHoldRange.y);
        _modeUntilTime = Time.time + hold;

        if (logModeSwitches)
            Debug.Log($"{name} attention mode -> {currentMode} for {hold:0.0}s");
    }

    public void SetActive(bool active)
    {
        isActive = active;

        if (!IsActiveEffective)
        {
            ResetRuntimeState();
            return;
        }

        if (HeadObject)
        {
            lookRotation = HeadObject.rotation;
            restRotation = HeadObject.rotation;
        }
    }

    public void ForceActive(bool force)
    {
        forcedActive = force;

        if (!IsActiveEffective)
        {
            ResetRuntimeState();
        }
        else
        {
            if (HeadObject)
            {
                lookRotation = HeadObject.rotation;
                restRotation = HeadObject.rotation;
            }
        }
    }

    private void ResetRuntimeState()
    {
        isInRange = false;
        isLooking = false;
        returnTimer = 0f;

        // Restore authored target when disabled
        if (enableNaturalAttention && _authoredTarget)
            TargetObject = _authoredTarget;

        if (HeadObject)
        {
            lookRotation = HeadObject.rotation;
            restRotation = HeadObject.rotation;
        }

        // Restart mode timing cleanly
        _modeUntilTime = 0f;
    }

    public void EnableHeadRotate() => SetActive(true);
    public void DisableHeadRotate() => SetActive(false);
}
