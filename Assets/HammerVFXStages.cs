using UnityEngine;

public class HammerVFXStages : MonoBehaviour
{
    public enum Stage { None, Low, High }

    [Header("Stage Roots")]
    [SerializeField] private GameObject stageNone;
    [SerializeField] private GameObject stageLow;
    [SerializeField] private GameObject stageHigh;

    [Header("Optional: clear particles when disabling a stage")]
    [SerializeField] private bool clearOnDisable = true;

    private Stage currentStage = Stage.None;

    void Awake()
    {
        // Ensure a clean initial state
        SetStage(Stage.None);
    }

    public void SetStage(Stage stage)
    {
        if (stage == currentStage) return;
        currentStage = stage;

        // Turn off all first
        DisableStage(stageNone);
        DisableStage(stageLow);
        DisableStage(stageHigh);

        // Then enable the chosen one
        switch (stage)
        {
            case Stage.None: EnableStage(stageNone); break;
            case Stage.Low: EnableStage(stageLow); break;
            case Stage.High: EnableStage(stageHigh); break;
        }
    }

    public void SetNone() => SetStage(Stage.None);
    public void SetLow() => SetStage(Stage.Low);
    public void SetHigh() => SetStage(Stage.High);

    private void EnableStage(GameObject go)
    {
        if (!go) return;
        go.SetActive(true);
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            ps.Play(true);
    }

    private void DisableStage(GameObject go)
    {
        if (!go) return;

        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (clearOnDisable) ps.Clear(true);
        }

        go.SetActive(false);
    }
}
