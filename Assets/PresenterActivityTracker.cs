using UnityEngine;

public class PresenterActivityTracker : MonoBehaviour
{
    [Header("Transforms (Teacher Rig)")]
    [SerializeField] private Transform teacherRoot;   // e.g., Camera Rig
    [SerializeField] private Transform head;          // e.g., LeftEyeAnchor (or CenterEye)
    [SerializeField] private Transform leftHand;      // hand anchor
    [SerializeField] private Transform rightHand;     // hand anchor

    [Header("Spotlight (high impact)")]
    [SerializeField] private MenuActions menuActions; 

    [Header("Voice")]
    [Tooltip("If teacher voice is played through an AudioSource, assign it.")]
    [SerializeField] private AudioSource voiceAudioSource;

    [Header("Voice (temporary fallback)")]
    [Range(0f, 1f)]
    [SerializeField] private float voiceFallback01 = 0.5f;


    [Tooltip("If true, use microphone input to measure voice dynamics (PC/testing). On Quest, this requires permissions + setup.")]
    [SerializeField] private bool useMicrophone = false;

    [Tooltip("Microphone device name (blank = default).")]
    [SerializeField] private string microphoneDeviceName = "";

    [Header("Sampling")]
    [SerializeField] private float tickInterval = 0.10f;
    [Range(0f, 1f)][SerializeField] private float smoothing = 0.15f;

    [Header("Weights (sum not required)")]
    [SerializeField] private float weightSpotlight = 0.35f;
    [SerializeField] private float weightVoice = 0.25f;
    [SerializeField] private float weightMovement = 0.20f;
    [SerializeField] private float weightHands = 0.15f;
    [SerializeField] private float weightHead = 0.05f;

    [Header("Tuning")]
    [Tooltip("Meters/sec where movement is considered 'active'.")]
    [SerializeField] private float moveSpeedGood = 0.40f;

    [Tooltip("Degrees/sec where head turn is considered 'active' (minimal weight).")]
    [SerializeField] private float headAngularGood = 60f;

    [Tooltip("Meters/sec where hand motion is considered 'active'.")]
    [SerializeField] private float handSpeedGood = 0.60f;

    [Header("Voice dynamics window")]
    [Tooltip("Seconds window to evaluate if voice amplitude is changing (flat -> boring).")]
    [SerializeField] private float voiceWindowSeconds = 3.0f;

    [Tooltip("If amplitude variance is below this, voice is 'flat'.")]
    [SerializeField] private float voiceVarianceBad = 0.0005f;

    [Tooltip("If amplitude variance is above this, voice is 'dynamic'.")]
    [SerializeField] private float voiceVarianceGood = 0.01f;

    [Header("Debug")]
    [SerializeField] private bool logToConsole = false;

    public float PresenterScore01 { get; private set; } = 0f;

    public float SubMovement01 { get; private set; }
    public float SubHands01 { get; private set; }
    public float SubHead01 { get; private set; }
    public float SubVoice01 { get; private set; }
    public float SubSpotlight01 { get; private set; }

    // internals
    private float _nextTickTime;
    private Vector3 _lastRootPos;
    private Quaternion _lastHeadRot;
    private Vector3 _lastLHandPos;
    private Vector3 _lastRHandPos;

    // voice sampling ring buffer
    private float[] _voiceAmpBuffer;
    private int _voiceAmpIndex;
    private int _voiceAmpCount;

    // microphone
    private AudioClip _micClip;
    private const int MicSampleRate = 16000;

    private void Awake()
    {
        _nextTickTime = Time.time + tickInterval;

        if (teacherRoot) _lastRootPos = teacherRoot.position;
        if (head) _lastHeadRot = head.rotation;
        if (leftHand) _lastLHandPos = leftHand.position;
        if (rightHand) _lastRHandPos = rightHand.position;

        int n = Mathf.Max(8, Mathf.CeilToInt(voiceWindowSeconds / tickInterval));
        _voiceAmpBuffer = new float[n];

        SetupVoiceInput();
    }

    private void SetupVoiceInput()
    {
        if (!useMicrophone) return;

        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            Debug.LogWarning("PresenterActivityTracker: No microphone devices found.");
            useMicrophone = false;
            return;
        }

        string dev = microphoneDeviceName;
        if (string.IsNullOrEmpty(dev)) dev = Microphone.devices[0];

        _micClip = Microphone.Start(dev, true, 10, MicSampleRate);
        if (_micClip == null)
        {
            Debug.LogWarning("PresenterActivityTracker: Microphone.Start failed.");
            useMicrophone = false;
        }
    }

    private void Update()
    {
        if (Time.time < _nextTickTime) return;

        float dt = tickInterval;
        _nextTickTime = Time.time + tickInterval;

        // 1) Movement (teacher root speed)
        float move01 = 0f;
        if (teacherRoot)
        {
            float dist = Vector3.Distance(teacherRoot.position, _lastRootPos);
            float speed = dist / dt;
            move01 = Mathf.Clamp01(speed / moveSpeedGood);
            _lastRootPos = teacherRoot.position;
        }
        SubMovement01 = Smooth(SubMovement01, move01);

        // 2) Head tracking (minimal) - angular velocity proxy
        float head01 = 0f;
        if (head)
        {
            float angle = Quaternion.Angle(_lastHeadRot, head.rotation);
            float angVel = angle / dt; // deg/sec
            head01 = Mathf.Clamp01(angVel / headAngularGood);
            _lastHeadRot = head.rotation;
        }
        SubHead01 = Smooth(SubHead01, head01);

        // 3) Hands activity (speed of both hands)
        float hands01 = 0f;
        if (leftHand && rightHand)
        {
            float ls = Vector3.Distance(leftHand.position, _lastLHandPos) / dt;
            float rs = Vector3.Distance(rightHand.position, _lastRHandPos) / dt;
            float avg = (ls + rs) * 0.5f;
            hands01 = Mathf.Clamp01(avg / handSpeedGood);

            _lastLHandPos = leftHand.position;
            _lastRHandPos = rightHand.position;
        }
        SubHands01 = Smooth(SubHands01, hands01);

        // 4) Spotlight (big boost)
        float spotlight01 = GetSpotlight01();
        SubSpotlight01 = Smooth(SubSpotlight01, spotlight01);

        // 5) Voice dynamics
        float voiceDyn01;

        if (voiceAudioSource || useMicrophone)
        {
            float amp = GetVoiceAmplitudeRMS();
            PushVoiceAmp(amp);
            voiceDyn01 = ComputeVoiceDynamics01();
        }
        else
        {
            // Temporary: fixed mid value until mic is enabled
            voiceDyn01 = voiceFallback01;
        }

        SubVoice01 = Smooth(SubVoice01, voiceDyn01);


        // Combine weighted
        float wSum = weightSpotlight + weightVoice + weightMovement + weightHands + weightHead;
        if (wSum <= 0.0001f) wSum = 1f;

        float score =
            (SubSpotlight01 * weightSpotlight +
             SubVoice01 * weightVoice +
             SubMovement01 * weightMovement +
             SubHands01 * weightHands +
             SubHead01 * weightHead) / wSum;

        PresenterScore01 = Mathf.Clamp01(score);

        if (logToConsole)
        {
            Debug.Log($"PresenterScore={PresenterScore01:F2} " +
                      $"Spot={SubSpotlight01:F2} Voice={SubVoice01:F2} Move={SubMovement01:F2} Hands={SubHands01:F2} Head={SubHead01:F2}");
        }
    }

    private float Smooth(float current, float target)
        => Mathf.Lerp(current, target, smoothing);

    private float GetSpotlight01()
    {
        if (!menuActions) return 0f;

        return menuActions.IsSpotlightModeOn ? 1f : 0f;
    }

    // Voice amplitude: RMS from either AudioSource or Mic clip
    private float GetVoiceAmplitudeRMS()
    {
        if (voiceAudioSource && voiceAudioSource.isActiveAndEnabled)
        {
            float[] samples = new float[256];
            voiceAudioSource.GetOutputData(samples, 0);

            double sumSq = 0;
            for (int i = 0; i < samples.Length; i++)
                sumSq += samples[i] * samples[i];

            return Mathf.Sqrt((float)(sumSq / samples.Length));
        }

        // Microphone path (fallback / prototyping)
        if (useMicrophone && _micClip != null)
        {
            int micPos = Microphone.GetPosition(null);
            if (micPos <= 0) return 0f;

            float[] samples = new float[256];
            int start = Mathf.Max(0, micPos - samples.Length);
            _micClip.GetData(samples, start);

            double sumSq = 0;
            for (int i = 0; i < samples.Length; i++)
                sumSq += samples[i] * samples[i];

            return Mathf.Sqrt((float)(sumSq / samples.Length));
        }

        return 0f;
    }

    private void PushVoiceAmp(float amp)
    {
        if (_voiceAmpBuffer == null || _voiceAmpBuffer.Length == 0) return;

        _voiceAmpBuffer[_voiceAmpIndex] = amp;
        _voiceAmpIndex = (_voiceAmpIndex + 1) % _voiceAmpBuffer.Length;
        _voiceAmpCount = Mathf.Min(_voiceAmpCount + 1, _voiceAmpBuffer.Length);
    }

    private float ComputeVoiceDynamics01()
    {
        // If no data, treat as no dynamics
        if (_voiceAmpCount < 4) return 0f;

        // Compute variance of amplitude in buffer
        float mean = 0f;
        for (int i = 0; i < _voiceAmpCount; i++) mean += _voiceAmpBuffer[i];
        mean /= _voiceAmpCount;

        float var = 0f;
        for (int i = 0; i < _voiceAmpCount; i++)
        {
            float d = _voiceAmpBuffer[i] - mean;
            var += d * d;
        }
        var /= _voiceAmpCount;

        return Mathf.InverseLerp(voiceVarianceBad, voiceVarianceGood, var);
    }
}
