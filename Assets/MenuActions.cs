using System.Collections.Generic;
using UnityEngine;

public class MenuActions : MonoBehaviour
{
    [Header("Spotlight mode")]
    [SerializeField] private Light spotlight3DUI;

    [Tooltip("Optional: Assign specific lights you want to control. If empty, script will auto-find all lights in the scene (once).")]
    [SerializeField] private List<Light> otherLights = new List<Light>();

    [Header("Spotlight Targeting")]
    [Tooltip("Player head / camera (CenterEyeAnchor is a good choice).")]
    [SerializeField] private Transform playerHead;

    [Tooltip("Optional: when following player, aim the spotlight at this target (e.g., Whiteboard). If null, aim forward from player head.")]
    [SerializeField] private Transform followLookTarget;

    [Tooltip("Offset from the player head when following (in playerHead local space). Example: (0, -0.1, 0.2) puts it slightly in front/down.")]
    [SerializeField] private Vector3 followLocalOffset = new Vector3(0f, -0.1f, 0.25f);

    [Tooltip("How quickly the spotlight follows the player (higher = snappier).")]
    [SerializeField] private float followPositionLerp = 12f;

    [Tooltip("How quickly the spotlight rotates to its target (higher = snappier).")]
    [SerializeField] private float followRotationLerp = 12f;

    [Header("Spotlight Direction")]
    [Tooltip("Flip the direction the spotlight is aiming (useful if the cone points the wrong way).")]
    [SerializeField] private bool reverseSpotlightDirection = false;

    [Header("Follow Spotlight Tuning")]
    [Tooltip("Increase cone size (spot angle) when target mode is FollowPlayer. 1.0 = unchanged.")]
    [SerializeField] private float followSpotAngleMultiplier = 1.35f;

    [Tooltip("Dim the follow spotlight intensity a bit. 1.0 = unchanged.")]
    [SerializeField] private float followIntensityMultiplier = 0.80f;

    [Tooltip("Optional: also scale range when following. 1.0 = unchanged.")]
    [SerializeField] private float followRangeMultiplier = 1.00f;

    [Header("Board Spotlight (separate light)")]
    [Tooltip("If you have a separate light that normally lights the board, assign it here so it can be turned OFF when spotlight is used.")]
    [SerializeField] private Light boardSpotlight;

    [Header("Startup")]
    [SerializeField] private bool startWithSpotlightModeOn = false;


    // Runtime
    private bool spotlightModeOn = false;
    private bool initialized = false;

    // Exposes
    public bool IsSpotlightModeOn => spotlightModeOn;
    public Transform SpotlightTransform => spotlight3DUI ? spotlight3DUI.transform : null;
    public bool IsFollowPlayerMode => targetMode == SpotlightTargetMode.FollowPlayer;
    public bool ReverseSpotlightDirection => reverseSpotlightDirection;



    // Board pose is captured once so you can always return to it.
    private Vector3 boardPos;
    private Quaternion boardRot;

    private enum SpotlightTargetMode { Board, FollowPlayer }
    [SerializeField] private SpotlightTargetMode targetMode = SpotlightTargetMode.Board;

    // Cache original spotlight settings so we can safely “tune” them and revert
    private float _baseIntensity;
    private float _baseSpotAngle;
    private float _baseInnerSpotAngle;
    private float _baseRange;

    private void Start()
    {
        InitializeIfNeeded();

        // Force a deterministic initial state
        spotlightModeOn = startWithSpotlightModeOn;

        // Apply lights based on spotlightModeOn
        foreach (var l in otherLights)
        {
            if (!l) continue;
            l.enabled = !spotlightModeOn;
        }

        if (spotlight3DUI)
            spotlight3DUI.gameObject.SetActive(spotlightModeOn);

        // Only position/aim spotlight when spotlight is ON
        if (spotlightModeOn)
            ApplyTargetModeImmediate();
    }


    private void Awake()
    {
        InitializeIfNeeded();

        if (spotlight3DUI)
        {
            boardPos = spotlight3DUI.transform.position;
            boardRot = spotlight3DUI.transform.rotation;

            _baseIntensity = spotlight3DUI.intensity;
            _baseSpotAngle = spotlight3DUI.spotAngle;
            _baseInnerSpotAngle = spotlight3DUI.innerSpotAngle;
            _baseRange = spotlight3DUI.range;
        }

        // If spotlight is disabled in the scene initially, we still want “other lights off” behavior consistent.
        if (spotlightModeOn)
            ApplyLightingState();
        else
            ApplyBlackoutState(); // per your request: if spotlight is not on, keep everything else off too
    }

    private void LateUpdate()
    {
        if (!spotlightModeOn) return;
        if (!spotlight3DUI) return;

        // Keep tuning consistent while ON
        ApplyTuningForCurrentMode();

        // Only follow logic in FollowPlayer mode
        if (targetMode != SpotlightTargetMode.FollowPlayer) return;
        if (!playerHead) return;

        Vector3 desiredPos = playerHead.TransformPoint(followLocalOffset);

        Quaternion desiredRot;
        if (followLookTarget)
        {
            Vector3 dir = (followLookTarget.position - desiredPos);
            if (dir.sqrMagnitude < 0.0001f) dir = playerHead.forward;
            if (reverseSpotlightDirection) dir = -dir;

            desiredRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
        else
        {
            Vector3 fwd = playerHead.forward;
            if (reverseSpotlightDirection) fwd = -fwd;

            desiredRot = Quaternion.LookRotation(fwd, Vector3.up);
        }

        Transform t = spotlight3DUI.transform;
        t.position = Vector3.Lerp(t.position, desiredPos, Time.deltaTime * followPositionLerp);
        t.rotation = Quaternion.Slerp(t.rotation, desiredRot, Time.deltaTime * followRotationLerp);
    }

    // Call from a test button / UI
    public void ToggleReverseSpotlightDirection()
    {
        reverseSpotlightDirection = !reverseSpotlightDirection;

        if (spotlightModeOn)
            ApplyTargetModeImmediate(); // updates rotation instantly

        Debug.Log($"ReverseSpotlightDirection = {reverseSpotlightDirection}");
    }

    public void ToggleSpotlightMode()
    {
        InitializeIfNeeded();

        spotlightModeOn = !spotlightModeOn;

        if (spotlightModeOn)
        {
            // Spotlight ON => everything else OFF + spotlight ON
            ApplyLightingState();
            ApplyTargetModeImmediate();
            ApplyTuningForCurrentMode();
        }
        else
        {
            // Spotlight OFF => keep spotlight OFF + keep everything else OFF (blackout)
            ApplyBlackoutState();
        }

        Debug.Log($"SpotlightModeOn = {spotlightModeOn} | TargetMode = {targetMode}");
    }

    // Call this from any button to switch Board <-> FollowPlayer
    public void ToggleSpotlightTarget()
    {
        InitializeIfNeeded();

        targetMode = (targetMode == SpotlightTargetMode.Board)
            ? SpotlightTargetMode.FollowPlayer
            : SpotlightTargetMode.Board;

        // IMPORTANT CHANGE:
        // Switching target should work even if spotlight is currently OFF.
        // When switching target, we force spotlight mode ON (and turn everything else OFF).
        ForceSpotlightOn();

        ApplyTargetModeImmediate();
        ApplyTuningForCurrentMode();

        Debug.Log($"Spotlight TargetMode switched to: {targetMode} (Spotlight forced ON)");
    }

    // Optional explicit methods (nice for UnityEvents dropdown)
    public void SetSpotlightToBoard()
    {
        InitializeIfNeeded();
        targetMode = SpotlightTargetMode.Board;

        ForceSpotlightOn();
        ApplyTargetModeImmediate();
        ApplyTuningForCurrentMode();
    }

    public void SetSpotlightToFollowPlayer()
    {
        InitializeIfNeeded();
        targetMode = SpotlightTargetMode.FollowPlayer;

        ForceSpotlightOn();
        ApplyTargetModeImmediate();
        ApplyTuningForCurrentMode();
    }

    private void ForceSpotlightOn()
    {
        if (spotlightModeOn) return;

        spotlightModeOn = true;
        ApplyLightingState();
    }

    /// <summary>
    /// Spotlight ON state:
    /// - All other lights OFF (including boardSpotlight)
    /// - spotlight3DUI ON
    /// </summary>
    private void ApplyLightingState()
    {
        // Turn other lights OFF
        foreach (var l in otherLights)
        {
            if (!l) continue;
            l.enabled = false;
        }

        // Turn board spotlight OFF too (even if it isn't in otherLights)
        if (boardSpotlight) boardSpotlight.enabled = false;

        // Turn spotlight ON
        if (spotlight3DUI) spotlight3DUI.gameObject.SetActive(true);
    }

    /// <summary>
    /// Spotlight OFF state (your requested behavior):
    /// - spotlight3DUI OFF
    /// - all other lights OFF
    /// </summary>
    private void ApplyBlackoutState()
    {
        // Spotlight OFF
        if (spotlight3DUI) spotlight3DUI.gameObject.SetActive(false);

        // Keep everything else OFF as well
        foreach (var l in otherLights)
        {
            if (!l) continue;
            l.enabled = false;
        }

        if (boardSpotlight) boardSpotlight.enabled = false;
    }

    private void ApplyTargetModeImmediate()
    {
        if (!spotlight3DUI) return;

        if (targetMode == SpotlightTargetMode.Board)
        {
            spotlight3DUI.transform.SetPositionAndRotation(boardPos, boardRot);
        }
        else
        {
            if (!playerHead) return;

            Vector3 p = playerHead.TransformPoint(followLocalOffset);
            spotlight3DUI.transform.position = p;

            if (followLookTarget)
            {
                Vector3 dir = (followLookTarget.position - p);
                if (dir.sqrMagnitude < 0.0001f) dir = playerHead.forward;
                if (reverseSpotlightDirection) dir = -dir;

                spotlight3DUI.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
            else
            {
                Vector3 fwd = playerHead.forward;
                if (reverseSpotlightDirection) fwd = -fwd;

                spotlight3DUI.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            }
        }
    }

    private void ApplyTuningForCurrentMode()
    {
        if (!spotlight3DUI) return;

        // Always revert to baseline first
        spotlight3DUI.intensity = _baseIntensity;
        spotlight3DUI.spotAngle = _baseSpotAngle;
        spotlight3DUI.innerSpotAngle = _baseInnerSpotAngle;
        spotlight3DUI.range = _baseRange;

        if (targetMode == SpotlightTargetMode.FollowPlayer)
        {
            spotlight3DUI.spotAngle = _baseSpotAngle * followSpotAngleMultiplier;

            float innerCandidate = _baseInnerSpotAngle * followSpotAngleMultiplier;
            spotlight3DUI.innerSpotAngle = Mathf.Min(spotlight3DUI.spotAngle - 0.1f, innerCandidate);

            spotlight3DUI.intensity = _baseIntensity * followIntensityMultiplier;
            spotlight3DUI.range = _baseRange * followRangeMultiplier;
        }
    }

    private void InitializeIfNeeded()
    {
        if (initialized) return;
        initialized = true;

        if (!spotlight3DUI)
            Debug.LogWarning("Spotlight3DUI reference is missing!");

        // If you didn't manually assign other lights, auto-find them
        if (otherLights == null || otherLights.Count == 0)
        {
            var allLights = FindObjectsOfType<Light>(true); // include inactive
            otherLights = new List<Light>();

            foreach (var l in allLights)
            {
                if (!l) continue;
                if (spotlight3DUI && l == spotlight3DUI) continue; // skip spotlight3DUI
                otherLights.Add(l);
            }
        }

        // Make sure spotlight isn't accidentally included in otherLights
        otherLights.RemoveAll(l => l == null || (spotlight3DUI && l == spotlight3DUI));

        // Also ensure boardSpotlight isn't accidentally in otherLights (we manage it explicitly)
        if (boardSpotlight)
            otherLights.RemoveAll(l => l == boardSpotlight);
    }
}
