using System.Collections;
using UnityEngine;

public class ScenarioFlowController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HammerVFXStages hammerVFX;

    [Header("Timing")]
    [SerializeField] private float toLowAfterSeconds = 5f;
    [SerializeField] private float toHighAfterSeconds = 10f; // "another 10 seconds after low"
    [SerializeField] private float afterSpotlightBackToLowDelay = 4f;

    private Coroutine scenarioRoutine;
    private Coroutine backToLowRoutine;

    // States for the flow
    private bool scenarioRunning = false;
    private bool reachedHighWaitingForSpotlight = false;

    private void Awake()
    {
        if (!hammerVFX)
            hammerVFX = FindObjectOfType<HammerVFXStages>(true);
    }

    public void StartSimulation()
    {
        StopAllFlow();

        scenarioRunning = true;
        reachedHighWaitingForSpotlight = false;

        if (!hammerVFX)
        {
            Debug.LogWarning($"{name}: HammerVFXStages reference missing.");
            return;
        }

        // Start at None stage
        hammerVFX.SetStage(HammerVFXStages.Stage.None);

        scenarioRoutine = StartCoroutine(RunScenario());
    }

    private IEnumerator RunScenario()
    {
        // After 5 seconds -> Low
        yield return new WaitForSeconds(toLowAfterSeconds);

        if (!scenarioRunning) yield break;
        hammerVFX.SetStage(HammerVFXStages.Stage.Low);

        // After another 10 seconds -> High
        yield return new WaitForSeconds(toHighAfterSeconds);

        if (!scenarioRunning) yield break;
        hammerVFX.SetStage(HammerVFXStages.Stage.High);

        // Stay High until Spotlight button is pressed
        reachedHighWaitingForSpotlight = true;

        while (scenarioRunning && reachedHighWaitingForSpotlight)
            yield return null;
    }

    public void OnSpotlightPressed()
    {
        // Only meaningful once reached the "High until Spotlight" phase.
        if (!scenarioRunning) return;
        if (!reachedHighWaitingForSpotlight) return;

        reachedHighWaitingForSpotlight = false;

        // Start (or restart) the "back to low after 4 sec" timer
        if (backToLowRoutine != null)
            StopCoroutine(backToLowRoutine);

        backToLowRoutine = StartCoroutine(BackToLowAfterDelay());
    }

    private IEnumerator BackToLowAfterDelay()
    {
        yield return new WaitForSeconds(afterSpotlightBackToLowDelay);

        if (!scenarioRunning) yield break;

        hammerVFX.SetStage(HammerVFXStages.Stage.Low);
        backToLowRoutine = null;
    }
    public void OnDisableAllUIPressed()
    {
        if (!hammerVFX) return;

        // Go back to None stage
        hammerVFX.SetStage(HammerVFXStages.Stage.None);

        // End scenario flow
        StopAllFlow();
    }

    private void StopAllFlow()
    {
        scenarioRunning = false;
        reachedHighWaitingForSpotlight = false;

        if (scenarioRoutine != null)
        {
            StopCoroutine(scenarioRoutine);
            scenarioRoutine = null;
        }

        if (backToLowRoutine != null)
        {
            StopCoroutine(backToLowRoutine);
            backToLowRoutine = null;
        }
    }
}
