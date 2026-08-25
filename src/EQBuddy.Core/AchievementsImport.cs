namespace EQBuddy.Core;

/// <summary>One achievement from `/outputfile achievements` — a C/I flag, a name, and
/// flagged criteria lines beneath it.</summary>
public sealed record AchievementEntry(string Section, string Name, bool Complete,
    List<(string Text, bool Complete)> Criteria);

/// <summary>A completed "Obtain X" criterion resolved to a Sky checklist reward.</summary>
public sealed record SkyRewardMatch(string ClassName, string Reward, string FromCriterion);

/// <summary>
/// Import for the game's `/outputfile achievements` dump (#88, typical-usual-chaos):
/// tab-indented sections → achievements → criteria, each row flagged C (complete) or
/// I (incomplete). The Sky payoff: class-unlock achievements list every Sky reward as
/// its own "Obtain X" criterion, so a character who finished quests before EQBuddy
/// existed can pre-mark the tracker from the game's own records — finer than the
/// achievement, since a half-done unlock still marks the rewards it did obtain.
/// Import only ever ADDS progress; nothing already tracked is unchecked (the AA
/// ledger's rule). Reward names drift from the wiki's ("Windhowl and Spirit Render"
/// vs "Windhowl/Spirit Render", "Spear of Harmony" vs "Harmonic Spear"), so matching
/// is normalized and token-fuzzy, and anything unmatched is REPORTED, never guessed.
/// </summary>
public static class AchievementsImport
{
    public static List<AchievementEntry> Parse(IEnumerable<string> lines)
    {
        var result = new List<AchievementEntry>();
        var section = "";
        AchievementEntry? current = null;
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (line.Length == 0) continue;
            if (line is ['C' or 'I', '\t', '\t', ..])
            {
                current?.Criteria.Add((line[3..].Trim(), line[0] == 'C'));
            }
            else if (line is ['C' or 'I', '\t', ..])
            {
                current = new AchievementEntry(section, line[2..].Trim(), line[0] == 'C', []);
                result.Add(current);
            }
            else
            {
                section = line.Trim();
                current = null;
            }
        }
        return result;
    }

    /// <summary>Completed "Obtain X" criteria from class-unlock achievements, resolved
    /// to Sky checklist rewards for that class. Unmatched completed obtains are
    /// returned for the preview — shown to the player, never silently dropped.
    /// AUTO-GRANT GUARD (#101, Frankthetankk): the player's PRIMARY class unlock is
    /// granted at character creation, and the dump marks it — and its criteria —
    /// complete without any item ever being obtained. A completed unlock whose
    /// "will autocomplete" criterion is itself flagged complete was granted, not
    /// earned, so its Obtain flags prove nothing; those rewards are SKIPPED and
    /// reported, never imported. Incomplete unlocks stay fully trustworthy — their
    /// per-criterion flags are individually tracked by the game.</summary>
    /// <summary>
    /// Every class the dump says this character HOLDS — primary first, then the rest in
    /// dump order.
    ///
    /// **The game's own statement about what the character is**, which is better evidence
    /// than any log heuristic and has been sitting in this file unused for two releases:
    /// <see cref="SkyRewards"/> has read these same rows since #101, purely to refuse
    /// importing rewards for a granted primary. Reading them for what they plainly SAY is
    /// the point of <see cref="CharacterClasses"/>.
    ///
    /// A complete "Class Unlock - X" is the character holding X **however they got it** —
    /// quested, confirmed as primary, or token-bought. That is deliberately wider than
    /// <see cref="SkyRewards"/>'s guard, and the difference matters: an auto-granted unlock
    /// means "do not tick their Sky rewards" and still means "they ARE a Bard". #193
    /// (wizen) is the worked example — token-unlocked Bard, no Sky rewards earned, and a
    /// Bard all the same.
    ///
    /// Incomplete unlocks are excluded: the dump tracks those per criterion and an
    /// unfinished unlock is a class the character is working towards, not one they have.
    /// </summary>
    public static List<string> UnlockedClasses(IEnumerable<AchievementEntry> achievements)
    {
        var primary = new List<string>();
        var rest = new List<string>();
        foreach (var a in achievements)
        {
            if (!a.Complete) continue;
            var dash = a.Name.LastIndexOf(" - ", StringComparison.Ordinal);
            if (dash < 0 || !a.Name.Contains("Class Unlock", StringComparison.OrdinalIgnoreCase))
                continue;
            var className = a.Name[(dash + 3)..].Trim();
            if (className.Length == 0) continue;
            // Canonical, because this list is compared against catalog class names
            // everywhere it lands — the ledger, the class strip, the quest filter. An
            // unresolvable name keeps its own spelling rather than being dropped: a
            // class we do not recognise is still a class the dump says they hold.
            if (QuestClassFilter.Canonical(className) is { Length: > 0 } canonical)
                className = canonical;
            var into = a.Name.Contains("Primary Class Unlock", StringComparison.OrdinalIgnoreCase)
                ? primary : rest;
            if (!into.Contains(className, StringComparer.OrdinalIgnoreCase))
                into.Add(className);
        }
        // Primary first: it is the class the character was created as, and the surface
        // that shows ONE class should show that one.
        return [.. primary, .. rest.Where(c => !primary.Contains(c, StringComparer.OrdinalIgnoreCase))];
    }

    public static (List<SkyRewardMatch> Matches, List<string> Unmatched, List<string> AutoGranted)
        SkyRewards(IEnumerable<AchievementEntry> achievements, IReadOnlyList<SkyQuestChecklistItem> checklist)
    {
        var matches = new List<SkyRewardMatch>();
        var unmatched = new List<string>();
        var autoGranted = new List<string>();
        foreach (var a in achievements)
        {
            // "Primary Class Unlock - Bard" / "Class Unlock - Berserker"
            var dash = a.Name.LastIndexOf(" - ", StringComparison.Ordinal);
            if (dash < 0 || !a.Name.Contains("Class Unlock", StringComparison.OrdinalIgnoreCase))
                continue;
            // The dump spells one class differently from every catalog here
            // ("Shadowknight" vs "Shadow Knight"), so the comparison goes through the
            // canonical name. It used to be an exact match followed by
            // `if (rewards.Count == 0) continue;`, and that pair dropped all sixteen
            // Shadow Knight rewards before the auto-grant guard AND before `unmatched`
            // — the list whose entire job is that nothing is swallowed. There is no
            // early return now: a class with no checklist rows reports its obtains as
            // unmatched, which is what makes the failure visible instead of silent.
            var className = a.Name[(dash + 3)..].Trim();
            if (QuestClassFilter.Canonical(className) is { Length: > 0 } canonical)
                className = canonical;
            var rewards = checklist.Where(c =>
                c.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase)).ToList();

            // TWO ways to get a class unlock without doing the quests, and the dump marks
            // the Obtain criteria complete for BOTH. Each announces itself with its own
            // completed criterion:
            //
            //   confirmed as your primary  →  "will autocomplete…"            C
            //   bought with a token        →  "can be bypassed using a … Token"  C
            //
            // 1.57.3 shipped only the first, which is why #101 read as fixed and #193
            // (wizen) stayed broken: he bought Primary Class Unlock TOKENS, so his Bard
            // unlock has the autocomplete line INCOMPLETE and the bypass line complete —
            // the guard never fired and six rewards he had never obtained imported as
            // turned in. His own three-way dump (2026-08-20) is what settles it, and it is
            // pinned verbatim in AchievementsImportTests: Druid confirmed-as-primary,
            // Bard token-unlocked, Berserker untouched.
            //
            // `a.Complete` still gates everything, so an unfinished unlock keeps its
            // per-criterion trust — the game tracks those flags honestly, and the
            // Berserker case proves it (every line I, nothing skipped, nothing imported).
            var wasAutoGranted = a.Complete && a.Criteria.Any(c => c.Complete
                && (c.Text.Contains("will autocomplete", StringComparison.OrdinalIgnoreCase)
                 || c.Text.Contains("can be bypassed", StringComparison.OrdinalIgnoreCase)));

            foreach (var (text, complete) in a.Criteria)
            {
                if (!complete) continue;
                if (!text.StartsWith("Obtain ", StringComparison.OrdinalIgnoreCase)) continue;
                var name = text["Obtain ".Length..].TrimEnd('.').Trim();
                if (wasAutoGranted)
                {
                    autoGranted.Add($"{className}: {name}");
                    continue;
                }
                var hit = rewards.FirstOrDefault(r => NamesMatch(r.Reward, name));
                if (hit is not null)
                    matches.Add(new SkyRewardMatch(hit.ClassName, hit.Reward, name));
                else
                    unmatched.Add($"{className}: {name}");
            }
        }
        return (matches, unmatched, autoGranted);
    }

    /// <summary>Marks matched rewards turned-in (SkyQuestCompleted keys + item
    /// checkboxes), adding only — an import can teach, never untick. Returns how many
    /// rewards were newly marked.</summary>
    public static int Apply(IEnumerable<SkyRewardMatch> matches, AppSettings settings)
    {
        var added = 0;
        foreach (var m in matches)
        {
            var key = $"{m.ClassName}|{m.Reward}";
            if (!settings.SkyQuestCompleted.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                settings.SkyQuestCompleted.Add(key);
                added++;
            }
            foreach (var item in settings.SkyQuestChecklist)
                if (item.ClassName.Equals(m.ClassName, StringComparison.OrdinalIgnoreCase)
                    && item.Reward.Equals(m.Reward, StringComparison.OrdinalIgnoreCase))
                    item.Acquired = true;   // turned in ⇒ the pieces were acquired first
        }
        return added;
    }

    private static readonly HashSet<string> Stopwords =
        new(["of", "the", "a", "an", "and"], StringComparer.OrdinalIgnoreCase);

    /// <summary>Token-fuzzy name equality for reward drift: normalize away punctuation
    /// and stopwords, then require every token on each side to find a partner on the
    /// other (exact, or a shared prefix of 5+ — "Harmony"/"Harmonic"). Catches
    /// "Windhowl and Spirit Render"="Windhowl/Spirit Render" and
    /// "Spear of Harmony"="Harmonic Spear" without letting "Wind Rune Azia" claim
    /// "Wind Rune Fana".</summary>
    public static bool NamesMatch(string a, string b)
    {
        var ta = Tokens(a);
        var tb = Tokens(b);
        if (ta.Count == 0 || tb.Count == 0) return false;
        return ta.All(x => tb.Any(y => TokenMatch(x, y)))
            && tb.All(x => ta.Any(y => TokenMatch(x, y)));
    }

    private static List<string> Tokens(string s)
    {
        var cleaned = new string(s.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ').ToArray());
        return cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !Stopwords.Contains(t)).ToList();
    }

    /// <summary>Exact, or sharing a long common prefix with only a short tail differing
    /// — "harmony"/"harmonic" (prefix 6, tails 1/2) match; "spiroc"/"spirit" (prefix 4)
    /// and "azia"/"fana" (prefix 0) do not.</summary>
    private static bool TokenMatch(string x, string y)
    {
        if (x == y) return true;
        var n = 0;
        var cap = Math.Min(x.Length, y.Length);
        while (n < cap && x[n] == y[n]) n++;
        return n >= 5 && n >= Math.Max(x.Length, y.Length) - 3;
    }
}
