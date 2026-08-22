using System.Text;

namespace EQBuddy.Core;

/// <summary>Where an observed drop stands against the creature's eqlwiki page.</summary>
public enum WikiDropStatus
{
    /// <summary>Lookup pending, or the wiki was unreachable — say nothing rather than guess.</summary>
    Unknown,
    /// <summary>The page already lists this item.</summary>
    Known,
    /// <summary>Page exists, item is not in its loot list — a meaningful update.</summary>
    NewToPage,
    /// <summary>Page exists but records no loot at all — the blank pages David kept
    /// meeting live ("the wiki page lists no drops yet"); everything looted is news.</summary>
    PageHasNoLoot,
    /// <summary>No page at all — the biggest contribution of the three.</summary>
    PageMissing,
    /// <summary>The title resolved to an article that is NOT a creature page — a lore or
    /// deity entry with no <c>{{Namedmobpage}}</c> (#226, LeBigNasty: *"Innoruk, for
    /// example, is checking against the Lore page and not against the creature page"*).
    ///
    /// Deliberately not <see cref="PageHasNoLoot"/>, which is what it used to be mistaken
    /// for: that status means "everything you looted is news to this page" and puts the
    /// creature in the contribution pack. Doing that here would offer to paste a loot
    /// table onto an article about a god. The two states parse identically — zero drops —
    /// and mean opposite things.</summary>
    PageIsNotACreature,
}

/// <summary>
/// Turns session drop observations into eqlwiki-ready contributions (discussion #65,
/// Frankthetankk). Copy/paste-first by design: nothing here talks to the wiki — it
/// classifies what's new against the pages EQBuddy already fetched for Target Drops,
/// and builds a "contribution pack" of paste blocks in the wiki's own house style
/// (surveyed from live {{Namedmobpage}} pages + eqlwiki Help:Contents, 2026-08-08),
/// each headed by a direct edit link. Auto-publish waits for the wiki admins' blessing.
///
/// Honesty rules: rarity labels use the wiki's published bands (Always 100% ·
/// Common ≥50% · Uncommon ≥25% · Rare ≥5% · Ultra Rare &lt;5%) and are suggested only
/// when the sample can carry them — 10+ kills, 20+ for "Always". Below that the span
/// is omitted and the editor decides; the observed numbers always ride along as a
/// suggested edit summary, denominator and all.
/// </summary>
public static class WikiContribution
{
    /// <summary>Tier suffixes fold ("Vest +2" → "Vest"), and backticks AND apostrophes
    /// drop — wikis strip both (the Skeleton L`rodd lesson, then Frankthetankk's ✦ on
    /// "Packmaster's Lash" when the page lists {{:Packmasters Lash}}, #65 test report).
    /// Then case-insensitive.</summary>
    private static string Fold(string item) =>
        QuestCatalog.BaseItemName(item).Replace("`", "").Replace("'", "").Replace("’", "");

    public static WikiDropStatus Classify(MobLookupResult? lookup, string item) => lookup switch
    {
        null => WikiDropStatus.Unknown,
        { State: ItemLookupState.Offline } => WikiDropStatus.Unknown,
        { State: ItemLookupState.NotFound } => WikiDropStatus.PageMissing,
        { Mob: null } => WikiDropStatus.Unknown,
        // BEFORE the no-loot case, which it would otherwise be swallowed by: both have
        // zero drops and only this one means "wrong article".
        { Mob.IsCreaturePage: false } => WikiDropStatus.PageIsNotACreature,
        { Mob.Drops.Count: 0 } => WikiDropStatus.PageHasNoLoot,
        { Mob: { } mob } => mob.Drops.Any(d =>
                string.Equals(Fold(d.Item), Fold(item), StringComparison.OrdinalIgnoreCase))
            ? WikiDropStatus.Known
            : WikiDropStatus.NewToPage,
    };

    /// <summary>
    /// Is this item worth SUGGESTING to a creature's wiki page?
    ///
    /// **Motes are not** (#217 Frankthetankk, #226 LeBigNasty; the wiki's own Mote Guide).
    /// They drop from everything, so listing one under a creature's `known_loot` is not a
    /// low-value edit — it is a WRONG one, and it would be wrong on every creature page a
    /// player ever pastes.
    ///
    /// **This is not the same question as the common-drops one, and the difference is why
    /// it can be decided here.** The wiki admins ruled that common or low-value drops stay
    /// IN the suggestion, and that ruling stands: a cheap gem really did drop from that
    /// creature. A mote did not come from that creature in any sense the page cares about.
    /// Frank drew the same line himself — omit-from-wiki versus hide-from-my-view — and the
    /// hide-from-my-view half is a separate display feature, not this.
    ///
    /// The player still sees their motes everywhere the app shows loot. This governs only
    /// what gets pasted onto somebody else's wiki.
    /// </summary>
    public static bool SuggestableToWiki(string itemName) => !Motes.IsMote(itemName);

    /// <summary>A wiki rarity label the observation can honestly support, or null when
    /// the sample is too thin to label (the editor decides; the numbers still travel
    /// in the edit summary).</summary>
    public static string? SuggestRarity(double? pct, int kills)
    {
        if (pct is not { } p || kills < 10) return null;
        if (p >= 100) return kills >= 20 ? "Always" : "Common";
        if (p >= 50) return "Common";
        if (p >= 25) return "Uncommon";
        if (p >= 5) return "Rare";
        return "Ultra Rare";
    }

    /// <summary>Parens and apostrophes are URL-legal and MediaWiki keeps them raw —
    /// "(Crushbone)" pages should paste as readable links, not %28 soup.</summary>
    public static string EditUrl(string pageTitle) =>
        "https://eqlwiki.com/index.php?title="
        + Uri.EscapeDataString(pageTitle.Trim().Replace(' ', '_'))
            .Replace("%28", "(").Replace("%29", ")").Replace("%27", "'")
        + "&action=edit";

    /// <summary>One creature's worth of input: the session summary plus whatever the
    /// Target-Drops lookup already knows about its wiki page (null = never looked up).</summary>
    public readonly record struct MobObservation(MobSummary Mob, MobLookupResult? Lookup);

    /// <summary>The paste-ready contribution pack. Only creatures with something the
    /// wiki doesn't know make the cut; creatures still Unknown are listed at the end
    /// so nobody mistakes "not checked" for "nothing new".</summary>
    public static string BuildExport(IEnumerable<MobObservation> observations,
        string character, string server, string currentZone, DateTime now)
    {
        var sb = new StringBuilder();
        var who = character.Length > 0 ? $"{character} ({server})" : "unknown character";
        sb.AppendLine($"EQBuddy → eqlwiki contribution pack — {who} — {now:yyyy-MM-dd HH:mm}");
        sb.AppendLine("Everything below was observed in your own loot log. Nothing publishes");
        sb.AppendLine("automatically: open each edit link, paste the block, review, save.");
        sb.AppendLine("Rarity labels use the wiki's own bands (Always 100% · Common ≥50% ·");
        sb.AppendLine("Uncommon ≥25% · Rare ≥5% · Ultra Rare <5%) and are only suggested from");
        sb.AppendLine("10+ kills — thinner samples leave the label to the page's editors.");

        var unknown = new List<string>();
        var wroteAny = false;
        foreach (var (mob, lookup) in observations.Select(o => (o.Mob, o.Lookup)))
        {
            if (mob.Loot.Count == 0) continue;
            var news = mob.Loot
                .Where(l => SuggestableToWiki(l.Item))
                .Select(l => (Loot: l, Status: Classify(lookup, l.Item)))
                .Where(x => x.Status is WikiDropStatus.NewToPage
                    or WikiDropStatus.PageHasNoLoot or WikiDropStatus.PageMissing)
                .ToList();
            if (news.Count == 0)
            {
                if (mob.Loot.Any(l => Classify(lookup, l.Item) == WikiDropStatus.Unknown))
                    unknown.Add(mob.Name);
                continue;
            }
            wroteAny = true;
            WriteMobSection(sb, mob, lookup, news, currentZone);
        }

        if (!wroteAny)
        {
            sb.AppendLine();
            sb.AppendLine(unknown.Count > 0
                ? "Nothing confirmed new yet — some creatures are still being checked against the wiki."
                : "Everything you looted this session is already on the wiki. Nothing to contribute — nice when that happens.");
        }
        if (unknown.Count > 0 && wroteAny)
        {
            sb.AppendLine();
            sb.AppendLine($"Not checked against the wiki yet (lookup pending or wiki offline): {string.Join(", ", unknown)}.");
        }
        return sb.ToString();
    }

    private static void WriteMobSection(StringBuilder sb, MobSummary mob,
        MobLookupResult? lookup, List<(MobLoot Loot, WikiDropStatus Status)> news,
        string currentZone)
    {
        var status = news[0].Status;
        // Upgrade tiers fold to one wiki entry (#65, Frankthetankk's catch): the
        // {{:Item}} transclusion drops the "+N" suffix, so base and +1 rows produced
        // two IDENTICAL <li> lines. One line per base item; the first row (the
        // tier-merged observation order) speaks for the family.
        news = news
            .GroupBy(n => QuestCatalog.BaseItemName(n.Loot.Item), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
        // A resolved page keeps its real title (edit links must hit the page that
        // answered, "(Zone)" suffix and all); a missing page is created at the
        // observed name — named mobs live at bare names per the wiki's own habit.
        var pageTitle = lookup?.Mob?.PageTitle is { Length: > 0 } t ? t : mob.Name;
        // Where this creature was actually killed — NOT where the player happens to be
        // standing when the pack is generated. The new-page template used currentZone
        // while the cross-references and stat block used this, so one entry could claim
        // two different zones and publish the wrong one (#65, Frankthetankk).
        var killZone = mob.Zone.Length > 0 ? mob.Zone : currentZone;

        sb.AppendLine();
        // The section headline wears the WIKI'S title when the page resolved — the
        // log normalizer strips articles ("a decaying skeleton" → "Decaying
        // skeleton"), and a paste block titled with the stripped name suggests the
        // wrong page name (#65 round four, Frankthetankk).
        sb.AppendLine($"=== {pageTitle} — " + status switch
        {
            WikiDropStatus.PageMissing => "no wiki page yet ===",
            WikiDropStatus.PageHasNoLoot => "wiki page lists no loot yet ===",
            _ => $"{news.Count} drop{(news.Count == 1 ? "" : "s")} not in the wiki's list ===",
        });
        if (status == WikiDropStatus.PageMissing)
            sb.AppendLine("(EQBuddy's log reader drops leading a/an/the from creature names — if the " +
                "in-game name carries one, most trash mobs do, create the page WITH it.)");
        sb.AppendLine((status == WikiDropStatus.PageMissing ? "Create page:  " : "Edit page:  ")
                      + EditUrl(pageTitle));

        switch (status)
        {
            case WikiDropStatus.NewToPage:
                sb.AppendLine("Add inside the known_loot <ul> list:");
                sb.AppendLine();
                foreach (var (l, _) in news)
                    sb.AppendLine("<li> " + LootEntry(l, mob.Kills) + "</li>");
                break;
            case WikiDropStatus.PageHasNoLoot:
                sb.AppendLine("Replace the empty known_loot field with:");
                sb.AppendLine();
                sb.AppendLine(KnownLootBlock(news.Select(n => n.Loot), mob.Kills));
                break;
            case WikiDropStatus.PageMissing:
                sb.AppendLine("Paste as the whole new page:");
                sb.AppendLine();
                sb.AppendLine(PageSkeleton(mob, news.Select(n => n.Loot), killZone));
                break;
        }
        sb.AppendLine();
        // Summary-field-sized summary (#65 round four, Frankthetankk: the itemized
        // list was "much longer than what a summary field is meant to hold") — the
        // detail lives in the log reference below instead.
        //
        // It does NOT name EQBuddy (#217 ask 4, Frankthetankk; David approved 2026-08-19).
        // This is the one string the app puts into text a player pastes onto someone
        // else's wiki, under their account and their name — the edit is theirs, and the
        // summary should describe what they observed rather than advertise the tool that
        // helped them read their log. Anyone who wants to credit EQBuddy can still say so;
        // the point is that it is their choice to make, in their own words.
        sb.AppendLine("Suggested edit summary: observed drops " +
            $"({news.Count} item{(news.Count == 1 ? "" : "s")}, {mob.Kills} kill{(mob.Kills == 1 ? "" : "s")}).");
        sb.AppendLine();
        // Full itemization + last-seen LOG TIMES WITH DATES (a session can span
        // midnight): one date header when they all share a day, per-item otherwise.
        var stamped = news.Where(n => n.Loot.LastAt is not null).ToList();
        var oneDay = stamped.Count > 0 && stamped.All(n => n.Loot.LastAt!.Value.Date == stamped[0].Loot.LastAt!.Value.Date);
        sb.AppendLine("Log reference (for your own records, not the wiki)" +
            (oneDay ? $" — {stamped[0].Loot.LastAt:ddd MMM d yyyy}:" : ":"));
        foreach (var (l, _) in news)
            sb.AppendLine($"  {l.Item} ×{l.Count} in {mob.Kills} kill{(mob.Kills == 1 ? "" : "s")}"
                + (l.DropRatePct is { } pct ? $" ({pct:0.#}%)" : "")
                + (l.LastAt is { } at ? oneDay ? $" — last at {at:HH:mm:ss}" : $" — last {at:ddd MMM d HH:mm:ss}" : ""));

        // The other half of the loop (#65 test report): each item's own page carries a
        // dropsfrom list that may not name this creature. No lookup is made here — the
        // contributor eyeballs the page the link opens and pastes only if it's missing.
        sb.AppendLine();
        sb.AppendLine("Each item's own page has a \"Drops from\" list — if this creature is missing");
        sb.AppendLine("there, add the line under the dropsfrom field" +
                      (killZone.Length > 0 ? $" (zone heading [[{killZone}]]):" : ":"));
        foreach (var (l, _) in news)
        {
            var item = QuestCatalog.BaseItemName(l.Item);
            sb.AppendLine($"  {EditUrl(item)}");
            // The resolved wiki title again — a [[Decaying skeleton]] link on an item
            // page would point at a page that doesn't exist.
            sb.AppendLine($"    * [[{pageTitle}]]");
        }

        WriteStatBlock(sb, mob, killZone);
    }

    /// <summary>The observed stat block (#65, Frankthetankk's field list): zone at
    /// kill time, per-kill money as the wiki's own low–high-per-coin format, and
    /// faction hits — with a confirmed absence reported too, because "no faction
    /// hit" saves the next tester the same experiment. Presented for RECONCILIATION,
    /// never as a correction: a mismatch may mean the wiki captured a range our
    /// sample hasn't seen. The same 10+ kill bar as rarity gates what's suggested.</summary>
    private static void WriteStatBlock(StringBuilder sb, MobSummary mob, string killZone)
    {
        var thin = mob.Kills < 10;
        sb.AppendLine();
        sb.AppendLine($"Observed stat block ({mob.Kills} kill{(mob.Kills == 1 ? "" : "s")}"
            + (thin ? " — thin sample, for your notes rather than the wiki yet):" : ") — compare, don't overwrite:"));
        if (killZone.Length > 0)
            sb.AppendLine($"  zone (at kill time): {killZone}");
        if (mob.LevelMin > 0)
            sb.AppendLine("  level (from /consider): " + (mob.LevelMin == mob.LevelMax
                ? $"{mob.LevelMin} (every /con this session agreed — more cons on other spawns could still widen it)"
                : $"{mob.LevelMin} - {mob.LevelMax}"));
        if (mob.CoinMin >= 0)
            sb.AppendLine("  money per kill: " + (mob.CoinMin == mob.CoinMax
                ? $"{StatsSnapshot.FormatCoin(mob.CoinMin)} (single observed value — one sample can't tell \"always\" from \"lucky\")"
                : $"{StatsSnapshot.FormatCoin(mob.CoinMin)} – {StatsSnapshot.FormatCoin(mob.CoinMax)}"));
        if (mob.Factions.Count > 0)
            foreach (var f in mob.Factions)
                sb.AppendLine($"  faction: {f.Faction} {(f.Delta >= 0 ? "+" : "")}{f.Delta}"
                    + $" ({f.Hits} of {mob.Kills} kill{(mob.Kills == 1 ? "" : "s")})");
        else
            sb.AppendLine($"  faction: no hits observed across {mob.Kills} kill{(mob.Kills == 1 ? "" : "s")}"
                + " — a confirmed absence is data too");
    }

    /// <summary>One loot entry in page style: {{:Item}} transclusion (their tooltip
    /// idiom) plus the rarity span only when the sample supports one.</summary>
    private static string LootEntry(MobLoot l, int kills)
    {
        var item = QuestCatalog.BaseItemName(l.Item);
        var rarity = SuggestRarity(l.DropRatePct, kills);
        return "{{:" + item + "}}" + (rarity is null ? "" : $" <span class='drare'>({rarity})</span>");
    }

    /// <summary>The known_loot field in the wiki's own multiline chaining style.</summary>
    private static string KnownLootBlock(IEnumerable<MobLoot> loot, int kills)
    {
        var entries = loot.Select(l => LootEntry(l, kills)).ToList();
        var sb = new StringBuilder();
        sb.AppendLine("| known_loot = ");
        sb.AppendLine();
        sb.Append("<ul><li> ").Append(entries[0]);
        foreach (var e in entries.Skip(1))
            sb.AppendLine().Append("</li><li> ").Append(e);
        sb.AppendLine().Append("</li></ul>");
        return sb.ToString();
    }

    /// <summary>A fresh {{Namedmobpage}} with what the session actually knows — name,
    /// zone, loot — and every other field left blank for editors who know the mob.
    /// Field list mirrors live pages (Ambassador Dvinn survey, 2026-08-08).</summary>
    private static string PageSkeleton(MobSummary mob, IEnumerable<MobLoot> loot, string zone)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{{Namedmobpage");
        sb.AppendLine();
        sb.AppendLine($"| name          = {mob.Name}");
        sb.AppendLine("| race          = ");
        sb.AppendLine("| class         = ");
        sb.AppendLine("| level         = " + (mob.LevelMin > 0
            ? mob.LevelMin == mob.LevelMax ? $"{mob.LevelMin}" : $"{mob.LevelMin} - {mob.LevelMax}"
            : ""));
        sb.AppendLine();
        sb.AppendLine($"| zone          = {(zone.Length > 0 ? $"[[{zone}]]" : "")}");
        sb.AppendLine("| location      = ");
        sb.AppendLine("| respawn_time  = ");
        sb.AppendLine();
        sb.AppendLine("| description = ");
        sb.AppendLine();
        sb.AppendLine(KnownLootBlock(loot, mob.Kills));
        sb.AppendLine();
        sb.AppendLine("| factions = ");
        sb.AppendLine();
        sb.AppendLine("| opposing_factions = ");
        sb.AppendLine();
        sb.AppendLine("| related_quests = ");
        sb.AppendLine();
        sb.AppendLine("}}");
        if (zone.Length > 0)
        {
            sb.AppendLine();
            sb.Append($"[[Category:{zone}]]");
        }
        return sb.ToString();
    }
}
