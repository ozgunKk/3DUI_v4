using UnityEngine;
using UnityEngine.UI;

public class PokeUIButton : MonoBehaviour
{
    [Tooltip("Drag the fingertip transform here (HandIndexFingertip).")]
    public Transform fingertip;

    private Button _button;
    private bool _pressed;

    void Awake()
    {
        _button = GetComponent<Button>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (_button == null || _pressed) return;
        if (fingertip == null) return;

        // Only react to the fingertip collider
        if (other.transform != fingertip) return;

        _pressed = true;
        _button.onClick.Invoke();
    }

    void OnTriggerExit(Collider other)
    {
        if (fingertip == null) return;
        if (other.transform != fingertip) return;

        _pressed = false;
    }
}
