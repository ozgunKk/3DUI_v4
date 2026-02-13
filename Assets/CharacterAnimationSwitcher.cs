using UnityEngine;

public class CharacterAnimationSwitcher : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private HeadRotate headRotate;

    [Header("Animator parameters")]
    [SerializeField] private string isWritingParam = "IsWriting";        // Bool
    [SerializeField] private string startWritingTrigger = "StartWriting"; // Trigger (optional)

    [Header("Behavior")]
    [Tooltip("If true, also fire StartWriting trigger when enabling writing.")]
    [SerializeField] private bool alsoFireStartWritingTrigger = false;

    void Reset()
    {
        animator = GetComponentInChildren<Animator>(true);
        headRotate = GetComponentInChildren<HeadRotate>(true);
    }

    public void SetWritingMode(bool writing)
    {
        if (!animator)
        {
            Debug.LogWarning($"{name}: Animator reference missing.");
            return;
        }

        // Set the bool
        if (!string.IsNullOrEmpty(isWritingParam))
            animator.SetBool(isWritingParam, writing);

        // Optionally fire the trigger too (only when turning writing ON)
        if (alsoFireStartWritingTrigger && writing && !string.IsNullOrEmpty(startWritingTrigger))
        {
            animator.ResetTrigger(startWritingTrigger);
            animator.SetTrigger(startWritingTrigger);
        }

        // Head rotate rule:
        // writing ON  -> disable head rotate
        // writing OFF -> enable head rotate
        if (headRotate)
        {
            if (writing) headRotate.DisableHeadRotate();
            else headRotate.EnableHeadRotate();
        }
    }

    public void EnableWritingMode() => SetWritingMode(true);
    public void EnableSittingMode() => SetWritingMode(false);

    public void StartWriting()
    {
        if (!animator)
        {
            Debug.LogWarning($"{name}: Animator reference missing.");
            return;
        }

        if (headRotate) headRotate.DisableHeadRotate();

        if (!string.IsNullOrEmpty(startWritingTrigger))
        {
            animator.ResetTrigger(startWritingTrigger);
            animator.SetTrigger(startWritingTrigger);
        }

        if (!string.IsNullOrEmpty(isWritingParam))
            animator.SetBool(isWritingParam, true);
    }

    public void EnableHeadRotateOnly()
    {
        if (headRotate) headRotate.EnableHeadRotate();
    }

    public void DisableHeadRotateOnly()
    {
        if (headRotate) headRotate.DisableHeadRotate();
    }

    public void TriggerSpotlightPressed()
    {
        if (!animator)
        {
            Debug.LogWarning($"{name}: Animator reference missing.");
            return;
        }

        animator.ResetTrigger("SpotlightPressed");
        animator.SetTrigger("SpotlightPressed");
    }


}
