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
               (row.EndReason == "RecoveredAfterCrash" ? " - (recovered)" : "") +
               (row.EndReason == "Active" ? " - (in progress)" : "");
    }

    public static string BuildOverview(SessionRow row, StatsSnapshot snapshot)
    {
        var text = new StringBuilder();
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

        if (snapshot.DamageBySource.Count > 0)
        {
            text.AppendLine("Top damage sources:");
            var grandTotal = Math.Max(1, snapshot.DamageBySource.Sum(source => source.Total));
            var topTotal = Math.Max(1, snapshot.DamageBySource.Max(source => source.Total));
            foreach (var source in snapshot.DamageBySource.Take(8))
                text.AppendLine($"  {source.Name,-24} {ShareBar((double)source.Total / topTotal),-10} {source.Total,8:N0}" +
                    $" - {100.0 * source.Total / grandTotal,3:0}% - {source.Hits} hits - avg {(double)source.Total / Math.Max(1, source.Hits):0.#}" +
                    $" - {source.Total / Math.Max(1, snapshot.CombatSeconds):0.#} dps" +
                    (source.Crits > 0 ? $" - {100.0 * source.Crits / Math.Max(1, source.Hits):0}% crit" : ""));
            text.AppendLine();
        }

        if (snapshot.HealsBySpell.Count > 0)
        {
            text.AppendLine("Top heals:");
            var grandTotal = Math.Max(1, snapshot.HealsBySpell.Sum(heal => heal.Total));
            var topTotal = Math.Max(1, snapshot.HealsBySpell.Max(heal => heal.Total));
            foreach (var heal in snapshot.HealsBySpell.Take(6))
                text.AppendLine($"  {heal.Name,-24} {ShareBar((double)heal.Total / topTotal),-10} {heal.Total,8:N0}" +
                    $" - {100.0 * heal.Total / grandTotal,3:0}% - {heal.Hits} cast{(heal.Hits == 1 ? "" : "s")}" +
                    $" - avg {(double)heal.Total / Math.Max(1, heal.Hits):0.#}" +
                    $" - {heal.Total / Math.Max(1, snapshot.CombatSeconds):0.#} hps");
            text.AppendLine();
        }

        if (snapshot.YourKills.Count > 0)
        {
            text.AppendLine("Kills by creature:");
            foreach (var kill in snapshot.YourKills.Take(10))
                text.AppendLine($"  {kill.Name,-28} x{kill.Count}");
            text.AppendLine();
        }

        if (snapshot.Loot.Count > 0)
        {
            text.AppendLine("Loot:");
            foreach (var loot in snapshot.Loot.Take(15))
                text.AppendLine($"  {loot.Item,-34} x{loot.Count}");
            text.AppendLine();
        }

        var farmed = snapshot.Mobs.Where(mob => mob.Kills > 0).Take(8).ToList();
        if (farmed.Count > 0)
        {
            text.AppendLine("Mob farming (observed personal rates):");
            foreach (var mob in farmed)
            {
                text.AppendLine($"  {mob.Name} - {mob.Kills} kills - avg fight {mob.AvgFightSeconds:0}s - " +
                                $"{mob.XpPercent:0.0}% xp - {StatsSnapshot.FormatCoin(mob.Copper)}");
                foreach (var loot in mob.Loot.Take(4))
                    text.AppendLine($"      {loot.Item,-30} x{loot.Count}" +
                        (loot.DropRatePct is { } percent ? $"  {percent:0.#}% ({loot.Count}/{mob.Kills})" : ""));
            }
            text.AppendLine();
        }

        if (snapshot.Stances.Count > 0)
            text.AppendLine("Stances: " + string.Join(" - ",
                snapshot.Stances.Select(stance => $"{stance.Name} {stance.Damage:N0} dmg over {(int)stance.CombatSeconds}s ({stance.Dps:0.#} dps)")));
        if (snapshot.Zones.Count > 0)
            text.AppendLine("Zones: " + string.Join(" -> ", snapshot.Zones.Select(zone => zone.Text)));
        if (snapshot.Markers.Count > 0)
            text.AppendLine("Markers: " + string.Join(" - ", snapshot.Markers.Select(marker => $"{marker.Text} ({marker.Time:h:mm tt})")));
        return text.ToString();
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
