using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class VideoFlowController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private VideoPlayer player;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private RawImage videoScreen;

    [Header("Clips")]
    [SerializeField] private VideoClip introClip;    // first video (auto-plays)
    [SerializeField] private VideoClip successClip;  // second video
    [SerializeField] private VideoClip failureClip;  // third video

    [Header("UI")]
    [SerializeField] private GameObject choicePanel; // contains the two buttons
    [SerializeField] private Button successButton;
    [SerializeField] private Button failureButton;

    private enum State { Idle, Intro, WaitingForChoice, PlayingOutcome }
    private State state = State.Idle;

    private void Awake()
    {
        // Safety checks
        if (!player) player = GetComponent<VideoPlayer>();
        if (!audioSource) audioSource = GetComponent<AudioSource>();

        // Ensure audio is routed
        player.audioOutputMode = VideoAudioOutputMode.AudioSource;
        player.EnableAudioTrack(0, true);
        player.SetTargetAudioSource(0, audioSource);

        // Button wiring
        if (successButton) successButton.onClick.AddListener(PlaySuccess);
        if (failureButton) failureButton.onClick.AddListener(PlayFailure);

        ShowChoices(false);

        // Subscribe to end-of-clip event
        player.loopPointReached += OnVideoFinished;
    }

    private void Start()
    {
        PlayClip(introClip, State.Intro);
    }

    private void OnDestroy()
    {
        player.loopPointReached -= OnVideoFinished;
        if (successButton) successButton.onClick.RemoveListener(PlaySuccess);
        if (failureButton) failureButton.onClick.RemoveListener(PlayFailure);
    }

    private void PlayClip(VideoClip clip, State nextState)
    {
        if (clip == null)
        {
            Debug.LogWarning("Missing VideoClip reference.");
            return;
        }

        ShowChoices(false);
        state = nextState;

        player.source = VideoSource.VideoClip;
        player.clip = clip;

        // Prepare to avoid a black frame; then play when ready
        player.Prepare();
        player.prepareCompleted += OnPrepared;
    }

    private void OnPrepared(VideoPlayer vp)
    {
        vp.prepareCompleted -= OnPrepared;
        vp.Play();
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        if (state == State.Intro)
        {
            // After intro finishes, reveal choice UI
            state = State.WaitingForChoice;
            ShowChoices(true);
        }
        else if (state == State.PlayingOutcome)
        {
            // End of success/failure video; you can loop back to choices or end flow.
            ShowChoices(true);
            state = State.WaitingForChoice;
        }
    }

    private void PlaySuccess()
    {
        PlayClip(successClip, State.PlayingOutcome);
    }

    private void PlayFailure()
    {
        PlayClip(failureClip, State.PlayingOutcome);
    }

    private void ShowChoices(bool show)
    {
        if (choicePanel) choicePanel.SetActive(show);
        // Optionally pause the player while choices are up
        if (show && player.isPlaying) player.Pause();
    }
}
