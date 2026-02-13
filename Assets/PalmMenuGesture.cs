using UnityEngine;

public class PalmMenuGesture : MonoBehaviour
{
    [Header("References")]
    public Transform head;              // CenterEyeAnchor
    public Transform handTransform;     // LeftHandAnchor (or palm joint later)
    public GameObject menuRoot;         // MenuRoot object
    public OVRHand ovrHand;             // Drag OVRLeftHandDataSource here

    [Header("Facing + Distance")]
    [Range(-1f, 1f)] public float facingThreshold = 0.85f; // narrower
    public float maxDistanceToHead = 0.60f;
    public float closeDelaySeconds = 0.20f;

    [Header("Palm Axis")]
    public bool useHandUpAsPalmNormal = true;
    public bool invertPalmNormal = true;

    [Header("Open Hand Gate")]
    public bool requireOpenHand = true;
    [Range(0f, 1f)] public float maxPinchStrengthToCountAsOpen = 0.2f;

    float _closeTimer = 0f;

    void Update()
    {
        if (!head || !handTransform || !menuRoot) return;

        if (requireOpenHand && !IsHandOpen())
        {
            CloseMenuWithDelay();
            return;
        }

        Vector3 toHead = head.position - handTransform.position;
        float distance = toHead.magnitude;
        Vector3 dirToHead = (distance > 0.0001f) ? (toHead / distance) : Vector3.forward;

        Vector3 palmNormal = useHandUpAsPalmNormal ? handTransform.up : handTransform.forward;
        if (invertPalmNormal) palmNormal = -palmNormal;

        float facing = Vector3.Dot(palmNormal.normalized, dirToHead);

        bool shouldOpen = (facing > facingThreshold) && (distance < maxDistanceToHead);

        if (shouldOpen)
        {
            _closeTimer = 0f;
            if (!menuRoot.activeSelf) menuRoot.SetActive(true);
        }
        else
        {
            CloseMenuWithDelay();
        }
    }

    bool IsHandOpen()
    {
        if (!ovrHand) return true;        // don't block if not assigned
        if (!ovrHand.IsTracked) return false;

        // If any finger is strongly pinching/curling, treat as not-open
        float thumb = ovrHand.GetFingerPinchStrength(OVRHand.HandFinger.Thumb);
        float index = ovrHand.GetFingerPinchStrength(OVRHand.HandFinger.Index);
        float middle = ovrHand.GetFingerPinchStrength(OVRHand.HandFinger.Middle);
        float ring = ovrHand.GetFingerPinchStrength(OVRHand.HandFinger.Ring);
        float pinky = ovrHand.GetFingerPinchStrength(OVRHand.HandFinger.Pinky);

        float max = Mathf.Max(thumb, index, middle, ring, pinky);
        return max <= maxPinchStrengthToCountAsOpen;
    }

    void CloseMenuWithDelay()
    {
        _closeTimer += Time.deltaTime;
        if (_closeTimer >= closeDelaySeconds && menuRoot.activeSelf)
            menuRoot.SetActive(false);
    }
}
