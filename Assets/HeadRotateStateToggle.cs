using UnityEngine;

public class HeadRotateStateToggle : StateMachineBehaviour
{
    public bool enableOnEnter = true;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var switcher = animator.GetComponentInParent<CharacterAnimationSwitcher>();
        if (switcher == null) return;

        if (enableOnEnter) switcher.EnableHeadRotateOnly();
        else switcher.DisableHeadRotateOnly();
    }
}
