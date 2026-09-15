using System.Reflection;
using System.Text.Json;

namespace EQBuddy.Core;

/// <summary>What eqlwiki says the creatures in a zone are levelled at — the
/// <c>Level of Monsters</c> infobox row, promoted into shipped data by
/// <c>scripts/harvests/eqlwiki/zonelevels-transform.py</c> from the COMMITTED zone
/// wikitext cache. It fetches nothing.
///
/// <para><b>This is a reference, never a recommendation.</b> A band is the wiki's own two
/// numbers and the verbatim they were read from, so a surface can cite them ("eqlwiki lists
/// its creatures at 5–20") rather than turn them into an adjective. Nothing here knows about
/// a player, an XP curve, or whether a zone is worth farming.</para>
///
/// <para><b>Absent is an answer, and it is the common one.</b> 46 of 118 zone pages give a
/// band this strictly; the rest either carry no row or carry a shape the transform refuses
/// to read (`50+`, `1-15, 35`). Those ship too, in <c>NoBand</c>, because "the page does not
/// answer" and "we have never read a page for this zone" are different sentences —
/// see <see cref="Lookup"/>'s four outcomes. Read
/// <c>scripts/harvests/eqlwiki/zonelevels-report.md</c> before relying on the coverage.</para>
///
/// <para>DRA-84 D1 ships the instrument only: no engine reads a band yet. The gate that
/// will (plan P2) is its own slice, and the report's join numbers are what it should be
/// written against.</para>
/// </summary>
public sealed class ZoneLevels
{
    /// <summary>A closed level band and the wiki row it was read from, so the words a
    /// surface writes can quote the source instead of paraphrasing it.</summary>
    public sealed record Band(int Min, int Max, string Verbatim);

    /// <summary>Why a lookup answered the way it did. Four outcomes and no fifth: a caller
    /// that wants to SAY something about a zone needs to tell "we have no idea" apart from
    /// "the wiki does not say" apart from "the wiki says something we will not read".</summary>
    public enum Source
    {
        /// <summary>No zone page of this name has been read. We know nothing.</summary>
        Unknown,
        /// <summary>The page was read and carries no <c>Level of Monsters</c> row —
        /// the cities, mostly.</summary>
        NoRow,
        /// <summary>The page answers, in a shape the transform refuses to turn into a band.
        /// <see cref="Answer.Verbatim"/> carries it word for word.</summary>
        Refused,
        /// <summary>A band.</summary>
        Banded,
    }

    /// <summary>One lookup's whole answer. <see cref="Verbatim"/> is empty except on
    /// <see cref="Source.Refused"/> and <see cref="Source.Banded"/>.</summary>
    public readonly record struct Answer(Source Source, Band? Band, string Verbatim);

    private readonly Dictionary<string, Answer> _byTitle = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Answer?> _byKey = new(StringComparer.OrdinalIgnoreCase);

    public int BandCount { get; }
    public int AbsentCount { get; }

    /// <summary>Zones whose page carries a <c>Level of Monsters</c> row we would not read.</summary>
    public int RefusedCount { get; }

    /// <summary>Zones whose page carries no such row at all.</summary>
    public int NoRowCount { get; }

    /// <summary>Every zone we have a band for, alphabetized.</summary>
    public IReadOnlyList<string> BandedZones =>
        _byTitle.Where(p => p.Value.Source == Source.Banded)
            .Select(p => p.Key).OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();

    public ZoneLevels() { }

    public ZoneLevels(IReadOnlyDictionary<string, Band> bands,
                      IReadOnlyDictionary<string, string> noBand)
    {
        foreach (var (zone, band) in bands)
            _byTitle[zone] = new Answer(Source.Banded, band, band.Verbatim);
        foreach (var (zone, verbatim) in noBand)
            _byTitle[zone] = verbatim.Length == 0
                ? new Answer(Source.NoRow, null, "")
                : new Answer(Source.Refused, null, verbatim);

        BandCount = bands.Count;
        AbsentCount = noBand.Count;
        NoRowCount = noBand.Count(p => p.Value.Length == 0);
        RefusedCount = AbsentCount - NoRowCount;

        // The fold index, for the spelling a DROP record uses rather than the title the
        // wiki page carries. Two titles can fold together ("Chardok (Pre-Revamp)" and
        // "Chardok (Post-Revamp)"); where they do NOT agree on their answer the key is
        // ambiguous and stored as null, because handing back whichever loaded last would
        // be a coin toss wearing a citation.
        foreach (var (zone, answer) in _byTitle)
        {
            var key = ZoneMapFiles.IdentityKey(zone);
            if (key.Length == 0) continue;
            if (_byKey.TryGetValue(key, out var existing))
            {
                if (existing is not { } prior || !Agrees(prior, answer)) _byKey[key] = null;
            }
            else _byKey[key] = answer;
        }
    }

    private static bool Agrees(Answer a, Answer b) =>
        a.Source == b.Source && a.Band == b.Band && a.Verbatim == b.Verbatim;

    /// <summary>The whole answer for a zone name — the wiki's own title, or the spelling an
    /// item record's <c>DropZones</c> carries.
    ///
    /// <para>Exact title first, then <see cref="ZoneMapFiles.IdentityKey"/>, and <b>nothing
    /// looser</b>. In particular this does NOT do the longest-containment match
    /// <see cref="ZoneGraph.Resolve"/> uses for travel: containment bridges "Estate of
    /// Unrest" to "The Estate of Unrest", but it also hands "Commonlands" West Commonlands's
    /// band, hands "Qeynos Aqueducts" the city's 1–9 when its own page was refused, and
    /// matches zone names sitting inside free prose. Travel can afford a near miss; a band
    /// that is wrong is a number a surface will state as fact.</para></summary>
    public Answer Lookup(string zone)
    {
        var name = zone.Trim();
        if (name.Length == 0) return new Answer(Source.Unknown, null, "");
        if (_byTitle.TryGetValue(name, out var exact)) return exact;
        var key = ZoneMapFiles.IdentityKey(name);
        if (key.Length != 0 && _byKey.TryGetValue(key, out var folded) && folded is { } a)
            return a;
        return new Answer(Source.Unknown, null, "");
    }

    /// <summary>The band for a zone, or null — the one entry point plan P1 named. Null
    /// covers all three absent outcomes; use <see cref="Lookup"/> when the difference
    /// between them is something you are about to say out loud.</summary>
    public Band? BandFor(string zone) => Lookup(zone).Band;

    public static ZoneLevels LoadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("EQBuddy.Core.Data.ZoneLevelBands.json");
        if (stream is null) return new ZoneLevels();
        try
        {
            var root = JsonSerializer.Deserialize<Root>(stream);
            if (root is null) return new ZoneLevels();
            return new ZoneLevels(root.Bands ?? [], root.NoBand ?? []);
        }
        catch (Exception ex)
        {
            CoreLog.Error(ex);
            return new ZoneLevels();
        }
    }

    private sealed class Root
    {
        public string? Source { get; set; }
        public Dictionary<string, Band>? Bands { get; set; }
        public Dictionary<string, string>? NoBand { get; set; }
    }

    private static ZoneLevels? _default;
    private static readonly object DefaultLock = new();

    /// <summary>Lazy, like every other shipped catalog: the parse happens on first use.</summary>
    public static ZoneLevels Default
    {
        get
        {
            if (_default is { } d) return d;
            lock (DefaultLock) return _default ??= LoadEmbedded();
        }
    }
}
