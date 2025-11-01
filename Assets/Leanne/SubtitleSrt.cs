using UnityEngine;
using UnityEngine.Video;
using TMPro;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class SubtitleSrt : MonoBehaviour
{
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private TextMeshProUGUI subtitleText;

    [Tooltip("Apply a constant shift to all cues (seconds).")]
    [SerializeField] private double timeOffset = 0.0;

    private readonly List<(double start, double end, string text)> cues = new();
    private int current = -1;

    public void SetTargets(VideoPlayer vp, TextMeshProUGUI tmp)
    {
        videoPlayer = vp;
        subtitleText = tmp;
    }

    public void LoadSrt(TextAsset srtAsset)
    {
        cues.Clear();
        current = -1;
        if (subtitleText) subtitleText.text = string.Empty;
        if (!srtAsset) return;

        // Normalize newlines and strip UTF-8 BOM if present
        string body = srtAsset.text.Replace("\r\n", "\n").Replace("\r", "\n");
        if (body.Length > 0 && body[0] == '\uFEFF') body = body.Substring(1);

        // Accept with or without numeric index line
        var blockRegex = new Regex(
            @"(?:^|\n)(?:\d+\n)?" +                                   // optional index
            @"(\d{2}:\d{2}:\d{2},\d{3})\s-->\s(\d{2}:\d{2}:\d{2},\d{3})\n" + // times
            @"([\s\S]*?)(?=\n{2,}|\n\z|\z)",                          // text
            RegexOptions.Compiled);

        foreach (Match m in blockRegex.Matches(body))
        {
            double s = ToSeconds(m.Groups[1].Value);
            double e = ToSeconds(m.Groups[2].Value);
            if (e < s) continue;
            string t = m.Groups[3].Value.Trim();
            cues.Add((s, e, t));
        }
        // (Optional) ensure sorted
        cues.Sort((a, b) => a.start.CompareTo(b.start));
    }

    private void Update()
    {
        if (videoPlayer == null || subtitleText == null) return;
        if (cues.Count == 0) { if (subtitleText.text.Length != 0) subtitleText.text = string.Empty; return; }
        if (!videoPlayer.isPrepared) return; // wait until the clip is ready

        // Use current time even if paused; this allows seeking & paused displays
        double t = videoPlayer.time + timeOffset;
        if (double.IsNaN(t)) return;

        // Keep current cue if valid
        if (current >= 0 && current < cues.Count)
        {
            var c = cues[current];
            if (t >= c.start && t <= c.end) return;
            // left the cue: clear immediately to prevent stale text in gaps
            if (subtitleText.text.Length != 0) subtitleText.text = string.Empty;
        }

        // Binary search for the cue containing t
        int idx = FindCueIndex(t);
        current = idx;
        subtitleText.text = (current >= 0) ? cues[current].text : string.Empty;
    }

    private static double ToSeconds(string hmsms)
    {
        var sp = hmsms.Split(':', ',');
        int h = int.Parse(sp[0]), m = int.Parse(sp[1]), s = int.Parse(sp[2]), ms = int.Parse(sp[3]);
        return h * 3600 + m * 60 + s + (ms / 1000.0);
    }

    // Returns index of cue that contains t, or -1
    private int FindCueIndex(double t)
    {
        int lo = 0, hi = cues.Count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            var c = cues[mid];
            if (t < c.start) hi = mid - 1;
            else if (t > c.end) lo = mid + 1;
            else return mid;
        }
        return -1;
    }
}
