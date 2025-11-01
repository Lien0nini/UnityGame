using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using System.Collections.Generic;
using System.Text.RegularExpressions;

[System.Serializable]
public class QuestionSet
{
    [Header("Video (required)")]
    public VideoClip questionClip;
    public VideoClip successClip;
    public VideoClip failureClip;

    [Header("Dialogue (mp3, optional)")]
    public AudioClip questionDialogue;
    public AudioClip successDialogue;
    public AudioClip failureDialogue;

    [Header("Background Music (mp3, optional)")]
    public AudioClip questionMusic;
    public AudioClip successMusic;
    public AudioClip failureMusic;

    [Header("Subtitles (SRT, optional)")]
    public TextAsset questionSrt;
    public TextAsset successSrt;
    public TextAsset failureSrt;
}

public class VideoFlowController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private VideoPlayer player;
    [SerializeField] private AudioSource dialogueSource;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private TextMeshProUGUI subtitleText;

    [Header("UI")]
    [SerializeField] private GameObject choicePanel;
    [SerializeField] private Button successButton;
    [SerializeField] private Button failureButton;

    [Header("Video Sequence")]
    [SerializeField] private List<QuestionSet> questions = new List<QuestionSet>();

    [Header("Subtitle Options")]
    [Tooltip("Apply a constant time shift to all cues (e.g., to offset audio latency).")]
    [SerializeField] private double timeOffsetSeconds = -0.12;  // tweak ±0.02 if needed
    [Tooltip("Clear on-screen text whenever playback is outside any cue.")]
    [SerializeField] private bool clearWhenStopped = true;

    private int currentIndex = 0;
    private enum Phase { Question, OutcomeSuccess, OutcomeFailure }
    private Phase phase = Phase.Question;

    private enum MediaKind { Question, Success, Failure }

    // ---- Minimal SRT runtime ----
    private class Cue { public double start, end; public string text; }
    private readonly List<Cue> cues = new();
    private int lastCueIndex = -1;

    void Awake()
    {
        if (!player) player = GetComponent<VideoPlayer>();
        if (successButton) successButton.onClick.AddListener(OnSuccess);
        if (failureButton) failureButton.onClick.AddListener(OnFailure);

        player.loopPointReached += OnVideoFinished;
        player.prepareCompleted += OnPreparedPlayAll;

        player.audioOutputMode = VideoAudioOutputMode.AudioSource;
        player.EnableAudioTrack(0, true);
        player.SetTargetAudioSource(0, dialogueSource);
        player.SetDirectAudioMute(0, true);
        player.skipOnDrop = true;
        player.waitForFirstFrame = true;

        ClearSubtitle();
    }

    void Start()
    {
        if (questions.Count == 0 || questions[0].questionClip == null)
        {
            Debug.LogWarning("No questions configured or missing first question clip.");
            return;
        }
        PlayQuestion(currentIndex);
    }

    void OnDestroy()
    {
        player.loopPointReached -= OnVideoFinished;
        player.prepareCompleted -= OnPreparedPlayAll;
        if (successButton) successButton.onClick.RemoveListener(OnSuccess);
        if (failureButton) failureButton.onClick.RemoveListener(OnFailure);
    }

    void Update()
    {
        if (!subtitleText || cues.Count == 0) return;

        // Use dialogue audio clock for stable sync
        double t = ((dialogueSource && dialogueSource.clip)
            ? (double)dialogueSource.time
            : player ? player.time : 0.0) + timeOffsetSeconds;

        if (lastCueIndex >= 0 && lastCueIndex < cues.Count)
        {
            var c = cues[lastCueIndex];
            if (t >= c.start && t <= c.end) return;
            if (clearWhenStopped) subtitleText.text = string.Empty;
        }

        int idx = FindCueIndex(t);
        if (idx != -1)
        {
            lastCueIndex = idx;
            subtitleText.text = cues[idx].text;
        }
        else
        {
            lastCueIndex = ClampLastLessOrEqualIndex(t);
            if (clearWhenStopped) subtitleText.text = string.Empty;
        }
    }

    // ---------- Flow ----------

    private void PlayQuestion(int index)
    {
        phase = Phase.Question;
        QueueBundle(questions[index], MediaKind.Question);
    }

    private void PlaySuccessOutcome()
    {
        phase = Phase.OutcomeSuccess;
        QueueBundle(questions[currentIndex], MediaKind.Success);
    }

    private void PlayFailureOutcome()
    {
        phase = Phase.OutcomeFailure;
        QueueBundle(questions[currentIndex], MediaKind.Failure);
    }

    private void OnSuccess() => PlaySuccessOutcome();
    private void OnFailure() => PlayFailureOutcome();

    private void OnVideoFinished(VideoPlayer vp)
    {
        switch (phase)
        {
            case Phase.Question:
                if (choicePanel) choicePanel.SetActive(true);
                break;
            case Phase.OutcomeSuccess:
                currentIndex++;
                if (currentIndex < questions.Count && questions[currentIndex].questionClip)
                    PlayQuestion(currentIndex);
                else
                {
                    if (choicePanel) choicePanel.SetActive(false);
                    StopAudio();
                    Debug.Log("✅ Sequence complete!");
                }
                break;
            case Phase.OutcomeFailure:
                PlayQuestion(currentIndex);
                break;
        }
    }

    private void QueueBundle(QuestionSet qs, MediaKind kind)
    {
        if (choicePanel) choicePanel.SetActive(false);

        VideoClip v = kind switch
        {
            MediaKind.Question => qs.questionClip,
            MediaKind.Success  => qs.successClip,
            _                  => qs.failureClip
        };
        AudioClip d = kind switch
        {
            MediaKind.Question => qs.questionDialogue,
            MediaKind.Success  => qs.successDialogue,
            _                  => qs.failureDialogue
        };
        AudioClip m = kind switch
        {
            MediaKind.Question => qs.questionMusic,
            MediaKind.Success  => qs.successMusic,
            _                  => qs.failureMusic
        };

        TextAsset srt = kind switch
        {
            MediaKind.Question => qs.questionSrt,
            MediaKind.Success  => qs.successSrt,
            _                  => qs.failureSrt
        };

        if (!v) { Debug.LogWarning($"Missing VideoClip for {kind} at index {currentIndex}"); return; }

        StopAudio();
        dialogueSource.clip = d;
        musicSource.clip = m;

        player.Stop();
        player.clip = v;
        player.time = 0.0;
        player.frame = 0;

        LoadSrt(srt);
        player.Prepare();
    }

    private void OnPreparedPlayAll(VideoPlayer vp)
    {
        if (dialogueSource.clip) dialogueSource.time = 0f;
        if (musicSource.clip) musicSource.time = 0f;

        vp.Play();
        if (dialogueSource.clip) dialogueSource.Play();
        if (musicSource.clip) musicSource.Play();
    }

    private void StopAudio()
    {
        if (dialogueSource && dialogueSource.isPlaying) dialogueSource.Stop();
        if (musicSource && musicSource.isPlaying) musicSource.Stop();
    }

    // ---------- SRT handling ----------
    private void LoadSrt(TextAsset srt)
    {
        cues.Clear();
        lastCueIndex = -1;
        ClearSubtitle();
        if (!srt) return;

        string body = srt.text.Replace("\r\n", "\n").Replace("\r", "\n");
        var blockRx = new Regex(
            @"(?:^|\n)\d+\n" +
            @"(\d{2}:\d{2}:\d{2},\d{3})\s-->\s" +
            @"(\d{2}:\d{2}:\d{2},\d{3})\n" +
            @"([\s\S]*?)(?=\n{2,}|\n\z|\z)", RegexOptions.Compiled);

        foreach (Match m in blockRx.Matches(body))
        {
            double start = ToSeconds(m.Groups[1].Value);
            double end = ToSeconds(m.Groups[2].Value);
            if (end < start) continue;
            string text = m.Groups[3].Value.Trim();
            cues.Add(new Cue { start = start, end = end, text = text });
        }
    }

    private static double ToSeconds(string hhmmssmmm)
    {
        var parts = hhmmssmmm.Split(':', ',');
        int h = int.Parse(parts[0]), m = int.Parse(parts[1]),
            s = int.Parse(parts[2]), ms = int.Parse(parts[3]);
        return h * 3600 + m * 60 + s + ms / 1000.0;
    }

    private int FindCueIndex(double t)
    {
        int lo = 0, hi = cues.Count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            var c = cues[mid];
            if (t < c.start) hi = mid - 1;
            else if (t > c.end) lo = mid + 1;
            else return mid;
        }
        return -1;
    }

    private int ClampLastLessOrEqualIndex(double t)
    {
        int lo = 0, hi = cues.Count - 1, ans = -1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            if (cues[mid].start <= t) { ans = mid; lo = mid + 1; }
            else hi = mid - 1;
        }
        return Mathf.Clamp(ans, -1, cues.Count - 1);
    }

    private void ClearSubtitle()
    {
        if (subtitleText) subtitleText.text = string.Empty;
    }
}
