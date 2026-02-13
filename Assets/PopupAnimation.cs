using System.Collections;
using UnityEngine;

public class UIPopup : MonoBehaviour
{
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private float popDuration = 0.15f;
    [SerializeField] private float visibleTime = 1.0f;

    Coroutine _routine;

    public void ShowPopup()
    {
        if (!popupRoot) return;

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(PopRoutine());
    }

    private IEnumerator PopRoutine()
    {
        popupRoot.SetActive(true);

        // pop from 0 to 1
        popupRoot.transform.localScale = Vector3.zero;
        float t = 0f;
        while (t < popDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / popDuration);
            popupRoot.transform.localScale = Vector3.LerpUnclamped(Vector3.zero, Vector3.one, EaseOutBack(k));
            yield return null;
        }
        popupRoot.transform.localScale = Vector3.one;

        yield return new WaitForSecondsRealtime(visibleTime);

        popupRoot.SetActive(false);
    }

    private float EaseOutBack(float x)
    {
        // nice “pop”
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
