using System.Text;
using System.Text.Json;
using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

public sealed record HistoryFilterOption(string Label, string? Server = null, string? Character = null)
{
    public static HistoryFilterOption All { get; } = new("All characters");
    public override string ToString() => Label;
}

public sealed record HistorySessionItem(SessionRow Row, string DisplayText);

public sealed record HistoryImportResult(string FileName, int ImportedSessions, string Message);

/// <summary>A breakdown row a view can render natively: a share bar sized to
/// Fraction behind "Name … Value", with an optional tooltip.</summary>
public sealed record HistoryBreakdownRow(string Name, string Value, double Fraction, string? Tooltip);

/// <summary>Structured session detail: text header, native-renderable breakdown rows
/// for damage and heals, and the remaining text sections. The plain-text rendition
/// (BuildOverview) stays available for copy-summary and text-only views.</summary>
public sealed record HistoryDetail(
    string HeaderText,
    IReadOnlyList<HistoryBreakdownRow> DamageRows,
    IReadOnlyList<HistoryBreakdownRow> HealRows,
    string RestText,
    IReadOnlyList<TimelinePoint> Timeline,
    IReadOnlyList<PullInfo> Fights)
{
    /// <summary>Session deaths (time + killer), so the per-pull Discord copy can say
    /// you died without each view reloading the snapshot it was built from.</summary>
    public IReadOnlyList<TimedDetail> Deaths { get; init; } = [];
}

/// <summary>DPS-over-time graph geometry, normalized to a drawing surface: X spans the
/// session minutes, Y is inverted (0 = top) so views can feed the points straight into a
/// polyline. Null from the builder means "no graph" (too few points — including every
/// session archived before the timeline existed).</summary>
public sealed record HistoryGraph(
    IReadOnlyList<(double X, double Y)> Points,
    double PeakDps,
    DateTime Start,
    DateTime End);

public static class HistoryPresentation
{
    public const string SelectSessionText = "Select a session.";
    public const string MissingSessionText = "Could not load session detail.";
    public const string MissingComparisonText = "Could not load one of the sessions.";

    public static HistoryFilterOption BuildFilter(string server, string character) =>
        new($"{character} ({server})", server, character);

    public static string BuildCount(int count) =>
        $"{count} session{(count == 1 ? "" : "s")}";

    public static string BuildSessionRow(SessionRow row)
    {
        var duration = TimeSpan.FromSeconds(row.ElapsedSeconds);
        return $"{row.StartLocal:MMM d h:mm tt} - {row.Character}\n" +
               $"   {(row.PrimaryZone.Length > 0 ? row.PrimaryZone : "-")} - " +
               $"{(int)duration.TotalHours}h {duration.Minutes}m - " +
               $"{row.Kills} kills - {row.XpPercent:0.#}% xp - {StatsSnapshot.FormatCoin(row.Copper)}" +
               (row.EndReason == SessionRepository.RecoveredEndReason ? " - (recovered)" : "") +
               (row.EndReason == SessionRepository.ActiveEndReason ? " - (in progress)" : "");
    }

    public static string BuildOverview(SessionRow row, StatsSnapshot snapshot)
    {
        var text = new StringBuilder();
        AppendHeader(text, row, snapshot);

        if (snapshot.DamageBySource.Count > 0)
        {
            text.AppendLine("Top damage sources:");
            var grandTotal = Math.Max(1, snapshot.DamageBySource.Sum(source => source.Total));
            var topTotal = Math.Max(1, snapshot.DamageBySource.Max(source => source.Total));
            foreach (var source in snapshot.DamageBySource.Take(SourceCap))
                text.AppendLine($"  {source.Name,-24} {ShareBar((double)source.Total / topTotal),-10} {source.Total,8:N0}" +
                    $" - {100.0 * source.Total / grandTotal,3:0}% - {source.Hits} hits - avg {(double)source.Total / Math.Max(1, source.Hits):0.#}" +
                    $" - {source.Total / Math.Max(1, snapshot.CombatSeconds):0.#} dps" +
                    (source.Crits > 0 ? $" - {100.0 * source.Crits / Math.Max(1, source.Hits):0}% crit" : ""));
            AppendMore(text, snapshot.DamageBySource.Count, SourceCap, "source");
            text.AppendLine();
        }

        if (snapshot.HealsBySpell.Count > 0)
        {
            text.AppendLine("Top heals:");
            var grandTotal = Math.Max(1, snapshot.HealsBySpell.Sum(heal => heal.Total));
            var topTotal = Math.Max(1, snapshot.HealsBySpell.Max(heal => heal.Total));
            foreach (var heal in snapshot.HealsBySpell.Take(HealCap))
                text.AppendLine($"  {heal.Name,-24} {ShareBar((double)heal.Total / topTotal),-10} {heal.Total,8:N0}" +
                    $" - {100.0 * heal.Total / grandTotal,3:0}% - {heal.Hits} cast{(heal.Hits == 1 ? "" : "s")}" +
                    $" - avg {(double)heal.Total / Math.Max(1, heal.Hits):0.#}" +
                    $" - {heal.Total / Math.Max(1, snapshot.CombatSeconds):0.#} hps");
            AppendMore(text, snapshot.HealsBySpell.Count, HealCap, "heal");
            text.AppendLine();
        }

        AppendRest(text, snapshot);
        return text.ToString();
    }

    private static void AppendHeader(StringBuilder text, SessionRow row, StatsSnapshot snapshot)
    {
        var duration = TimeSpan.FromSeconds(row.ElapsedSeconds);
        var active = TimeSpan.FromSeconds(row.ActiveSeconds);
        text.AppendLine($"{row.Character} ({row.Server}) - {row.StartLocal:dddd MMM d, h:mm tt}");
        text.AppendLine($"Duration {(int)duration.TotalHours}h {duration.Minutes}m - active {(int)active.TotalMinutes}m - ended: {row.EndReason}");
        text.AppendLine();
        text.AppendLine($"Kills      {snapshot.YourKillCount} (+{snapshot.PartyKillCount} group) - {snapshot.KillsPerHour:0.0}/hr");
        text.AppendLine($"XP         {snapshot.XpPercent:0.0}% - {snapshot.XpPerHour:0.0}%/hr" +
                        (snapshot.Levels.Count > 0 ? $" - {string.Join(", ", snapshot.Levels.Select(level => level.Text))}" : "") +
                        (snapshot.AaGained > 0 ? $" - {snapshot.AaGained} AA" : ""));
        text.AppendLine($"Damage     {snapshot.DamageDealt:N0} dealt - {snapshot.SessionDps:0.0} dps - taken {snapshot.DamageTaken:N0}");
        if (snapshot.HealingDone > 0)
            text.AppendLine($"Healing    {snapshot.HealingDone:N0} done - {snapshot.Hps:0.#} hps");
        text.AppendLine($"Money      {StatsSnapshot.FormatCoin(snapshot.Copper)} ({StatsSnapshot.FormatCoin(snapshot.CopperPerHour)}/hr)");
        text.AppendLine($"Deaths     {snapshot.Deaths.Count}");
        text.AppendLine();
    }

    /// <summary>
    /// Caps that REMAIN in the session-history detail, and the rule that goes with them.
    ///
    /// #234 (atrzonkowski) was a silent cap: "Kills by creature" took the top 10 and "Mob
    /// farming" the top 8, both from lists Core sorts by kill count descending. A named is
    /// the mob you killed ONCE, so in a Guk session with a dozen kinds of trash at ten-plus
    /// kills each it sorts below all of them and falls off the end — while Encounters, which
    /// is neither ranked nor truncated, still showed it. That discrepancy is what the
    /// reporter saw, and it is the whole diagnosis.
    ///
    /// Those two lists are now UNCAPPED. This is a desktop review surface — it exists to be
    /// read after play, it scrolls, and one row per creature you killed is not a lot. The
    /// nameds are also the part of a session a player actually remembers, so they are the
    /// worst possible rows to drop.
    ///
    /// **Where a cap survives, it says so.** A truncated list that looks complete is the
    /// "silent no-ops are broken" rule wearing a different hat: the player cannot tell a
    /// short session from a trimmed one.
    /// </summary>
    private const int LootCap = 15;
    private const int SourceCap = 8;
    private const int HealCap = 6;
    private const int PetCap = 8;
    private const int MobLootCap = 4;

    /// <summary>"... and 6 more items" — printed only when something was actually cut.</summary>
    private static void AppendMore(StringBuilder text, int total, int shown, string noun,
        string indent = "  ")
    {
        if (total <= shown) return;
        var extra = total - shown;
        text.AppendLine($"{indent}... and {extra} more {noun}{(extra == 1 ? "" : "s")}");
    }

    private static void AppendRest(StringBuilder text, StatsSnapshot snapshot)
    {
        if (snapshot.YourKills.Count > 0)
        {
            text.AppendLine("Kills by creature:");
            foreach (var kill in snapshot.YourKills)
                text.AppendLine($"  {kill.Name,-28} x{kill.Count}");
            text.AppendLine();
        }

        if (snapshot.Loot.Count > 0)
        {
            text.AppendLine("Loot:");
            foreach (var loot in snapshot.Loot.Take(LootCap))
                text.AppendLine($"  {loot.Item,-34} x{loot.Count}");
            AppendMore(text, snapshot.Loot.Count, LootCap, "item");
            text.AppendLine();
        }

        var farmed = snapshot.Mobs.Where(mob => mob.Kills > 0).ToList();
        if (farmed.Count > 0)
        {
            text.AppendLine("Mob farming (observed personal rates):");
            foreach (var mob in farmed)
            {
                text.AppendLine($"  {mob.Name} - {mob.Kills} kills - avg fight {mob.AvgFightSeconds:0}s - " +
                                $"{mob.XpPercent:0.0}% xp - {StatsSnapshot.FormatCoin(mob.Copper)}");
                foreach (var loot in mob.Loot.Take(MobLootCap))
                    text.AppendLine($"      {loot.Item,-30} x{loot.Count}" +
                        (loot.DropRatePct is { } percent ? $"  {percent:0.#}% ({loot.Count}/{mob.Kills})" : ""));
                AppendMore(text, mob.Loot.Count, MobLootCap, "drop", indent: "      ");
            }
            text.AppendLine();
        }

        if (snapshot.PetAbilities.Count > 0)
            text.AppendLine("Pet abilities: " + string.Join(" - ",
                snapshot.PetAbilities.Take(PetCap).Select(ability =>
                    $"{ability.Name} {ability.Total:N0} ({ability.Hits} hits)"))
                + (snapshot.PetAbilities.Count > PetCap
                    ? $" - ... and {snapshot.PetAbilities.Count - PetCap} more"
                    : ""));
        if (snapshot.Stances.Count > 0)
            text.AppendLine("Stances: " + string.Join(" - ",
                snapshot.Stances.Select(stance => $"{stance.Name} {stance.Damage:N0} dmg over {(int)stance.CombatSeconds}s ({stance.Dps:0.#} dps)")));
        if (snapshot.Invocations.Count > 0)
            text.AppendLine("Invocations: " + string.Join(" - ",
                snapshot.Invocations.Select(inv => $"{inv.Name} {inv.Damage:N0} dmg over {(int)inv.CombatSeconds}s ({inv.Dps:0.#} dps)")));
        if (snapshot.Zones.Count > 0)
            text.AppendLine("Zones: " + string.Join(" -> ", snapshot.Zones.Select(zone => zone.Text)));
        if (snapshot.Markers.Count > 0)
            text.AppendLine("Markers: " + string.Join(" - ", snapshot.Markers.Select(marker => $"{marker.Text} ({marker.Time:h:mm tt})")));
    }

    /// <summary>Structured detail for views that render native breakdown bars.</summary>
    public static HistoryDetail BuildDetail(SessionRow row, StatsSnapshot snapshot)
    {
        var header = new StringBuilder();
        AppendHeader(header, row, snapshot);
        var rest = new StringBuilder();
        AppendRest(rest, snapshot);
        return new HistoryDetail(
            header.ToString().TrimEnd(),
            BuildBreakdownRows(snapshot.DamageBySource, snapshot.CombatSeconds, "dps", 10),
            BuildBreakdownRows(snapshot.HealsBySpell, snapshot.CombatSeconds, "hps", 6),
            rest.ToString().Trim(),
            snapshot.DamageTimeline,
            // Grouped into pulls — the encounter as the player experienced it, adds
            // included. Full list when the session recorded one; the old 8-fight tail
            // (newest first — flip to chronological) keeps older sessions reviewable.
            EncounterGrouping.Group(snapshot.Encounters.Count > 0
                ? snapshot.Encounters
                : [.. snapshot.RecentEncounters.AsEnumerable().Reverse()]))
        { Deaths = snapshot.Deaths };
    }

    /// <summary>One pull's collapsed header line in the History fight review. Leads
    /// with the creatures' names — collapsed, the list reads as "who was fought".</summary>
    public static string BuildFightHeader(PullInfo p) =>
        $"{p.Title} — {p.Start:h:mm tt} · {p.DamageOut:N0} dmg · {p.Dps:0.#} dps · " +
        $"{p.DurationSeconds:0}s · took {p.DamageIn:N0}" +
        (p.Fights.All(f => f.Outcome == "Killed") ? "" : " · " + string.Join(" · ",
            p.Fights.Where(f => f.Outcome != "Killed").Select(f => $"{f.Name} {f.Outcome}").Distinct()));

    /// <summary>Lays a damage timeline out as polyline points on a width×height surface.
    /// Minutes the timeline skips (no damage) are drawn at zero — an idle stretch is
    /// real data, not a gap to interpolate over. Null when there's nothing to draw.</summary>
    public static HistoryGraph? BuildDpsGraph(IReadOnlyList<TimelinePoint> timeline, double width, double height)
    {
        if (timeline.Count < 2 || width <= 0 || height <= 0) return null;
        var start = timeline[0].Time;
        var minutes = (int)Math.Round((timeline[^1].Time - start).TotalMinutes);
        if (minutes < 1) return null;
        var byMinute = timeline.ToDictionary(
            p => (long)Math.Round((p.Time - start).TotalMinutes), p => p.Damage);
        var peak = timeline.Max(p => p.Damage) / 60.0;
        if (peak <= 0) return null;
        var points = new List<(double X, double Y)>(minutes + 1);
        for (var m = 0; m <= minutes; m++)
        {
            var dps = byMinute.GetValueOrDefault(m) / 60.0;
            points.Add((width * m / minutes, height - height * dps / peak));
        }
        return new HistoryGraph(points, peak, start, timeline[^1].Time);
    }

    /// <summary>A step chart for character progress (level dings, AA totals): values
    /// HOLD until the next observation — a level is a fact until the next ding, so the
    /// line is a staircase, never a slope. Null when fewer than two observations or the
    /// value never changed (a flat line across three weeks says nothing worth a chart).</summary>
    public static HistoryGraph? BuildStepGraph(
        IReadOnlyList<(DateTime Time, double Value)> observations, double width, double height)
    {
        var obs = observations.Where(o => o.Value > 0).OrderBy(o => o.Time).ToList();
        if (obs.Count < 2 || width <= 0 || height <= 0) return null;
        var lo = obs.Min(o => o.Value);
        var hi = obs.Max(o => o.Value);
        if (hi <= lo) return null;
        var start = obs[0].Time;
        var span = Math.Max(1, (obs[^1].Time - start).TotalSeconds);
        double X(DateTime t) => width * (t - start).TotalSeconds / span;
        double Y(double v) => height - height * (v - lo) / (hi - lo);
        var points = new List<(double X, double Y)>();
        for (var i = 0; i < obs.Count; i++)
        {
            if (i > 0) points.Add((X(obs[i].Time), Y(obs[i - 1].Value)));   // hold, then step
            points.Add((X(obs[i].Time), Y(obs[i].Value)));
        }
        return new HistoryGraph(points, hi, start, obs[^1].Time);
    }

    /// <summary>The standard ability-row columns ("total · ×hits · avg · rate (· crit%)")
    /// with fractions relative to the top entry — mirrors the live widget's rows.</summary>
    public static IReadOnlyList<HistoryBreakdownRow> BuildBreakdownRows(
        IReadOnlyList<SourceDamage> stats, double combatSeconds, string rateLabel, int max = int.MaxValue)
    {
        if (stats.Count == 0) return [];
        var grand = Math.Max(1, stats.Sum(d => d.Total));
        var top = Math.Max(1, stats.Max(d => d.Total));
        var secs = Math.Max(1, combatSeconds);
        return stats.Take(max).Select(d => new HistoryBreakdownRow(
            d.Name,
            $"{d.Total:N0} · ×{d.Hits} · avg {(double)d.Total / Math.Max(1, d.Hits):0.#}" +
                $" · {d.Total / secs:0.#} {rateLabel}" +
                (d.Crits > 0 ? $" · {100.0 * d.Crits / Math.Max(1, d.Hits):0}% crit" : ""),
            (double)d.Total / top,
            $"{100.0 * d.Total / grand:0.#}% of total · {rateLabel} = total ÷ {secs:0}s in combat" +
                (d.ActiveSeconds > 0
                    ? $" · burst {d.Total / Math.Max(1, d.ActiveSeconds):0.#}/s over the ~{d.ActiveSeconds:0}s it was in use"
                    : ""))).ToList();
    }

    public static string BuildComparison(SessionRow firstRow, StatsSnapshot? firstSnapshot,
        SessionRow secondRow, StatsSnapshot? secondSnapshot)
    {
        if (firstSnapshot is null || secondSnapshot is null) return MissingComparisonText;

        var text = new StringBuilder();
        text.AppendLine("SESSION COMPARISON");
        text.AppendLine($"A: {firstRow.Character} - {firstRow.StartLocal:MMM d h:mm tt} - {firstRow.PrimaryZone}");
        text.AppendLine($"B: {secondRow.Character} - {secondRow.StartLocal:MMM d h:mm tt} - {secondRow.PrimaryZone}");
        if (firstRow.Character != secondRow.Character || firstRow.PrimaryZone != secondRow.PrimaryZone)
            text.AppendLine("(different character/zone - rates may not compare directly)");
        text.AppendLine();
        text.AppendLine($"{"",-16}{"A",14}{"B",14}");
        void AddRow(string label, string first, string second) => text.AppendLine($"{label,-16}{first,14}{second,14}");
        AddRow("Duration", $"{firstRow.ElapsedSeconds / 3600:0.0}h", $"{secondRow.ElapsedSeconds / 3600:0.0}h");
        AddRow("Active", $"{firstRow.ActiveSeconds / 60:0}m", $"{secondRow.ActiveSeconds / 60:0}m");
        AddRow("XP/hr", $"{firstSnapshot.XpPerHour:0.0}%", $"{secondSnapshot.XpPerHour:0.0}%");
        AddRow("Kills/hr", $"{firstSnapshot.KillsPerHour:0.0}", $"{secondSnapshot.KillsPerHour:0.0}");
        AddRow("Money/hr", StatsSnapshot.FormatCoin(firstSnapshot.CopperPerHour), StatsSnapshot.FormatCoin(secondSnapshot.CopperPerHour));
        AddRow("DPS", $"{firstSnapshot.SessionDps:0.0}", $"{secondSnapshot.SessionDps:0.0}");
        AddRow("HPS", $"{firstSnapshot.Hps:0.0}", $"{secondSnapshot.Hps:0.0}");
        AddRow("Damage taken", $"{firstSnapshot.DamageTaken:N0}", $"{secondSnapshot.DamageTaken:N0}");
        AddRow("Deaths", $"{firstSnapshot.Deaths.Count}", $"{secondSnapshot.Deaths.Count}");
        AddRow("Loot items", $"{firstSnapshot.LootTotal}", $"{secondSnapshot.LootTotal}");
        return text.ToString();
    }

    public static string BuildImporting(string path) => $"Importing {Path.GetFileName(path)}...";

    public static HistoryImportResult BuildImportResult(string path, int importedSessions)
    {
        var fileName = Path.GetFileName(path);
        return new HistoryImportResult(fileName, importedSessions,
            $"Imported {importedSessions} session{(importedSessions == 1 ? "" : "s")} from {fileName}.");
    }

    public static string BuildExportFileName(SessionRow row) =>
        $"eqbuddy-{row.Character}-{row.StartLocal:yyyyMMdd-HHmm}.json";

    public static string BuildExportJson(StatsSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });

    private static string ShareBar(double fraction) =>
        new('█', Math.Clamp((int)Math.Round(fraction * 10), 1, 10));
}
