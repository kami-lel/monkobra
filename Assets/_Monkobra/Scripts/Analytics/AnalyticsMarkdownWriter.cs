using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// renders analytics data to markdown, holds no state of its own
/// </summary>
public static class AnalyticsMarkdownWriter {
    // Public Methods  #########################################################
    /// <summary>
    /// one run entry, <paramref name="number"/> is its 1-based place in the
    /// session
    /// </summary>
    public static string BuildRun(RunData run, int number) {
        // a run still open ends at this very moment
        EventStamp end = run.Status == RunStatus.InProgress
            ? new EventStamp(DateTime.Now, run.End.Height)
            : run.End;
        float duration = run.Status == RunStatus.InProgress
            ? (float)(end.Clock - run.StartClock).TotalSeconds
            : run.DurationS;

        StringBuilder sb = new StringBuilder();
        sb.Append("## Run ").Append(number).Append("\n\n");
        if (run.Status != RunStatus.Lost) {
            sb.Append("- Status: ").Append(run.Status).Append('\n');
        }
        sb.Append("- Highest Height: ").Append(FormatHeight(run.HighestHeight));
        sb.Append('\n');
        if (run.Status == RunStatus.Lost) {
            sb.Append("- Death Height: ").Append(FormatHeight(end.Height));
            sb.Append('\n');
            sb.Append("- Death Cause: ").Append(
                DeathCauseResolver.GetDisplayName(run.Cause)
            ).Append('\n');
        }
        sb.Append('\n');

        sb.Append("#### Run Duration: ");
        sb.Append(duration.ToString("F1", CultureInfo.InvariantCulture));
        sb.Append(" s\n\n");
        sb.Append("- Time Start: ").Append(FormatClock(run.StartClock));
        sb.Append('\n');
        sb.Append("- Time Stop: ").Append(FormatClock(end.Clock));
        sb.Append("\n\n");
        AppendHeightCurve(sb, run.Samples);

        sb.Append("#### Final Score: ").Append(run.TotalScore).Append("\n\n");
        sb.Append("- Score from Height: ").Append(run.HeightScore);
        sb.Append('\n');
        sb.Append("- Score from Banana: ");
        sb.Append(FormatBananaScore(run.RewardScore, run.Bananas.Count));
        sb.Append("\n\n");
        AppendScoreSplit(sb, run.HeightScore, run.RewardScore);

        sb.Append("#### Banana Count: ").Append(run.Bananas.Count);
        sb.Append("\n\n");
        AppendClockList(sb, run.Bananas, run.StartClock, true);
        AppendStaminaCurve(sb, run.Samples);

        sb.Append("#### Web Encounter: ").Append(run.Webs.Count);
        sb.Append("\n\n");
        AppendClockList(sb, run.Webs, run.StartClock, false);

        sb.Append("#### Branch Hit: ").Append(run.Branches.Count);
        sb.Append("\n\n");
        AppendClockList(sb, run.Branches, run.StartClock, true);

        return sb.ToString();
    }

    /// <summary>
    /// whole session file: header, summary of the
    /// <see cref="RunStatus.Lost"/> runs, then every run
    /// </summary>
    public static string BuildSession(SessionData session) {
        StringBuilder sb = new StringBuilder();
        sb.Append("# MonKobra Analytics: Session: ").Append(session.Title);
        sb.Append("\n\n");
        sb.Append("MonKobra v").Append(session.AppVersion).Append("\n\n");

        List<DeathCause> causes = session.GetLostDeathCauses();
        if (causes.Count > 0) {
            sb.Append("## Session Summary\n\n");
            AppendDeathCauses(sb, causes);
            AppendLostRunBars(sb, session);
        }
        for (int i = 0; i < session.Runs.Count; i++) {
            sb.Append(BuildRun(session.Runs[i], i + 1));
        }

        return sb.ToString();
    }

    /// <summary>
    /// whether <paramref name="text"/> was written to
    /// <paramref name="path"/>, a failure logs a warning
    /// </summary>
    public static bool WriteFile(string path, string text) {
        try {
            File.WriteAllText(path, text, new UTF8Encoding(false));
        } catch (Exception e) {
            Debug.LogWarning("Analytics:\tfail to write session: " + e);
            return false;
        }

        return true;
    }

    // constants  ##############################################################
    private const string CLOCK_FORMAT = "HH:mm:ss";

    // private methods  ########################################################
    // pie of what ended each lost run of the session
    private static void AppendDeathCauses(
        StringBuilder sb,
        List<DeathCause> causes
    ) {
        sb.Append("#### Death Causes\n\n```mermaid\npie showData\n");
        sb.Append("    title Death Causes\n");
        foreach (DeathCause cause in Enum.GetValues(typeof(DeathCause))) {
            int count = causes.FindAll(c => c == cause).Count;
            if (count > 0) {
                sb.Append("    \"");
                sb.Append(DeathCauseResolver.GetDisplayName(cause));
                sb.Append("\" : ");
                sb.Append(count).Append('\n');
            }
        }
        sb.Append("```\n\n");
    }

    // one bar chart per measure over the lost runs, x is the run number
    private static void AppendLostRunBars(
        StringBuilder sb,
        SessionData session
    ) {
        List<string> labels = new List<string>();
        List<string> scores = new List<string>();
        List<string> highests = new List<string>();
        List<string> heights = new List<string>();
        List<string> durations = new List<string>();
        for (int i = 0; i < session.Runs.Count; i++) {
            RunData run = session.Runs[i];
            if (run.Status != RunStatus.Lost) {
                continue;
            }

            labels.Add("\"" + (i + 1) + "\"");
            scores.Add(run.TotalScore.ToString(CultureInfo.InvariantCulture));
            highests.Add(FormatNumber(run.HighestHeight));
            heights.Add(FormatNumber(run.End.Height));
            durations.Add(FormatNumber(run.DurationS));
        }

        AppendRunBars(sb, "Final Score vs Run", "Score", labels, scores);
        AppendRunBars(
            sb, "Highest Height vs Run", "Height (u)", labels, highests
        );
        AppendRunBars(
            sb, "Death Height vs Run", "Height (u)", labels, heights
        );
        AppendRunBars(
            sb, "Duration vs Run", "Duration (s)", labels, durations
        );
    }

    private static void AppendRunBars(
        StringBuilder sb,
        string title,
        string yLabel,
        List<string> labels,
        List<string> values
    ) {
        sb.Append("#### ").Append(title).Append("\n\n```mermaid\n");
        sb.Append("xychart-beta\n    title \"").Append(title).Append("\"\n");
        sb.Append("    x-axis \"Run\" [")
            .Append(string.Join(", ", labels)).Append("]\n");
        sb.Append("    y-axis \"").Append(yLabel).Append("\"\n");
        sb.Append("    bar [").Append(string.Join(", ", values));
        sb.Append("]\n```\n\n");
    }

    private static string FormatNumber(float value) {
        return value.ToString("0.#", CultureInfo.InvariantCulture);
    }

    private static string FormatClock(DateTime clock) {
        return clock.ToString(CLOCK_FORMAT, CultureInfo.InvariantCulture);
    }

    private static string FormatSeconds(double seconds) {
        return seconds.ToString("F1", CultureInfo.InvariantCulture);
    }

    private static string FormatHeight(float height) {
        return height.ToString("+0.0;-0.0;+0.0", CultureInfo.InvariantCulture);
    }

    // "unit * count = total" when every banana paid the same, else total only
    private static string FormatBananaScore(long total, int count) {
        if (count > 0 && total % count == 0) {
            return (total / count) + " * " + count + " = " + total;
        }

        return total.ToString(CultureInfo.InvariantCulture);
    }

    // each stamp as time elapsed since the run started, then its height
    // iff hasHeight
    private static void AppendClockList(
        StringBuilder sb,
        List<EventStamp> stamps,
        DateTime start,
        bool hasHeight
    ) {
        if (stamps.Count == 0) {
            return;
        }

        sb.Append(hasHeight ? "| # | Time | y |\n" : "| # | Time |\n");
        sb.Append(hasHeight ? "|---|---|---|\n" : "|---|---|\n");
        for (int i = 0; i < stamps.Count; i++) {
            TimeSpan elapsed = stamps[i].Clock - start;
            sb.Append("| ").Append(i + 1).Append(" | +");
            sb.Append(elapsed.ToString(
                @"hh\:mm\:ss",
                CultureInfo.InvariantCulture
            ));
            sb.Append(" |");
            if (hasHeight) {
                sb.Append(' ').Append(stamps[i].Height.ToString(
                    "0.0",
                    CultureInfo.InvariantCulture
                )).Append(" |");
            }
            sb.Append('\n');
        }
        sb.Append('\n');
    }

    // line of sampled stamina over the run, skipped w/o 2 samples
    private static void AppendStaminaCurve(
        StringBuilder sb,
        List<RunSample> samples
    ) {
        if (samples.Count < 2) {
            return;
        }

        StaminaBarController bar = StaminaBarController.I;
        int maxStamina = Mathf.Max(
            1,
            Mathf.CeilToInt(bar != null ? bar.MaxStamina : 0f)
        );
        List<string> times = new List<string>();
        List<string> values = new List<string>();
        for (int i = 0; i < samples.Count; i++) {
            times.Add(FormatSeconds(samples[i].Elapsed));
            values.Add(samples[i].Stamina.ToString(
                "F1",
                CultureInfo.InvariantCulture
            ));
        }

        sb.Append("```mermaid\nxychart-beta\n");
        sb.Append("    title \"Stamina vs Time\"\n");
        sb.Append("    x-axis \"Elapsed (s)\" [")
            .Append(string.Join(", ", times)).Append("]\n");
        sb.Append("    y-axis \"Stamina\" 0 --> ").Append(maxStamina);
        sb.Append('\n');
        sb.Append("    line [").Append(string.Join(", ", values));
        sb.Append("]\n```\n\n");
    }

    // pie of height score against banana score, skipped w/o any score
    private static void AppendScoreSplit(
        StringBuilder sb,
        long heightScore,
        long rewardScore
    ) {
        if (heightScore <= 0 && rewardScore <= 0) {
            return;
        }

        sb.Append("```mermaid\npie showData\n");
        sb.Append("    title Score Split\n");
        if (heightScore > 0) {
            sb.Append("    \"Height\" : ").Append(heightScore).Append('\n');
        }
        if (rewardScore > 0) {
            sb.Append("    \"Banana\" : ").Append(rewardScore).Append('\n');
        }
        sb.Append("```\n\n");
    }

    // line of sampled height over the run, skipped w/o 2 samples; the x axis
    // is categorical, so even spacing relies on the regular sample interval
    private static void AppendHeightCurve(
        StringBuilder sb,
        List<RunSample> samples
    ) {
        if (samples.Count < 2) {
            return;
        }

        List<string> times = new List<string>();
        List<string> heights = new List<string>();
        float minHeight = 0f;
        float maxHeight = 1f;
        for (int i = 0; i < samples.Count; i++) {
            times.Add(FormatSeconds(samples[i].Elapsed));
            heights.Add(samples[i].Height.ToString(
                "F1",
                CultureInfo.InvariantCulture
            ));
            minHeight = Mathf.Min(minHeight, samples[i].Height);
            maxHeight = Mathf.Max(maxHeight, samples[i].Height);
        }

        sb.Append("```mermaid\nxychart-beta\n");
        sb.Append("    title \"Height vs Time\"\n");
        sb.Append("    x-axis \"Elapsed (s)\" [")
            .Append(string.Join(", ", times)).Append("]\n");
        sb.Append("    y-axis \"Height (u)\" ")
            .Append(Mathf.FloorToInt(minHeight)).Append(" --> ")
            .Append(Mathf.CeilToInt(maxHeight)).Append('\n');
        sb.Append("    line [").Append(string.Join(", ", heights));
        sb.Append("]\n```\n\n");
    }
}
