using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// What the Wiki contribution pack surface SHOWS before you copy it (#217,
/// Frankthetankk — Ask 1).
///
/// <see cref="WikiContribution"/> already owned the paste blocks. What did not exist was
/// anything that could tell you what the pack CONTAINS: the command was a single button
/// inside Drops by Creature, and the only reason its scope was legible was that you were
/// looking at the session's drop list while you pressed it. Frankthetankk's argument for
/// moving it out is right — it stopped being a loot feature several releases ago — but a
/// bare menu command would have exported a silent scope, which is the same shape as
/// CLAUDE.md's "silent no-ops are broken" with the switch on the other side.
///
/// So the move comes with a window, and this is what that window says. It is framework-free
/// like the rest of UI.Shared, so it is unit-tested with no window and BOTH desktops compose
/// the same rules rather than a hand-copied approximation.
///
/// The one honesty rule carried over verbatim from <see cref="WikiContribution.BuildExport"/>:
/// a creature whose wiki lookup has not landed is NOT the same as a creature with nothing
/// new, and the surface must never let the two read alike. That is <see cref="RowKind.Pending"/>,
/// and it is counted separately everywhere below.
/// </summary>
public static class WikiPackPresentation
{
    /// <summary>Why a creature is in the pack — ordered by how much the edit is worth to
    /// the wiki, which is also the order the rows are shown in. A missing page is the
    /// biggest contribution of the three; <see cref="Pending"/> is not a contribution at
    /// all and always sinks to the bottom.</summary>
    public enum RowKind
    {
        PageMissing,
        PageHasNoLoot,
        NewToPage,
        Pending,
    }

    /// <summary>One creature's line in the pack.</summary>
    /// <param name="Contributions">Drops on this creature the wiki does not have. Zero
    /// for <see cref="RowKind.Pending"/> — nothing is claimed about a page we could not read.</param>
    public readonly record struct PackRow(
        string Creature, int Kills, RowKind Kind, int Contributions, string Note);

    /// <summary>Everything the window needs, derived once.</summary>
    /// <param name="KnownDrops">Drops the wiki already lists. Not a contribution, but the
    /// number that makes a small pack read as "the wiki is in good shape here" rather
    /// than "EQBuddy found nothing".</param>
    public sealed record Pack(
        IReadOnlyList<PackRow> Rows,
        int Creatures,
        int Contributions,
        int PagesMissing,
        int PagesWithoutLoot,
        int NewDrops,
        int PendingCreatures,
        int KnownDrops);

    /// <summary>The status words, once, so the two desktops cannot spell them differently.</summary>
    public static string KindLabel(RowKind kind) => kind switch
    {
        RowKind.PageMissing => "no wiki page",
        RowKind.PageHasNoLoot => "page lists no loot",
        RowKind.NewToPage => "new to the page",
        _ => "not checked yet",
    };

    public static string KindTip(RowKind kind) => kind switch
    {
        RowKind.PageMissing =>
            "eqlwiki has no page for this creature at all — the biggest contribution of the three.",
        RowKind.PageHasNoLoot =>
            "The page exists but records no loot, so everything you looted is news to it.",
        RowKind.NewToPage =>
            "The page exists and lists loot, but not these items.",
        _ =>
            "EQBuddy could not read this creature's wiki page — the lookup is still in " +
            "flight, or the wiki was unreachable. This is NOT the same as \"nothing new\": " +
            "nothing is claimed either way, and it is left out of the pack.",
    };

    /// <summary>The row's icon, by name in <see cref="IconPaths"/> — a vector, never a
    /// glyph (§4, PRs #148/#166). "Sparkle" is deliberate continuity: it is the shape the
    /// ✦ marker in Drops by Creature has always meant, drawn at a size the app controls
    /// and on a Wine prefix that cannot render the dingbat.</summary>
    public static string KindIcon(RowKind kind) =>
        kind == RowKind.Pending ? "Timer" : "Sparkle";

    /// <summary>A palette key, so the eye can sort the rows before it reads them. Pending
    /// goes dim on purpose — it is the one row that is NOT a contribution.</summary>
    public static string KindInk(RowKind kind) => kind switch
    {
        RowKind.PageMissing => "GoodBrush",
        RowKind.PageHasNoLoot => "AccentBrush",
        RowKind.NewToPage => "AccentBrush",
        _ => "DimBrush",
    };

    /// <summary>Build the pack view from the same observations <see cref="WikiContribution.BuildExport"/>
    /// consumes, so what you see is what you copy.</summary>
    public static Pack Build(IEnumerable<WikiContribution.MobObservation> observations)
    {
        var rows = new List<PackRow>();
        int pagesMissing = 0, pagesNoLoot = 0, newDrops = 0, pending = 0, known = 0;

        foreach (var (mob, lookup) in observations.Select(o => (o.Mob, o.Lookup)))
        {
            if (mob.Loot.Count == 0) continue;

            var classified = mob.Loot
                .Select(l => WikiContribution.Classify(lookup, l.Item))
                .ToList();

            known += classified.Count(s => s == WikiDropStatus.Known);

            var news = classified.Count(s => s is WikiDropStatus.NewToPage
                or WikiDropStatus.PageHasNoLoot or WikiDropStatus.PageMissing);

            if (news == 0)
            {
                // Nothing to contribute. Only worth a row if the reason is "we could not
                // look", never if the reason is "the wiki already has it".
                if (classified.Any(s => s == WikiDropStatus.Unknown))
                {
                    pending++;
                    rows.Add(new PackRow(mob.Name, mob.Kills, RowKind.Pending, 0,
                        "wiki page not read"));
                }
                continue;
            }

            // A creature's kind is decided by its page, not by its items, so the first
            // contributable status is representative of all of them.
            var kind = classified.Contains(WikiDropStatus.PageMissing) ? RowKind.PageMissing
                : classified.Contains(WikiDropStatus.PageHasNoLoot) ? RowKind.PageHasNoLoot
                : RowKind.NewToPage;

            switch (kind)
            {
                case RowKind.PageMissing: pagesMissing++; break;
                case RowKind.PageHasNoLoot: pagesNoLoot++; break;
                default: newDrops++; break;
            }

            rows.Add(new PackRow(mob.Name, mob.Kills, kind, news,
                news == 1 ? "1 item" : $"{news} items"));
        }

        rows.Sort((a, b) =>
        {
            var byKind = a.Kind.CompareTo(b.Kind);
            if (byKind != 0) return byKind;
            var byCount = b.Contributions.CompareTo(a.Contributions);
            if (byCount != 0) return byCount;
            return string.Compare(a.Creature, b.Creature, StringComparison.OrdinalIgnoreCase);
        });

        return new Pack(rows,
            Creatures: rows.Count(r => r.Kind != RowKind.Pending),
            Contributions: rows.Sum(r => r.Contributions),
            PagesMissing: pagesMissing,
            PagesWithoutLoot: pagesNoLoot,
            NewDrops: newDrops,
            PendingCreatures: pending,
            KnownDrops: known);
    }

    /// <summary>The one line that stops the scope being silent — the whole reason this is a
    /// window and not a menu command. It names what was pooled, not what could be.</summary>
    public static string ScopeLine(string character, string server, DateTime? sessionStart)
    {
        var who = character.Length > 0
            ? server.Length > 0 ? $"{character} ({server})" : character
            : "this character";
        var since = sessionStart is { } s ? $", since {s:HH:mm}" : "";
        return $"This session only — {who}{since}. Kills from earlier sessions are not counted yet.";
    }

    /// <summary>The headline above the rows.</summary>
    public static string Headline(Pack pack)
    {
        if (pack.Contributions == 0) return "Nothing to contribute yet";
        var items = pack.Contributions == 1 ? "1 item" : $"{pack.Contributions} items";
        var mobs = pack.Creatures == 1 ? "1 creature" : $"{pack.Creatures} creatures";
        return $"{items} across {mobs} the wiki doesn't have";
    }

    /// <summary>The sub-line: where the value is concentrated, so a player can tell a pack
    /// worth pasting from a pack worth skipping without reading every row.</summary>
    public static string Breakdown(Pack pack)
    {
        var parts = new List<string>();
        if (pack.PagesMissing > 0)
            parts.Add($"{pack.PagesMissing} with no wiki page");
        if (pack.PagesWithoutLoot > 0)
            parts.Add($"{pack.PagesWithoutLoot} whose page lists no loot");
        if (pack.NewDrops > 0)
            parts.Add($"{pack.NewDrops} with new drops for an existing page");
        return string.Join(" · ", parts);
    }

    /// <summary>What an empty pack says — and it has three genuinely different causes that
    /// must never be worded the same. The pending case is the one that matters: telling a
    /// player "nothing new" when EQBuddy simply could not reach the wiki would send them
    /// away from a contribution they actually have.</summary>
    public static string EmptyText(Pack pack)
    {
        if (pack.PendingCreatures > 0 && pack.KnownDrops == 0)
            return $"EQBuddy hasn't been able to read the wiki pages for " +
                $"{Creatures(pack.PendingCreatures)} yet, so it can't tell you whether your " +
                "drops are news. Leave this open a moment, or check your connection — this " +
                "is not the same as \"nothing new\".";

        if (pack.KnownDrops > 0)
        {
            var pendingTail = pack.PendingCreatures > 0
                ? $" {Creatures(pack.PendingCreatures)} still haven't been checked."
                : "";
            return $"Everything you've looted this session is already on eqlwiki " +
                $"({pack.KnownDrops} {(pack.KnownDrops == 1 ? "drop" : "drops")} matched). " +
                $"That's the wiki being in good shape, not EQBuddy finding nothing.{pendingTail}";
        }

        return "No loot recorded this session yet. Kill something and come back — the pack " +
            "builds itself from your own loot log.";
    }

    private static string Creatures(int n) => n == 1 ? "1 creature" : $"{n} creatures";

    /// <summary>Copy is offered only when there is something to paste. A button that copies
    /// a header and nothing else is a silent no-op.</summary>
    public static bool CanCopy(Pack pack) => pack.Contributions > 0;

    public static string CopyTip(Pack pack) => CanCopy(pack)
        ? "Copy paste-ready eqlwiki edits for everything listed, each with a direct edit " +
          "link. Nothing publishes automatically: you open the link, paste, review and save."
        : "Nothing to copy yet — the pack is empty.";

    /// <summary>The standing footer. Says where the numbers come from and where they don't,
    /// because a rarity band from 10 kills and a rarity band from 200 are not the same claim.</summary>
    public const string Footer =
        "Built from your own loot log — observed personal drop rates, with the kill count as " +
        "the denominator. Rarity labels use eqlwiki's own bands and are only suggested from " +
        "10+ kills; thinner samples leave the label to the page's editors. Nothing is ever " +
        "sent anywhere: this prepares a paste and you decide what to save.";

    /// <summary>The window's title, once. Frankthetankk's rename: what this became is a
    /// contribution pipeline, and "Drops by Creature" stopped describing it releases ago.</summary>
    public const string Title = "Wiki contribution pack";

    /// <summary>The pointer left behind in Drops by Creature, so the button moving out is
    /// not a disappearance.</summary>
    public const string MovedHint =
        "Drops eqlwiki doesn't know yet are marked here. The paste-ready edits for them " +
        "are now under Data & imports → Wiki contribution pack.";
}
