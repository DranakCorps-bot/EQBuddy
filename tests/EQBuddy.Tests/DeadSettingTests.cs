using System.Text.RegularExpressions;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// A setting that only READERS touch is the signature of a lost capability.
///
/// It has cost three player-facing bugs, each looking like something different:
///
///  * <c>SkyQuestCompleted</c> — you could not tell EQBuddy you had turned a Plane of Sky
///    reward in (#204, #209). Fixed 1.92.0.
///  * <c>EpicQuestCompleted</c> — "Epic complete" had no writer anywhere, and the helper
///    that did the work had passing tests and NO CALLER (#210). Fixed 1.93.0.
///  * <c>SkyQuestClass</c> — EQBuddy Mobile scoped its whole Sky list by it, so a value
///    last saved before 2026-08-16 filtered the phone forever and nothing could change it
///    (#212). Fixed 1.93.2.
///
/// Every one of them came from the same event: a surface was folded into another, the
/// DATA survived the move and the WRITE path did not. No test and no ratchet could see
/// it, because everything still compiled and every existing test still passed.
///
/// So this is the ratchet for that class of bug. It scans the source for settings that are
/// read and never written, and holds the result to a known list with a reason on each
/// entry. A NEW writer-less setting fails the build; removing one means deleting its line.
///
/// **What it cannot see:** a collection mutated through a helper that takes it by
/// reference (<c>BuffSetStore.Add(settings.BuffSetsByClass, …)</c>) looks writer-less
/// here. Those live in the list with that as their reason rather than being special-cased,
/// because a heuristic that tried to spot them would also start hiding real ones.
/// </summary>
public class DeadSettingTests
{
    private static string Src => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));

    private static string SettingsFile => Path.Combine(Src, "EQBuddy.Core", "AppSettings.cs");

    /// <summary>Settings with no writer today, and WHY that is or is not acceptable.
    /// Adding a line here is a deliberate act — read the class note first.</summary>
    private static readonly Dictionary<string, string> Known = new(StringComparer.Ordinal)
    {
        // Mutated through a helper that takes the collection by reference — invisible to
        // this scan, genuinely written at runtime.
        ["BuffSetsByClass"] = "BuffSetStore.Add/Remove mutate it in place",
        ["BuffSuggestionDismissed"] = "BuffSuggestions.Dismiss mutates it in place",
        ["EpicQuestChecklist"] = "rows are mutated item-by-item; seeded in AppSettings",
        ["SkyQuestChecklist"] = "rows are mutated item-by-item; seeded in AppSettings",

        // The second half of a tuple deconstruction — "(s.WindowLeft, s.WindowTop) = …".
        ["WindowTop"] = "written with WindowLeft by tuple deconstruction",
        ["QuestsTop"] = "written with QuestsLeft by tuple deconstruction",
        ["ProgressTop"] = "written with ProgressLeft by tuple deconstruction",
        ["GearLootTop"] = "written with GearLootLeft by tuple deconstruction",
        ["CreatureTop"] = "written with CreatureLeft by tuple deconstruction",
        ["SpawnTop"] = "written with SpawnLeft by tuple deconstruction",
        ["TimelineTop"] = "written with TimelineLeft by tuple deconstruction",

        // GENUINELY writer-less, and each one is a deliberate decision rather than a
        // lost capability. Both class lenses lost their writer when the 2026-08-16
        // consolidation deleted the widget's Sky and Epic cards; both auto-checkers
        // already treat an empty or stale value as "no class passes" rather than as a
        // wildcard, which is the #193 fix, and Mobile stopped filtering on SkyQuestClass
        // in 1.93.2 (#212). They are read-only leftovers now, not switches.
        ["SkyQuestClass"] = "#193/#212 — no writer since 2026-08-16; every reader guards it",
        ["EpicQuestClass"] = "#193 twin — no writer since 2026-08-16; EpicLootAutoCheck guards it",

        // Power-user knobs: real settings with no UI, changed by editing settings.json.
        // Listed so that stays a decision someone made rather than something nobody
        // noticed. If any of these deserves an Options control, that is a feature.
        ["CursorRingSize"] = "no UI by design — edit settings.json",
        ["CompanionPort"] = "no UI by design — edit settings.json",
        ["CompanionHiddenSurfaces"] = "no UI by design — edit settings.json",
        ["UpdateFolder"] = "no UI by design — edit settings.json",
        ["WineFloatOverFullscreen"] = "no UI by design — Wine/Proton escape hatch",
        ["WineKeepGameFullscreen"] = "no UI by design — Wine/Proton escape hatch",
    };

    [Fact]
    public void NoSettingGainsReadersWithoutAWriter()
    {
        var found = WriterLess();
        var unexpected = found.Where(p => !Known.ContainsKey(p)).ToList();

        Assert.True(unexpected.Count == 0,
            "These settings are READ but never written, and are not on the known list:"
            + Environment.NewLine + string.Join(Environment.NewLine, unexpected)
            + Environment.NewLine + Environment.NewLine
            + "A setting only readers touch is how a capability goes missing without "
            + "anything failing — it has cost three player-facing bugs (#204, #210, #212). "
            + "Either wire the write path, or add it to DeadSettingTests.Known with the "
            + "reason it is acceptable.");
    }

    [Fact]
    public void TheKnownListDoesNotRot()
    {
        // An entry that has GAINED a writer should be deleted, or the list slowly stops
        // describing the codebase and the test above stops meaning anything.
        var found = new HashSet<string>(WriterLess(), StringComparer.Ordinal);
        var stale = Known.Keys.Where(k => !found.Contains(k)).ToList();

        Assert.True(stale.Count == 0,
            "These are on the known writer-less list but now HAVE a writer (or were "
            + "renamed). Delete their lines from DeadSettingTests.Known:"
            + Environment.NewLine + string.Join(Environment.NewLine, stale));
    }

    // ---- the scan ----

    private static List<string> WriterLess()
    {
        var sources = Directory
            .EnumerateFiles(Src, "*.*", SearchOption.AllDirectories)
            .Where(f => Path.GetExtension(f) is ".cs" or ".xaml")
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => !f.Equals(SettingsFile, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(f => f, File.ReadAllText);

        var result = new List<string>();
        foreach (var name in Properties())
        {
            var read = sources.Values.Any(t => Regex.IsMatch(t, $@"\.{name}\b"));
            if (!read) continue;                     // unread is a different problem
            if (sources.Values.Any(t => Written(t, name))) continue;
            result.Add(name);
        }
        return result;
    }

    private static bool Written(string text, string name) =>
        Regex.IsMatch(text, $@"\.{name}\s*(?:=[^=]|\+=|-=|\?\?=)")          // direct
        || Regex.IsMatch(text, $@"\.{name}\s*[,)][^;\n]*\)\s*=")            // tuple
        || Regex.IsMatch(text, $@"\.{name}\s*\.\s*(?:Add|AddRange|Remove|RemoveAll|Clear|Insert)\s*\(")
        || Regex.IsMatch(text, $@"\.{name}\s*\[[^\]]*\]\s*=")               // indexer
        || Regex.IsMatch(text, $@"\b{name}\s*=\s*");                        // object initializer

    /// <summary>AppSettings' OWN settable properties — nested types declared in the same
    /// file (the checklist item records) have their own and are not settings.</summary>
    private static List<string> Properties()
    {
        var text = File.ReadAllText(SettingsFile);
        var start = text.IndexOf("public sealed class AppSettings", StringComparison.Ordinal);
        Assert.True(start >= 0, "AppSettings class not found — did it get renamed?");

        var open = text.IndexOf('{', start);
        var depth = 0;
        var end = -1;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0) { end = i; break; }
        }
        Assert.True(end > open, "Could not find the end of the AppSettings class body.");

        var body = Regex.Replace(text[open..end],
            @"public (?:sealed |static )?class \w+[\s\S]*?\n    \}", "");
        var props = Regex.Matches(body, @"public\s+[\w<>,\[\]\?\s]+?\s+(\w+)\s*\{\s*get;\s*set;")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(props.Count > 100,
            $"Only {props.Count} settings parsed — the scan is broken, not the codebase.");
        return props;
    }
}
