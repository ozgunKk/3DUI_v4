using System.Collections.Generic;
using UnityEngine;

public class MenuActions : MonoBehaviour
{
    public enum SpotlightMode { None, Board, Target }

    [Header("Normal Scene Lights (everything except spotlights)")]
    [Tooltip("If empty, script will auto-find all lights in scene (except the 2 spotlights).")]
    [SerializeField] private List<Light> otherLights = new List<Light>();

    [Header("Target Spotlight (Follow Player)")]
    [SerializeField] private Light targetSpotlight;     // Spotlight3DUI
    [SerializeField] private Transform playerHead;      // CenterEyeAnchor (recommended)
    [SerializeField] private Transform followLookTarget; // optional (e.g. board center). If null => use head forward
    [SerializeField] private Vector3 followLocalOffset = new Vector3(0f, -0.10f, 0.25f);
    [SerializeField] private float followPositionLerp = 12f;
    [SerializeField] private float followRotationLerp = 12f;

    [Header("Target Spotlight Direction")]
    [SerializeField] private bool reverseSpotlightDirection = false;

    [Header("Target Spotlight Tuning (only in Target mode)")]
    [SerializeField] private float followSpotAngleMultiplier = 1.35f;
    [SerializeField] private float followIntensityMultiplier = 0.80f;
    [SerializeField] private float followRangeMultiplier = 1.00f;

    [Header("Board Spotlight (separate light)")]
    [SerializeField] private Light boardSpotlight;      // Spotlight3DUI_Board

    [Header("Startup")]
    [SerializeField] private SpotlightMode startMode = SpotlightMode.None;
    [SerializeField] private bool autoFindOtherLights = true;

    [Header("Debug")]
    [SerializeField] private bool logState = false;

    [SerializeField] private SpotlightMode mode = SpotlightMode.None;

    // Baseline tuning cache (Target spotlight)
    private float _baseIntensity;
    private float _baseSpotAngle;
    private float _baseInnerSpotAngle;
    private float _baseRange;

    private bool _initialized;

    // Exposed for SpotlightFocusEnforcer
    public bool IsSpotlightModeOn => mode != SpotlightMode.None;
    public bool IsFollowPlayerMode => mode == SpotlightMode.Target;
    public Transform SpotlightTransform => targetSpotlight ? targetSpotlight.transform : null;
    public bool ReverseSpotlightDirection => reverseSpotlightDirection;

    private void Awake()
    {
        InitializeIfNeeded();

        // Cache baseline target spotlight tuning
        if (targetSpotlight)
        {
            _baseIntensity = targetSpotlight.intensity;
            _baseSpotAngle = targetSpotlight.spotAngle;
            _baseInnerSpotAngle = targetSpotlight.innerSpotAngle;
            _baseRange = targetSpotlight.range;
        }

        SetMode(startMode, immediate: true);
    }

    private void LateUpdate()
    {
        if (mode != SpotlightMode.Target) return;
        if (!targetSpotlight || !playerHead) return;

        ApplyTargetTuning();

        // Follow player position
        Vector3 desiredPos = playerHead.TransformPoint(followLocalOffset);

        // Aim
        Vector3 dir;
        if (followLookTarget)
            dir = (followLookTarget.position - desiredPos);
        else
            dir = playerHead.forward;

        if (dir.sqrMagnitude < 0.0001f)
            dir = playerHead.forward;

        if (reverseSpotlightDirection)
            dir = -dir;

        Quaternion desiredRot = Quaternion.LookRotation(dir.normalized, Vector3.up);

        Transform t = targetSpotlight.transform;
        t.position = Vector3.Lerp(t.position, desiredPos, Time.deltaTime * followPositionLerp);
        t.rotation = Quaternion.Slerp(t.rotation, desiredRot, Time.deltaTime * followRotationLerp);
    }

    // -------------------------
    // Buttons
    // -------------------------

    // Hook this to Button-SpotlightBoard
    public void ToggleBoardSpotlight()
    {
        SetMode(mode == SpotlightMode.Board ? SpotlightMode.None : SpotlightMode.Board, immediate: true);
    }

    // Hook this to Button-SpotlightTarget
    public void ToggleTargetSpotlight()
    {
        SetMode(mode == SpotlightMode.Target ? SpotlightMode.None : SpotlightMode.Target, immediate: true);
    }

    public void ToggleReverseSpotlightDirection()
    {
        reverseSpotlightDirection = !reverseSpotlightDirection;
        if (logState) Debug.Log($"ReverseSpotlightDirection={reverseSpotlightDirection}");
    }

    // Optional explicit setters
    public void SetSpotlightToBoard() => SetMode(SpotlightMode.Board, immediate: true);
    public void SetSpotlightToFollowPlayer() => SetMode(SpotlightMode.Target, immediate: true);
    public void DisableSpotlights() => SetMode(SpotlightMode.None, immediate: true);

    // -------------------------
    // Core state machine
    // -------------------------

    private void SetMode(SpotlightMode newMode, bool immediate)
    {
        InitializeIfNeeded();
        mode = newMode;

        // First: default everything OFF
        if (targetSpotlight) targetSpotlight.gameObject.SetActive(false);
        if (boardSpotlight) boardSpotlight.gameObject.SetActive(false);

        // Restore baseline target tuning whenever we disable or switch
        RestoreTargetBaseline();

        if (mode == SpotlightMode.None)
        {
            SetOtherLightsEnabled(true);
        }
        else if (mode == SpotlightMode.Board)
        {
            SetOtherLightsEnabled(false);
            if (boardSpotlight) boardSpotlight.gameObject.SetActive(true);
        }
        else // Target
        {
            SetOtherLightsEnabled(false);
            if (targetSpotlight) targetSpotlight.gameObject.SetActive(true);

            // Snap once so you instantly see the cone (no “it’s on but somewhere else”)
            if (immediate && playerHead && targetSpotlight)
            {
                Vector3 p = playerHead.TransformPoint(followLocalOffset);
                targetSpotlight.transform.position = p;

                Vector3 dir;
                if (followLookTarget) dir = (followLookTarget.position - p);
                else dir = playerHead.forward;

                if (dir.sqrMagnitude < 0.0001f) dir = playerHead.forward;
                if (reverseSpotlightDirection) dir = -dir;

                targetSpotlight.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }

            ApplyTargetTuning();
        }

        if (logState)
            Debug.Log($"MenuActions mode={mode} | otherLights={otherLights.Count} | target={(targetSpotlight ? "OK" : "NULL")} | board={(boardSpotlight ? "OK" : "NULL")}");
    }

    private void SetOtherLightsEnabled(bool enabled)
    {
        for (int i = 0; i < otherLights.Count; i++)
        {
            var l = otherLights[i];
            if (!l) continue;
            l.enabled = enabled;
        }
    }

    private void ApplyTargetTuning()
    {
        if (!targetSpotlight) return;

        targetSpotlight.spotAngle = _baseSpotAngle * followSpotAngleMultiplier;

        float innerCandidate = _baseInnerSpotAngle * followSpotAngleMultiplier;
        targetSpotlight.innerSpotAngle = Mathf.Min(targetSpotlight.spotAngle - 0.1f, innerCandidate);

        targetSpotlight.intensity = _baseIntensity * followIntensityMultiplier;
        targetSpotlight.range = _baseRange * followRangeMultiplier;
    }

    private void RestoreTargetBaseline()
    {
        if (!targetSpotlight) return;

        // If Awake hasn't cached yet, don't overwrite
        if (_baseRange <= 0f && _baseSpotAngle <= 0f) return;

        targetSpotlight.intensity = _baseIntensity;
        targetSpotlight.spotAngle = _baseSpotAngle;
        targetSpotlight.innerSpotAngle = _baseInnerSpotAngle;
        targetSpotlight.range = _baseRange;
    }

    private void InitializeIfNeeded()
    {
        if (_initialized) return;
        _initialized = true;

        // Auto-find other scene lights (recommended so “Other Lights = 0” can’t break behavior)
        if (autoFindOtherLights && (otherLights == null || otherLights.Count == 0))
        {
#if UNITY_2023_1_OR_NEWER
            var allLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
#else
            var allLights = FindObjectsOfType<Light>(true);
#endif
            otherLights = new List<Light>(allLights.Length);
            foreach (var l in allLights)
            {
                if (!l) continue;
                if (targetSpotlight && l == targetSpotlight) continue;
                if (boardSpotlight && l == boardSpotlight) continue;
                otherLights.Add(l);
            }
        }

        // Clean nulls
        if (otherLights == null) otherLights = new List<Light>();
        otherLights.RemoveAll(l => l == null);
    }
}
