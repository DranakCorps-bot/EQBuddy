using System.Reflection;
using System.Text.Json;

namespace EQBuddy.Core;

/// <summary>What eqlwiki says a zone's content BELONGS TO — the <c>{{&lt;Era&gt; Era}}</c>
/// banner every zone page opens with, promoted into shipped data by
/// <c>scripts/harvests/eqlwiki/zone-eras-transform.py</c> from the COMMITTED zone wikitext
/// cache. It fetches nothing.
///
/// <para><b>This is a reference, never a recommendation.</b> An era is the wiki's own word
/// and the template it was read from, so a surface can cite it ("eqlwiki files Kael Drakkel
/// under Velious") rather than turn it into a judgement. Nothing here knows about a player,
/// a level, or what era the WORLD is at — <b>no file in this repo states that yet</b>, and
/// DRA-180 D1 deliberately does not invent one.</para>
///
/// <para><b>Why a level band could never answer this.</b> The Founder's Farm Gear smoke put
/// a level-29 character's Befallen sword against 25 dominating catalog candidates, 20 of
/// them Kunark/Velious raid loot. Kael Drakkel's <c>Level of Monsters</c> row reads
/// <c>30-60+</c>, so <see cref="ZoneLevels"/> hands the band gate a <c>Min</c> of 30 and the
/// gate PASSES it — correctly, because Kael really does hold level-30 giants. In an era the
/// world has not reached. A band cannot express an expansion; this banner can, and it was
/// already on the page.</para>
///
/// <para><b>Absent is still an answer.</b> 104 of 118 zone pages carry a banner; the other
/// 14 do not, and they ship too, in <c>NoEra</c> — see <see cref="Lookup"/>'s four outcomes.
/// <b>"Absent means Classic" is refused by name</b>: three of the 14 (Stonebrunt Mountains,
/// The Warrens, Kerra Island) are the Paineel-adjacent set, in a corpus where exactly one
/// page carries <c>{{Paineel Era}}</c>, so defaulting them would put a level-45 Warrens camp
/// in a pre-Paineel world on the strength of a template nobody wrote (trap 73). Read
/// <c>scripts/harvests/eqlwiki/zone-eras-report.md</c> before relying on the coverage.</para>
///
/// <para>D1 ships the instrument; <b>DRA-180 D2 is the first reader</b> — an era gate beside
/// <c>Recommendations</c>' band gate, refusing a catalog zone row whose era is later than the
/// world's. Nothing here knows about that: this class answers what the wiki said, and the
/// judgement lives with the engine.</para>
/// </summary>
public sealed class ZoneEras
{
    /// <summary>An era and the wiki template it was read from, so the words a surface writes
    /// can quote the source instead of paraphrasing it.
    ///
    /// <para><see cref="Era"/> is the LADDER's spelling (<see cref="QuestEraLadder.Eras"/>)
    /// and <see cref="Verbatim"/> is the page's own text, which is not always the same
    /// string: the transform matches case-insensitively so a lowercase wiki edit cannot move
    /// the committed bytes of the field a gate reads.</para></summary>
    public sealed record Banner(string Era, string Verbatim);

    /// <summary>Why a lookup answered the way it did. Four outcomes and no fifth — the
    /// <see cref="ZoneLevels.Source"/> idiom, for the same reason: a surface that wants to
    /// SAY something about a zone needs to tell "we have no idea" apart from "the page does
    /// not say" apart from "the page says something we will not read".</summary>
    public enum Source
    {
        /// <summary>No zone page of this name has been read. We know nothing.</summary>
        Unknown,
        /// <summary>The page was read and carries no era banner at all — 14 of 118.</summary>
        NoTemplate,
        /// <summary>The page carries a banner the transform refuses to turn into an era:
        /// a word that is not on <see cref="QuestEraLadder.Eras"/>, or two DIFFERENT eras on
        /// one page. <see cref="Answer.Verbatim"/> carries the template(s) word for word.
        /// Neither arm fires on the current corpus, which is exactly why both are guarded
        /// against fixtures and in the transform's <c>--selftest</c> (trap 78).</summary>
        Refused,
        /// <summary>An era, spelled as the ladder spells it.</summary>
        Dated,
    }

    /// <summary>One lookup's whole answer. <see cref="Era"/> is empty except on
    /// <see cref="Source.Dated"/>; <see cref="Verbatim"/> is empty except on
    /// <see cref="Source.Dated"/> and <see cref="Source.Refused"/>.</summary>
    public readonly record struct Answer(Source Source, string Era, string Verbatim);

    private readonly Dictionary<string, Answer> _byTitle = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Answer?> _byKey = new(StringComparer.OrdinalIgnoreCase);

    public int DatedCount { get; }
    public int AbsentCount { get; }

    /// <summary>Zones whose page carries a banner we would not read.</summary>
    public int RefusedCount { get; }

    /// <summary>Zones whose page carries no banner at all.</summary>
    public int NoTemplateCount { get; }

    /// <summary>Every zone we have an era for, alphabetized.</summary>
    public IReadOnlyList<string> DatedZones =>
        _byTitle.Where(p => p.Value.Source == Source.Dated)
            .Select(p => p.Key).OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();

    public ZoneEras() { }

    public ZoneEras(IReadOnlyDictionary<string, Banner> eras,
                    IReadOnlyDictionary<string, string> noEra)
    {
        foreach (var (zone, banner) in eras)
            _byTitle[zone] = new Answer(Source.Dated, banner.Era, banner.Verbatim);
        foreach (var (zone, verbatim) in noEra)
            _byTitle[zone] = verbatim.Length == 0
                ? new Answer(Source.NoTemplate, "", "")
                : new Answer(Source.Refused, "", verbatim);

        DatedCount = eras.Count;
        AbsentCount = noEra.Count;
        NoTemplateCount = noEra.Count(p => p.Value.Length == 0);
        RefusedCount = AbsentCount - NoTemplateCount;

        // The fold index, for the spelling a DROP record uses rather than the title the wiki
        // page carries. Exactly one pair of enumerated titles folds together — "Chardok
        // (Pre-Revamp)" {{Kunark Era}} and "Chardok (Post-Revamp)" {{Chardok Revamp Era}},
        // while the item catalog's DropZones just says "Chardok" — so the collision has to
        // be decided rather than discovered later. See Reconcile.
        foreach (var (zone, answer) in _byTitle)
        {
            var key = ZoneMapFiles.IdentityKey(zone);
            if (key.Length == 0) continue;
            _byKey[key] = _byKey.TryGetValue(key, out var existing)
                ? Reconcile(existing, answer)
                : answer;
        }
    }

    /// <summary>
    /// What one identity key answers when two titles fold onto it. <b>This is the one place
    /// the Chardok decision is made</b> — the transform emits what each PAGE said and does
    /// not re-implement this (trap 4).
    ///
    /// <para><b>Two DATED titles answer the EARLIER era.</b> Content that exists from Kunark
    /// on exists in a Chardok-Revamp world too, so the earlier era is the true answer to
    /// "has the world reached this place yet"; the later one would have an era gate refuse a
    /// zone that is in the game.</para>
    ///
    /// <para><b>Anything else that disagrees answers NOTHING</b> — an absence is not an era
    /// and cannot be compared, so folding a dated title with an absent one would be inventing
    /// the very default ("absent means Classic") the transform refuses by name. Null is
    /// sticky: once a key is ambiguous no later title rescues it.</para>
    ///
    /// <para>The rule is commutative and associative on purpose, because dictionary iteration
    /// order is not something to depend on: equal answers agree, earliest-wins is a min, and
    /// a disagreement absorbs everything after it. <c>ZoneErasTests</c> asserts both
    /// insertion orders give one answer.</para>
    /// </summary>
    private static Answer? Reconcile(Answer? existing, Answer next)
    {
        if (existing is not { } prior) return null;
        if (prior == next) return prior;
        if (prior.Source != Source.Dated || next.Source != Source.Dated) return null;

        var a = QuestEraLadder.IndexOf(prior.Era);
        var b = QuestEraLadder.IndexOf(next.Era);
        // A Dated answer off the ladder cannot come out of the transform, but this
        // constructor is public and a fixture can hand one in. Two eras we cannot rank are
        // two eras we cannot pick between.
        if (a < 0 || b < 0) return null;
        return a <= b ? prior : next;
    }

    /// <summary>The whole answer for a zone name — the wiki's own title, or the spelling an
    /// item record's <c>DropZones</c> carries.
    ///
    /// <para>Exact title first, then <see cref="ZoneMapFiles.IdentityKey"/>, and <b>nothing
    /// looser</b> — the <see cref="ZoneLevels.Lookup"/> rule verbatim, for the reason spelled
    /// out there. Containment bridges "Estate of Unrest" to "The Estate of Unrest", but it
    /// also hands "Commonlands" West Commonlands's answer and matches zone names sitting
    /// inside free prose. Travel can afford a near miss; an era that is wrong is a claim a
    /// surface will state as fact, and it is the claim that REFUSES a row.</para></summary>
    public Answer Lookup(string zone)
    {
        var name = zone.Trim();
        if (name.Length == 0) return new Answer(Source.Unknown, "", "");
        if (_byTitle.TryGetValue(name, out var exact)) return exact;
        var key = ZoneMapFiles.IdentityKey(name);
        if (key.Length != 0 && _byKey.TryGetValue(key, out var folded) && folded is { } a)
            return a;
        return new Answer(Source.Unknown, "", "");
    }

    /// <summary>The era for a zone, or null. Null covers all three absent outcomes; use
    /// <see cref="Lookup"/> when the difference between them is something you are about to
    /// say out loud.</summary>
    public string? EraFor(string zone) =>
        Lookup(zone) is { Source: Source.Dated } a ? a.Era : null;

    public static ZoneEras LoadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("EQBuddy.Core.Data.ZoneEras.json");
        if (stream is null) return new ZoneEras();
        try
        {
            var root = JsonSerializer.Deserialize<Root>(stream);
            if (root is null) return new ZoneEras();
            return new ZoneEras(root.Eras ?? [], root.NoEra ?? []);
        }
        catch (Exception ex)
        {
            CoreLog.Error(ex);
            return new ZoneEras();
        }
    }

    private sealed class Root
    {
        public string? Source { get; set; }
        public Dictionary<string, Banner>? Eras { get; set; }
        public Dictionary<string, string>? NoEra { get; set; }
    }

    private static ZoneEras? _default;
    private static readonly object DefaultLock = new();

    /// <summary>Lazy, like every other shipped catalog: the parse happens on first use.</summary>
    public static ZoneEras Default
    {
        get
        {
            if (_default is { } d) return d;
            lock (DefaultLock) return _default ??= LoadEmbedded();
        }
    }
}
