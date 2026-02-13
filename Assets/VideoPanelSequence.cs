using System.Collections;
using UnityEngine;
using UnityEngine.Video;

public class VideoPanelSequence : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject panelRoot;     // QuestionnerePanel 
    [SerializeField] private VideoPlayer videoPlayer;  // VideoPlayer on the panel

    [Header("Timing")]
    [SerializeField] private float showDelay = 3f;
    [SerializeField] private int loopsToPlay = 4;

    [SerializeField] private bool playFakeVideo = false;


    public System.Action OnQuestionnaireShown;


    private int loopsPlayed = 0;
    private Coroutine running;

    public void StartSequence()
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        if (!panelRoot || !videoPlayer)
        {
            Debug.LogWarning("VideoPanelSequence: Missing references.");
            yield break;
        }

        // Wait before showing panel
        yield return new WaitForSeconds(showDelay);

        // Enable panel first
        if (!panelRoot.activeSelf) panelRoot.SetActive(true);

        OnQuestionnaireShown?.Invoke();

        if (!playFakeVideo)
            yield break;



        // Prepare loop counting
        loopsPlayed = 0;
        videoPlayer.isLooping = false;
        videoPlayer.loopPointReached -= OnLoopPointReached;
        videoPlayer.loopPointReached += OnLoopPointReached;

        // Start playback
        videoPlayer.time = 0;
        videoPlayer.Play();
    }

    private void OnLoopPointReached(VideoPlayer vp)
    {
        loopsPlayed++;

        if (loopsPlayed < loopsToPlay)
        {
            // Replay again
            vp.time = 0;
            vp.Play();
        }
        else
        {
            // Stop and keep last frame visible
            vp.Pause();
            vp.loopPointReached -= OnLoopPointReached;
        }
    }
}
