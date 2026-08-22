using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// What a Drops creature header says about HOW FRESH its wiki read is, and whether a
/// re-check may be asked for right now (#226, LeBigNasty + Frankthetankk; plan in
/// FABLE.md, Fable 5, 2026-08-21).
///
/// The bug this exists for was SILENT staleness: the ✦ compared against a 7-day per-page
/// cache, so a player who corrected the wiki — the thing the ✦ asks them to do — saw the
/// flag stay lit for a week with nothing on screen to say why. A button alone would clear
/// that one instance and leave the NEXT staleness silent, so every header now says when
/// its page was read, and the button is the way to read it again.
///
/// Framework-free and unit-tested here so both desktops spell the same words, and COARSE
/// on purpose: the caption goes into the Drops tab's repaint signature, and a value that
/// ticks every second would rebuild forty rows a minute for nothing (trap 8).
/// </summary>
public static class WikiFreshness
{
    /// <summary>A page read more recently than this is not re-read: the button reads
    /// "checked just now" and does nothing. Etiquette toward a volunteer wiki, and the
    /// number David may change — the plan put it at 30 s.</summary>
    public static readonly TimeSpan MinRecheckInterval = TimeSpan.FromSeconds(30);

    /// <summary>May the player ask for this page again? No while a read is in flight, no
    /// inside <see cref="MinRecheckInterval"/> of the last one. A page never read at all
    /// (null) may always be asked for — that is the Pending case, and asking is the cure.
    ///
    /// This debounces the WIKI, not the BUTTON (Bevel, 2026-08-22): the ↻ stays live and
    /// looks live, because a control that greys out for thirty seconds reads as broken
    /// rather than as considerate. A press inside the window is answered by the tooltip
    /// ("Checked just now") instead of by a dead control.</summary>
    public static bool CanRecheck(DateTime? fetchedAtUtc, bool inFlight, DateTime nowUtc)
    {
        if (inFlight) return false;
        if (fetchedAtUtc is not { } at) return true;
        return nowUtc - at >= MinRecheckInterval;
    }

    /// <summary>The header's caption: when the page was read, in buckets, and whether the
    /// last attempt reached the wiki at all.</summary>
    public static string Caption(MobLookupResult? lookup, bool inFlight, DateTime nowUtc)
    {
        if (inFlight) return "checking\u2026";
        if (lookup is null) return "wiki not read yet";
        return lookup.State switch
        {
            ItemLookupState.Offline => "wiki unreachable",
            ItemLookupState.StaleCache when lookup.FetchedAt is { } at =>
                $"wiki unreachable \u2014 showing {Ago(nowUtc - at)}",
            ItemLookupState.NotFound => "no wiki page",
            // No "read" (Bevel, 2026-08-22): "wiki read just now" hears as "wiki RED just
            // now" on a surface whose whole vocabulary is a red \u2726 marker. The age alone
            // says the same thing and is shorter on an already dense heading.
            _ when lookup.FetchedAt is { } at => $"wiki {Ago(nowUtc - at)}",
            _ => "",
        };
    }

    /// <summary>The button's tooltip. Names the SERVED page (trap 3 — the page the wiki
    /// answered with, which a redirect can make a different page from the one asked
    /// for), so a lookup that resolved to the wrong article becomes visible on screen
    /// instead of being inferred from a screenshot a week later (#226, Innoruk).</summary>
    public static string RecheckTip(MobLookupResult? lookup, bool inFlight, DateTime nowUtc)
    {
        if (inFlight) return "Reading the wiki page now\u2026";
        if (!CanRecheck(lookup?.FetchedAt, inFlight, nowUtc)) return "Checked just now.";
        var page = lookup?.Mob?.PageTitle is { Length: > 0 } t ? $"Page read: \u201c{t}\u201d.\n" : "";
        return page + "Read this creature\u2019s wiki page again now \u2014 after you fix the page, " +
               "this is how the \u2726 marks catch up without waiting a week.";
    }

    /// <summary>Coarse relative time — "just now", "3m ago", "2h ago", "8d ago". Minutes
    /// under an hour, hours under a day, then days; never seconds.</summary>
    public static string Ago(TimeSpan since)
    {
        if (since < TimeSpan.FromMinutes(1)) return "just now";
        if (since < TimeSpan.FromHours(1)) return $"{(int)since.TotalMinutes}m ago";
        if (since < TimeSpan.FromDays(1)) return $"{(int)since.TotalHours}h ago";
        return $"{(int)since.TotalDays}d ago";
    }

    /// <summary>What the repaint signature carries for one creature: the caption's
    /// bucket and the in-flight bit, so a re-check that returns the SAME status still
    /// repaints the caption — and nothing finer, so a second passing does not.</summary>
    public static string SignatureToken(MobLookupResult? lookup, bool inFlight, DateTime nowUtc) =>
        Caption(lookup, inFlight, nowUtc);
}
