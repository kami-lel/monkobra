using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// Builds a fully mocked session and saves it as its own markdown file, so
/// the file layout can be checked w/o playing. The live session is untouched.
/// <para>
/// Every run is simulated w/ the tuning of the real game (speed, stamina
/// drain, fruit restore, branch drop, scoring), so heights, durations, scores
/// and death causes agree w/ each other. The monkey gets better run by run.
/// </para>
/// </summary>
public static class MockAnalyticsGenerator {
    // Public Methods  #########################################################
    /// <param name="folder">analytics folder to save into</param>
    /// <param name="now">moment the session is stamped w/ and the last run
    /// ends at</param>
    /// <param name="windowS">death cause window, as in
    /// <see cref="AnalyticsConfig.DeathCauseWindowS"/></param>
    /// <returns>path of the file written, empty if the write failed</returns>
    public static string SaveMockSession(
        string folder,
        DateTime now,
        float windowS
    ) {
        string stamp = now.ToString(
            "yyyy-MM-dd_HH_mm_ss",
            CultureInfo.InvariantCulture
        );
        string path = Path.Combine(folder, "0" + stamp + "-mock.md");
        try {
            Directory.CreateDirectory(folder);
        } catch (Exception e) {
            Debug.LogWarning("Analytics:\tfail to open mock folder: " + e);
            return string.Empty;
        }

        SessionData session = BuildMockSession(
            now,
            new System.Random(),
            windowS
        );
        bool isWritten = AnalyticsMarkdownWriter.WriteFile(
            path,
            AnalyticsMarkdownWriter.BuildSession(session)
        );

        return isWritten ? path : string.Empty;
    }

    /// <returns>session of <see cref="MIN_RUNS"/> to
    /// <see cref="MAX_RUNS"/> runs, all lost but the last, which is cut short;
    /// the final run ends at <paramref name="now"/></returns>
    public static SessionData BuildMockSession(
        DateTime now,
        System.Random rng,
        float windowS
    ) {
        int runCount = rng.Next(MIN_RUNS, MAX_RUNS + 1);
        List<RunPlan> plans = new List<RunPlan>();
        float total = 0f;
        for (int i = 0; i < runCount; i++) {
            // the monkey learns: skill climbs w/ the run number, w/ noise
            float skill = Mathf.Clamp01(
                0.1f + i * (0.7f / runCount) + Range(rng, -0.1f, 0.1f)
            );
            RunPlan plan = SimulateRun(skill, rng);
            if (i == runCount - 1) {
                CutShort(plan, Range(rng, 0.3f, 0.8f));
            }
            plans.Add(plan);
            total += plan.DurationS + RUN_GAP_S;
        }

        DateTime sessionStart = now.AddSeconds(-total);
        SessionData session = new SessionData {
            StartClock = sessionStart,
            Title = sessionStart.ToString(
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture
            ) + " (mock)",
            AppVersion = Application.version
        };

        DateTime cursor = sessionStart;
        foreach (RunPlan plan in plans) {
            session.Runs.Add(BuildRun(plan, cursor, windowS));
            cursor = cursor.AddSeconds(plan.DurationS + RUN_GAP_S);
        }

        return session;
    }

    // constants  ##############################################################
    private const int MIN_RUNS = 6;
    private const int MAX_RUNS = 9;
    private const float RUN_GAP_S = 5f;
    private const float STEP_S = 0.25f;
    private const float MAX_RUN_S = 240f;
    // mirrors the default of AnalyticsConfig.SampleIntervalS
    private const float SAMPLE_INTERVAL_S = 2f;

    // game tuning mirrored from GameBalanceConfig  ----------------------------
    private const float MAX_STAMINA = 100f;
    private const float UPWARD_DRAIN_PER_U = 0.5f;
    // the periodic drop of 10 per 10 s
    private const float IDLE_DRAIN_PER_S = 1f;
    private const float IDLE_RECOVERY_PER_S = 2f;
    private const float FRUIT_RESTORE = 30f;
    private const float EXHAUSTED_SPEED_MULTIPLIER = 0.2f;
    private const float BRANCH_DROP_U = 10f;
    private const float BRANCH_STUN_S = 0.75f;
    private const float UNITS_PER_METER = 10f;
    private const int POINTS_PER_METER = 1;
    private const int REWARD_POINTS = 100;

    // nested types  ###########################################################
    // one moment of a simulated run, seconds since run start
    private readonly struct Moment {
        public readonly float T;
        public readonly float Height;
        public readonly float Stamina;

        public Moment(float t, float height, float stamina) {
            T = t;
            Height = height;
            Stamina = stamina;
        }
    }

    // a simulated run, kept in run time until it is placed on the clock
    private class RunPlan {
        public float DurationS;
        public float EndHeight;
        public float HighestHeight;
        public bool IsLost = true;
        public readonly List<Moment> Bananas = new List<Moment>();
        public readonly List<Moment> Webs = new List<Moment>();
        public readonly List<Moment> Branches = new List<Moment>();
        public readonly List<Moment> Track = new List<Moment>();
    }

    // private methods  ########################################################
    // climbs until the monkey is caught: exhaustion, or a stumble on a web or
    // branch that lets the cobra close in
    private static RunPlan SimulateRun(float skill, System.Random rng) {
        RunPlan plan = new RunPlan();
        float t = 0f;
        float height = 0f;
        float stamina = MAX_STAMINA;
        float stuckUntil = 0f;
        float exhaustedS = 0f;
        float exhaustedLimitS = Range(rng, 3f, 7f);
        // set by a stumble, the moment the cobra reaches the monkey
        float caughtAt = float.MaxValue;
        // avg height between bananas, better players find more of them
        float bananaSpacingU = 22f - skill * 8f;
        float climbSpeedU = 3f + skill * 2f;
        plan.Track.Add(new Moment(0f, 0f, stamina));

        while (t < MAX_RUN_S) {
            t += STEP_S;
            float climbedU = 0f;

            if (t < stuckUntil || rng.NextDouble() < 0.15) {
                stamina += IDLE_RECOVERY_PER_S * STEP_S;
            } else {
                float speed = stamina <= 0f
                    ? climbSpeedU * EXHAUSTED_SPEED_MULTIPLIER
                    : climbSpeedU;
                climbedU = speed * STEP_S;
                height += climbedU;
                stamina -= climbedU * UPWARD_DRAIN_PER_U;
            }
            stamina -= IDLE_DRAIN_PER_S * STEP_S;
            stamina = Mathf.Clamp(stamina, 0f, MAX_STAMINA);
            plan.HighestHeight = Mathf.Max(plan.HighestHeight, height);

            if (rng.NextDouble() < climbedU / bananaSpacingU) {
                stamina = Mathf.Min(MAX_STAMINA, stamina + FRUIT_RESTORE);
                plan.Bananas.Add(new Moment(t, height, stamina));
            }
            if (rng.NextDouble() < climbedU / (70f + skill * 60f)) {
                stuckUntil = t + Range(rng, 1.5f, 3f);
                plan.Webs.Add(new Moment(t, height, stamina));
                if (rng.NextDouble() < 0.3) {
                    caughtAt = Mathf.Min(caughtAt, t + Range(rng, 0.4f, 1.8f));
                }
            }
            if (rng.NextDouble() < climbedU / (35f + skill * 30f)) {
                stuckUntil = Mathf.Max(stuckUntil, t + BRANCH_STUN_S);
                height = Mathf.Max(0f, height - BRANCH_DROP_U);
                plan.Branches.Add(new Moment(t, height, stamina));
                if (rng.NextDouble() < 0.25) {
                    caughtAt = Mathf.Min(caughtAt, t + Range(rng, 0.4f, 1.8f));
                }
            }

            plan.Track.Add(new Moment(t, height, stamina));
            exhaustedS = stamina <= 0f ? exhaustedS + STEP_S : 0f;
            if (t >= caughtAt || exhaustedS >= exhaustedLimitS) {
                break;
            }
        }

        plan.DurationS = t;
        plan.EndHeight = height;

        return plan;
    }

    // ends the run at a fraction of its length, as if the game was quit
    private static void CutShort(RunPlan plan, float fraction) {
        float cutoff = plan.DurationS * fraction;
        plan.IsLost = false;
        plan.DurationS = cutoff;
        plan.Bananas.RemoveAll(m => m.T > cutoff);
        plan.Webs.RemoveAll(m => m.T > cutoff);
        plan.Branches.RemoveAll(m => m.T > cutoff);
        plan.Track.RemoveAll(m => m.T > cutoff);
        Moment last = plan.Track[plan.Track.Count - 1];
        plan.EndHeight = last.Height;
        plan.HighestHeight = 0f;
        foreach (Moment m in plan.Track) {
            plan.HighestHeight = Mathf.Max(plan.HighestHeight, m.Height);
        }
    }

    // places a simulated run on the clock & fills the data a real run holds
    private static RunData BuildRun(
        RunPlan plan,
        DateTime start,
        float windowS
    ) {
        RunData run = new RunData {
            Status = plan.IsLost ? RunStatus.Lost : RunStatus.Interrupted,
            StartClock = start,
            DurationS = plan.DurationS,
            HighestHeight = plan.HighestHeight
        };
        run.End = new EventStamp(
            start.AddSeconds(plan.DurationS),
            plan.EndHeight
        );

        foreach (Moment m in plan.Bananas) {
            run.Bananas.Add(ToStamp(m, start));
        }
        foreach (Moment m in plan.Webs) {
            run.Webs.Add(ToStamp(m, start));
        }
        foreach (Moment m in plan.Branches) {
            run.Branches.Add(ToStamp(m, start));
        }

        AddSamples(run, plan);

        if (plan.IsLost) {
            // the cause comes from the same rule as a real run
            run.Cause = DeathCauseResolver.Resolve(
                run,
                run.End.Clock,
                windowS
            );
        }

        run.HeightScore =
            (long)Mathf.Floor(plan.HighestHeight / UNITS_PER_METER)
            * POINTS_PER_METER;
        run.RewardScore = (long)run.Bananas.Count * REWARD_POINTS;
        run.TotalScore = run.HeightScore + run.RewardScore;

        return run;
    }

    // a sample at the start, then every interval & at the end, as the real
    // sampler does; a banana refill shows at the next tick
    private static void AddSamples(RunData run, RunPlan plan) {
        float nextT = 0f;
        foreach (Moment m in plan.Track) {
            if (m.T >= nextT) {
                run.Samples.Add(new RunSample(m.T, m.Height, m.Stamina));
                nextT = m.T + SAMPLE_INTERVAL_S;
            }
        }

        Moment last = plan.Track[plan.Track.Count - 1];
        if (run.Samples[run.Samples.Count - 1].Elapsed < last.T) {
            run.Samples.Add(new RunSample(last.T, last.Height, last.Stamina));
        }
    }

    private static EventStamp ToStamp(Moment m, DateTime start) {
        return new EventStamp(start.AddSeconds(m.T), m.Height);
    }

    private static float Range(System.Random rng, float min, float max) {
        return min + (float)rng.NextDouble() * (max - min);
    }
}
