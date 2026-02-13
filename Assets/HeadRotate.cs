using UnityEngine;

public class HeadRotate : MonoBehaviour
{
    [Header("References")]
    public Transform HeadObject;
    public Transform TargetObject;
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

    [SerializeField] private bool forcedActive = false;     // debug-visible
    public bool ForcedActive => forcedActive;

    public bool IsActiveEffective => isActive || forcedActive;

    [SerializeField] private bool isInRange = false;        // debug-visible
    public bool IsInRange => isInRange;

    // focused = effective active + in range
    public bool IsFocusedNow => IsActiveEffective && isInRange;

    private bool isLooking = false;

    private Quaternion lookRotation; // smoothed rotation while tracking
    private Quaternion restRotation; // rotation to return to when target out of range
    private float returnTimer = 0f;

    void Start()
    {
        if (HeadObject)
        {
            lookRotation = HeadObject.rotation;
            restRotation = HeadObject.rotation;
        }
    }

    void LateUpdate()
    {
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

        // Signed angle between direction-to-target and head-forward
        float angle = Vector3.SignedAngle(dir, HeafForward.forward, HeafForward.up);

        bool inRange = (angle < MaxAngle && angle > MinAngle);
        isInRange = inRange;

        if (inRange)
        {
            // First time start looking:
            if (!isLooking)
            {
                restRotation = HeadObject.rotation;
                lookRotation = HeadObject.rotation;
                isLooking = true;
            }

            Quaternion targetRot = Quaternion.LookRotation(TargetObject.position - HeadObject.position);
            lookRotation = Quaternion.Slerp(lookRotation, targetRot, Time.deltaTime * RotateSpeed);
            HeadObject.rotation = lookRotation;

            returnTimer = returnDuration; // reset return window
        }
        else if (isLooking)
        {
            // Smooth return to the stored rest rotation
            returnTimer -= Time.deltaTime;

            float t = (returnDuration <= 0.0001f) ? 1f : (1f - (returnTimer / returnDuration));
            t = Mathf.Clamp01(t);

            HeadObject.rotation = Quaternion.Slerp(lookRotation, restRotation, t);

            // When finished returning, stop "looking"
            if (returnTimer <= 0f)
            {
                HeadObject.rotation = restRotation;
                isLooking = false;
            }
        }
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

        // If force turned off and normal is also off -> fully reset
        if (!IsActiveEffective)
        {
            ResetRuntimeState();
        }
        else
        {
            // Clean re-entry from current pose to avoid snapping
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

        if (HeadObject)
        {
            lookRotation = HeadObject.rotation;
            restRotation = HeadObject.rotation;
        }
    }

    public void EnableHeadRotate() => SetActive(true);
    public void DisableHeadRotate() => SetActive(false);
}
