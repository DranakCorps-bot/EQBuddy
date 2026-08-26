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
        /// <summary>A contribution that is NOT loot (Bevel, Helm-signed 2026-08-23): the
        /// game's own /consider called this creature rare, its page already has every
        /// drop it produced (or it dropped only motes), and the description-field paste
        /// is the whole edit. Its own kind rather than a reuse of
        /// <see cref="PageHasNoLoot"/>/<see cref="NewToPage"/>, because those claim the
        /// page is missing loot and this claims nothing about the page at all — EQBuddy
        /// cannot read the description, so the row's tip says ADD, never replace.</summary>
        RareConfirmed,
        /// <summary>The title resolved to an article that is not a creature page at all.
        /// NOT a contribution — it sits with <see cref="Pending"/> at the bottom — but it
        /// has to be SHOWN, because it is the one row the player can act on and EQBuddy
        /// cannot: only a person can say which page the creature actually lives on.</summary>
        NotACreaturePage,
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
        int KnownDrops,
        /// <summary>Creatures whose title resolved to something that is not a creature page.
        /// They contribute nothing, so without their own count the headline and the empty
        /// state would call the session "nothing to contribute" — which is the one thing a
        /// wrong-article session must never be told (Bevel, Helm-signed 2026-08-22). Zero
        /// contributions and no problem are not the same sentence.</summary>
        int WrongArticleCreatures,
        /// <summary>Creatures that DID drop something, but nothing the pack may suggest —
        /// in practice a session whose only loot was motes. Counted separately because the
        /// alternative is telling the player "no loot recorded this session yet", which is
        /// false and reads as EQBuddy having missed their kills (Fable 5, v1.99.5 review).
        /// </summary>
        int NothingSuggestableCreatures,
        /// <summary>Creatures whose only contribution is the con-rarity fact — no loot the
        /// wiki lacks, but the game itself said "a rare creature" and the page can carry
        /// that. Its own count because it is not an ITEM: folding it into
        /// <see cref="Contributions"/> would make the headline claim drops that do not
        /// exist, and ignoring it would let the headline say "nothing to contribute" over
        /// a row that plainly contributes (Bevel's #226 rule: two different states must
        /// not read alike).</summary>
        int RareOnlyCreatures = 0);

    /// <summary>The status words, once, so the two desktops cannot spell them differently.</summary>
    public static string KindLabel(RowKind kind) => kind switch
    {
        RowKind.PageMissing => "no wiki page",
        RowKind.PageHasNoLoot => "page lists no loot",
        RowKind.NewToPage => "new to the page",
        RowKind.RareConfirmed => "rare spawn confirmed",
        RowKind.NotACreaturePage => "not a creature page",
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
        RowKind.RareConfirmed =>
            "The game's own /consider called this creature \"a rare creature\", and its " +
            "page already has every drop it produced — so the rarity IS the contribution. " +
            "The paste goes in the page's description field (the wiki's stopgap until " +
            "{{Namedmobpage}} grows a rare-spawn field, confirmed with the admins on " +
            "#217), and it is an ADD: EQBuddy cannot read what the description already " +
            "says, so never replace what is written there.",
        RowKind.NotACreaturePage =>
            "The wiki answered with an article that is not a creature page — no " +
            "{{Namedmobpage}} on it — so this is almost certainly the wrong page for the " +
            "creature, not an empty one. Innoruk is the reported example: the check lands " +
            "on the Lore article (#226, LeBigNasty). Nothing is suggested for it, because " +
            "pasting a loot table onto a lore page would be worse than adding nothing. " +
            "Open it, find the creature's own page, and the next re-check will follow it.",
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
        RowKind.RareConfirmed => "AccentBrush",
        // Not dim: this is the one row that needs a person to look at it.
        RowKind.NotACreaturePage => "BadBrush",
        _ => "DimBrush",
    };

    /// <summary>Build the pack view from the same observations <see cref="WikiContribution.BuildExport"/>
    /// consumes, so what you see is what you copy.</summary>
    public static Pack Build(IEnumerable<WikiContribution.MobObservation> observations)
    {
        var rows = new List<PackRow>();
        int pagesMissing = 0, pagesNoLoot = 0, newDrops = 0, pending = 0, known = 0;
        var nothingSuggestable = 0;
        var rareOnly = 0;

        // The rare-only row, in ONE place for the two branches that used to drop the fact
        // (all-loot-known, and motes-only). Mirrors WikiContribution.BuildExport's own
        // check so what you see is what you copy.
        bool TryRareRow(MobSummary mob, MobLookupResult? lookup)
        {
            if (!WikiContribution.EarnsRareOnlyRow(mob, lookup)) return false;
            rareOnly++;
            rows.Add(new PackRow(mob.Name, mob.Kills, RowKind.RareConfirmed, 0,
                WikiContribution.RareSpawnRowNote(mob) ?? "rare"));
            return true;
        }

        foreach (var (mob, lookup) in observations.Select(o => (o.Mob, o.Lookup)))
        {
            if (mob.Loot.Count == 0) continue;

            // Motes are never suggested to a creature page, so they must not colour the
            // row either — a creature whose only "new" drop was a mote is not a
            // contribution (WikiContribution.SuggestableToWiki).
            var suggestable = mob.Loot
                .Where(l => WikiContribution.SuggestableToWiki(l.Item))
                .ToList();
            // It DID drop something; there is just nothing here a creature page should
            // carry — unless the game conned it rare, which is a contribution of its own.
            if (suggestable.Count == 0)
            {
                if (!TryRareRow(mob, lookup)) nothingSuggestable++;
                continue;
            }

            var classified = suggestable
                .Select(l => WikiContribution.Classify(lookup, l.Item))
                .ToList();

            known += classified.Count(s => s == WikiDropStatus.Known);

            // WRONG ARTICLE, before anything else. It contributes nothing, so without its
            // own row it would fall through the "nothing to contribute" branch below and
            // vanish entirely — a creature silently absent from the pack, which reads as
            // "nothing new here" and is the opposite of what happened.
            if (classified.Contains(WikiDropStatus.PageIsNotACreature))
            {
                rows.Add(new PackRow(mob.Name, mob.Kills, RowKind.NotACreaturePage, 0,
                    lookup?.Mob?.PageTitle is { Length: > 0 } served
                        ? $"read \"{served}\"" : "wrong page"));
                continue;
            }

            var news = classified.Count(s => s is WikiDropStatus.NewToPage
                or WikiDropStatus.PageHasNoLoot or WikiDropStatus.PageMissing);

            if (news == 0)
            {
                // Nothing LOOT can contribute. A row only if the reason is "we could not
                // look" (never claimed as "nothing new"), or if the game conned it rare —
                // the fact this kind exists to stop being dropped. Unknown first: an
                // unread page gets no claim of any kind, rare included.
                if (classified.Any(s => s == WikiDropStatus.Unknown))
                {
                    pending++;
                    rows.Add(new PackRow(mob.Name, mob.Kills, RowKind.Pending, 0,
                        "wiki page not read"));
                }
                else
                {
                    TryRareRow(mob, lookup);
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
            // Not the rare-only rows: this count feeds "N items across M creatures", and a
            // creature contributing zero items would make that sentence claim drops that
            // do not exist. The rare rows have their own count and their own clause.
            Creatures: rows.Count(r => r.Kind is not RowKind.Pending and not RowKind.RareConfirmed),
            Contributions: rows.Sum(r => r.Contributions),
            PagesMissing: pagesMissing,
            PagesWithoutLoot: pagesNoLoot,
            NewDrops: newDrops,
            PendingCreatures: pending,
            KnownDrops: known,
            WrongArticleCreatures: rows.Count(r => r.Kind == RowKind.NotACreaturePage),
            NothingSuggestableCreatures: nothingSuggestable,
            RareOnlyCreatures: rareOnly);
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
        // A wrong-article session has zero contributions and is NOT "nothing to contribute" —
        // it is EQBuddy having read the wrong page. Checked before the zero, or the false
        // reassurance wins (Bevel, Helm-signed: "two failures must not look alike").
        if (pack.Contributions == 0 && pack.WrongArticleCreatures > 0)
            return pack.WrongArticleCreatures == 1
                ? "1 creature's wiki page isn't the creature"
                : $"{pack.WrongArticleCreatures} creatures' wiki pages aren't the creature";
        // A rare-only pack is a real contribution with zero ITEMS — the headline counts it
        // (Bevel, Helm-signed 2026-08-23) rather than calling the session empty.
        var rare = pack.RareOnlyCreatures switch
        {
            0 => "",
            1 => "1 rare-spawn confirmation",
            var n => $"{n} rare-spawn confirmations",
        };
        if (pack.Contributions == 0)
            return rare.Length > 0 ? $"{rare} for the wiki" : "Nothing to contribute yet";
        var items = pack.Contributions == 1 ? "1 item" : $"{pack.Contributions} items";
        var mobs = pack.Creatures == 1 ? "1 creature" : $"{pack.Creatures} creatures";
        return $"{items} across {mobs} the wiki doesn't have"
            + (rare.Length > 0 ? $" · {rare}" : "");
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
        if (pack.RareOnlyCreatures > 0)
            parts.Add($"{pack.RareOnlyCreatures} confirmed rare via /consider");
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

        if (pack.WrongArticleCreatures > 0)
            return $"EQBuddy looked up {Creatures(pack.WrongArticleCreatures)} and got an " +
                "article that is not a creature page — a lore or deity entry, not an empty " +
                "page. Nothing is suggested for those, because pasting a loot table onto the " +
                "wrong article would be worse than adding nothing. Open the row to see which " +
                "page it read, then find the creature's own page.";

        if (pack.NothingSuggestableCreatures > 0)
            return $"You looted from {Creatures(pack.NothingSuggestableCreatures)} this " +
                "session, but only motes — and motes drop from everything, so they do not " +
                "belong on any creature's page. That is the pack having nothing to suggest, " +
                "not EQBuddy having missed your kills.";

        return "No loot recorded this session yet. Kill something and come back — the pack " +
            "builds itself from your own loot log.";
    }

    private static string Creatures(int n) => n == 1 ? "1 creature" : $"{n} creatures";

    /// <summary>Copy is offered only when there is something to paste. A button that copies
    /// a header and nothing else is a silent no-op. A rare-only pack has a paste — the
    /// description-field ADD — so it copies too.</summary>
    public static bool CanCopy(Pack pack) => pack.Contributions > 0 || pack.RareOnlyCreatures > 0;

    public static string CopyTip(Pack pack) => CanCopy(pack)
        ? "Copy paste-ready eqlwiki edits for everything listed, each with a direct edit " +
          "link. Nothing publishes automatically: you open the link, paste, review and save."
        : "Nothing to copy yet — the pack is empty.";

    // ---------------- the re-check (#226; plan in FABLE.md, Fable 5) ----------------

    /// <summary>The creatures the pack's "Re-check" button re-reads: every one the pack
    /// is about to CLAIM something for, or could not read — never a creature whose page
    /// already has all of it. Bounded on purpose (etiquette toward a volunteer wiki): the
    /// flagged creatures, not the whole session.</summary>
    public static IReadOnlyList<string> RecheckTargets(IEnumerable<WikiContribution.MobObservation> observations)
    {
        var targets = new List<string>();
        foreach (var (mob, lookup) in observations.Select(o => (o.Mob, o.Lookup)))
        {
            if (mob.Loot.Count == 0) continue;
            var allKnown = mob.Loot
                .Where(l => WikiContribution.SuggestableToWiki(l.Item))
                .All(l => WikiContribution.Classify(lookup, l.Item) == WikiDropStatus.Known);
            if (!allKnown) targets.Add(mob.Name);
        }
        return targets;
    }

    public static string RecheckLabel(int targets) => targets switch
    {
        0 => "Nothing to re-check",
        1 => "Re-check 1 page",
        _ => $"Re-check {targets} pages",
    };

    /// <summary>While it runs, the button itself reports progress through the window's
    /// existing 3 s tick — "checking 3 of 9…" — so a slow wiki reads as working, not stuck.</summary>
    public static string RecheckProgress(int inFlight, int total) =>
        $"checking {Math.Max(0, total - inFlight) + 1} of {total}\u2026";

    public static bool CanRecheck(int targets, bool running) => targets > 0 && !running;

    public static string RecheckTip(int targets, bool running) => running
        ? "Reading the flagged pages again now."
        : targets == 0
            ? "Every creature here is already fully on the wiki — nothing to re-read."
            : "Read the flagged creatures' wiki pages again now, past the 7-day cache — after " +
              "you fix a page, this is how the pack catches up. At most two requests at a time.";

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
