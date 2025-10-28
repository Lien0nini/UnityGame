using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Collections.Generic;

[System.Serializable]
public class QuestionSet
{
    public VideoClip questionClip;
    public VideoClip successClip;
    public VideoClip failureClip;
}

public class VideoFlowController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private VideoPlayer player;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private RawImage videoScreen;

    [Header("UI")]
    [SerializeField] private GameObject choicePanel;
    [SerializeField] private Button successButton;
    [SerializeField] private Button failureButton;

    [Header("Video Sequence")]
    [SerializeField] private List<QuestionSet> questions = new List<QuestionSet>();

    private int currentIndex = 0;

    private enum Phase { Question, OutcomeSuccess, OutcomeFailure }
    private Phase phase = Phase.Question;

    private void Awake()
    {
        // Wire buttons
        if (successButton) successButton.onClick.AddListener(OnSuccess);
        if (failureButton) failureButton.onClick.AddListener(OnFailure);

        // Video events
        player.loopPointReached += OnVideoFinished;

        // Route audio through AudioSource
        player.audioOutputMode = VideoAudioOutputMode.AudioSource;
        player.EnableAudioTrack(0, true);
        player.SetTargetAudioSource(0, audioSource);
    }

    private void Start()
    {
        if (questions.Count == 0 || questions[0].questionClip == null)
        {
            Debug.LogWarning("No questions configured.");
            return;
        }
        PlayQuestion(currentIndex);
    }

    private void OnDestroy()
    {
        player.loopPointReached -= OnVideoFinished;
        if (successButton) successButton.onClick.RemoveListener(OnSuccess);
        if (failureButton) failureButton.onClick.RemoveListener(OnFailure);
    }

    // ---------- Flow helpers ----------

    private void PlayClip(VideoClip clip)
    {
        if (!clip) { Debug.LogWarning("Missing VideoClip."); return; }
        choicePanel.SetActive(false);

        // Prepare then play (prevents black frame)
        player.clip = clip;
        player.prepareCompleted -= OnPrepared;           // safety: avoid multi-subscribe
        player.prepareCompleted += OnPrepared;
        player.Prepare();
    }

    private void OnPrepared(VideoPlayer vp)
    {
        vp.prepareCompleted -= OnPrepared;
        vp.Play();
    }

    private void PlayQuestion(int index)
    {
        phase = Phase.Question;
        PlayClip(questions[index].questionClip);
    }

    private void PlaySuccessOutcome()
    {
        phase = Phase.OutcomeSuccess;
        PlayClip(questions[currentIndex].successClip);
    }

    private void PlayFailureOutcome()
    {
        phase = Phase.OutcomeFailure;
        PlayClip(questions[currentIndex].failureClip);
    }

    // ---------- UI callbacks ----------

    private void OnSuccess()  => PlaySuccessOutcome();
    private void OnFailure()  => PlayFailureOutcome();

    // ---------- Video end logic ----------

    private void OnVideoFinished(VideoPlayer vp)
    {
        switch (phase)
        {
            case Phase.Question:
                // Question just ended → show choices
                choicePanel.SetActive(true);
                break;

            case Phase.OutcomeSuccess:
                // Success outcome ended → advance to NEXT question (if any)
                currentIndex++;
                if (currentIndex < questions.Count && questions[currentIndex].questionClip)
                {
                    PlayQuestion(currentIndex);
                }
                else
                {
                    choicePanel.SetActive(false);
                    Debug.Log("✅ Sequence complete!");
                }
                break;

            case Phase.OutcomeFailure:
                // Failure outcome ended → REPLAY the SAME question
                PlayQuestion(currentIndex);
                break;
        }
    }
}
