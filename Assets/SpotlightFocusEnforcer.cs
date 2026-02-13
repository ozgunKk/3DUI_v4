using System.Collections.Generic;
using UnityEngine;

public class SpotlightFocusEnforcer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MenuActions menuActions;
    [SerializeField] private Transform charactersRoot; 

    [Header("Raycast")]
    [SerializeField] private float maxDistance = 8f;
    [SerializeField] private LayerMask characterLayerMask = ~0; 

    [Tooltip("How often to check spotlight hit (seconds).")]
    [SerializeField] private float tickInterval = 0.05f;

    [Header("Debug")]
    [SerializeField] private bool debugDrawRay = true;

    private float _nextTickTime;

    private HeadRotate _currentForced;
    private bool _prevForcedWasActive; // previous normal state (isActive)
    private readonly Dictionary<HeadRotate, bool> _savedStates = new();

    private void Update()
    {
        if (!menuActions || !charactersRoot) return;
        if (Time.time < _nextTickTime) return;
        _nextTickTime = Time.time + tickInterval;

        // Only enforce in FollowPlayer mode while spotlight is ON
        if (!menuActions.IsSpotlightModeOn || !menuActions.IsFollowPlayerMode)
        {
            ClearForcedIfAny();
            return;
        }

        // We raycast from spotlight position forward
        var spotT = menuActions.SpotlightTransform;
        if (!spotT)
        {
            ClearForcedIfAny();
            return;
        }

        Vector3 origin = spotT.position;
        Vector3 dir = menuActions.ReverseSpotlightDirection ? -spotT.forward : spotT.forward;

        if (debugDrawRay)
            Debug.DrawRay(origin, dir * maxDistance, Color.yellow, tickInterval);

        if (Physics.Raycast(origin, dir, out RaycastHit hit, maxDistance, characterLayerMask, QueryTriggerInteraction.Ignore))
        {
            var hr = hit.collider.GetComponentInParent<HeadRotate>();
            if (hr && hr.transform.IsChildOf(charactersRoot))
            {
                ForceThisCharacter(hr);
                return;
            }
        }

        ClearForcedIfAny();
    }

    private void ForceThisCharacter(HeadRotate hr)
    {
        if (_currentForced == hr) return;

        // Clear old one
        ClearForcedIfAny();

        // Save previous normal state (isActive) once
        if (!_savedStates.ContainsKey(hr))
            _savedStates[hr] = hr.IsActive; // normal state only

        _currentForced = hr;

        // Force active
        hr.ForceActive(true);
    }

    private void ClearForcedIfAny()
    {
        if (!_currentForced) return;

        _currentForced.ForceActive(false);

        _currentForced = null;
    }
}
