using UnityEngine;
using UnityEngine.Video;
using TMPro;

public class QuestionnaireResultsPresenter : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;

    [Header("Video (fake result video)")]
    [SerializeField] private VideoPlayer videoPlayer;

    [Header("Results Text (TMP 3D)")]
    [SerializeField] private TMP_Text resultsText;

    [Header("Behavior")]
    [Tooltip("If true, stops/disables the VideoPlayer when showing results text.")]
    [SerializeField] private bool disableVideoWhenShowingResults = true;

    public void ShowResults(string text)
    {
        if (panelRoot && !panelRoot.activeSelf)
            panelRoot.SetActive(true);

        if (disableVideoWhenShowingResults && videoPlayer)
        {
            videoPlayer.Stop();
            videoPlayer.enabled = false; 
        }

        if (resultsText)
        {
            resultsText.gameObject.SetActive(true);
            resultsText.text = text;
        }
    }

    public void HideResults()
    {
        if (resultsText)
            resultsText.gameObject.SetActive(false);

        // keeping this in case of going back to the fake video:
        if (videoPlayer)
            videoPlayer.enabled = true;
    }
}
