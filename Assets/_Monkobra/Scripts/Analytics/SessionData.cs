using System;
using System.Collections.Generic;

/// <summary>
/// all data of one session (one game launch), filled by
/// <see cref="AnalyticsManager"/> and rendered by
/// <see cref="AnalyticsMarkdownWriter"/>
/// </summary>
public class SessionData {
    // Public Members  #########################################################
    public DateTime StartClock;
    // heading text of the session file
    public string Title = string.Empty;
    public string AppVersion = string.Empty;
    public readonly List<RunData> Runs = new List<RunData>();

    // Public Methods  #########################################################
    /// <summary>
    /// death causes of the <see cref="RunStatus.Lost"/> runs, in run order
    /// </summary>
    public List<DeathCause> GetLostDeathCauses() {
        List<DeathCause> causes = new List<DeathCause>();
        for (int i = 0; i < Runs.Count; i++) {
            if (Runs[i].Status == RunStatus.Lost) {
                causes.Add(Runs[i].Cause);
            }
        }

        return causes;
    }
}
