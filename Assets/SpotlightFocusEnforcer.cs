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

    [Header("Stability")]
    [Tooltip("If the ray misses briefly, keep the last forced target for this long (seconds). Helps avoid flicker.")]
    [SerializeField] private float missGraceSeconds = 0.12f;

    [Header("Debug")]
    [SerializeField] private bool debugDrawRay = true;
    [SerializeField] private bool logStateChanges = false;

    private float _nextTickTime;
    private float _lastHitTime;

    private HeadRotate _currentForced;

    private void Update()
    {
        if (!menuActions || !charactersRoot) return;

        if (Time.time < _nextTickTime) return;
        _nextTickTime = Time.time + tickInterval;

        // Only enforce in Follow/Target mode while spotlight is ON
        if (!menuActions.IsSpotlightModeOn || !menuActions.IsFollowPlayerMode)
        {
            ClearForcedIfAny();
            return;
        }

        // Raycast from spotlight position forward
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

        HeadRotate hitHr = null;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, maxDistance, characterLayerMask, QueryTriggerInteraction.Ignore))
        {
            hitHr = hit.collider.GetComponentInParent<HeadRotate>();

            // Make sure it belongs to the audience root
            if (hitHr && !hitHr.transform.IsChildOf(charactersRoot))
                hitHr = null;
        }

        if (hitHr)
        {
            _lastHitTime = Time.time;
            ForceThisCharacter(hitHr);
            return;
        }

        // No hit: keep current forced for a short grace time to avoid flicker
        if (_currentForced && (Time.time - _lastHitTime) <= missGraceSeconds)
            return;

        ClearForcedIfAny();
    }

    private void ForceThisCharacter(HeadRotate hr)
    {
        if (!hr) return;
        if (_currentForced == hr) return;

        ClearForcedIfAny();

        _currentForced = hr;
        _currentForced.ForceActive(true);

        if (logStateChanges)
            Debug.Log($"SpotlightFocusEnforcer: FORCING '{_currentForced.name}'");
    }

    private void ClearForcedIfAny()
    {
        if (!_currentForced) return;

        _currentForced.ForceActive(false);

        if (logStateChanges)
            Debug.Log($"SpotlightFocusEnforcer: CLEAR '{_currentForced.name}'");

        _currentForced = null;
    }
}
