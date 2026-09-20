using System.Text.RegularExpressions;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// **One producer for "which classes is this view about" (DRA-181 D4, plan P5).**
///
/// <para>The decision is one line; the bug was that it was typed THREE times and one copy
/// disagreed. The desktop's render and the phone's Sky leftover bands both read picks-first;
/// the desktop's class-lens chip strip read the RESOLVED identity list alone, so picking
/// Warrior/Paladin/Cleric left a chip for every other class the dump or the log had ever
/// resolved — and clicking one set a lens the very next render silently cleared. A dead
/// control with nothing on screen saying so (the Founder's 2026-09-17 Desktop smoke).</para>
///
/// <para>So the rows below are the DECISION, and the source guards under them are the "one
/// producer" half: a behaviour test cannot see a fourth copy of a ternary appearing in a file
/// it does not import.</para>
/// </summary>
public class QuestClassLensTests
{
    private static string Root =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    /// <summary>**Picks win.** The player ticked two of the three classes identity holds, so
    /// the view is about two — which is the whole of the Founder's report: the strip is built
    /// from this answer, so the third class stops having a chip.</summary>
    [Fact]
    public void PicksThatNarrowTheResolvedListAreWhatTheViewIsAbout()
    {
        string[] resolved = ["Warrior", "Paladin", "Cleric"];
        string[] picks = ["Warrior", "Paladin"];

        var offered = QuestClassLens.Offered(picks, resolved);

        Assert.Equal(["Warrior", "Paladin"], offered);
        // The leftover, named: this is the chip that used to survive the deselection.
        Assert.DoesNotContain("Cleric", offered);
    }

    /// <summary>**No picks: identity answers**, unchanged. This is the KEEP row — a player
    /// who has never opened the multi-select sees the same strip they have always seen, and
    /// without it every assertion in this file would pass on a build that had quietly made
    /// the picker mandatory.</summary>
    [Fact]
    public void WithNoPicksTheResolvedIdentityIsWhatTheViewIsAbout()
    {
        string[] resolved = ["Warrior", "Paladin", "Cleric"];

        Assert.Equal(resolved, QuestClassLens.Offered([], resolved));
    }

    /// <summary>
    /// **A pick identity does not hold still wins, and that is deliberate.**
    ///
    /// <para>It is reachable two ways and neither is exotic: <c>CharacterClasses.Resolve</c>
    /// leaves picks out entirely once the player has STATED their classes, and it caps the
    /// list at <c>CharacterClasses.Max</c> (3), so a dump naming three classes pushes every
    /// pick out of identity. The class picker offers all sixteen on purpose — "we may be
    /// helping a friend" (David, 2026-08-15) — and the render has always narrowed to such a
    /// pick.</para>
    ///
    /// <para>So the strip must offer it a chip. Before this producer it could not: the strip
    /// was built from identity, so the one class the player had picked was the one class they
    /// could not lens to.</para>
    /// </summary>
    [Fact]
    public void APickTheResolvedListDoesNotCarryStillDecidesTheView()
    {
        string[] resolved = ["Bard", "Druid", "Enchanter"];
        string[] picks = ["Warrior", "Paladin"];

        var offered = QuestClassLens.Offered(picks, resolved);

        Assert.Equal(["Warrior", "Paladin"], offered);
        Assert.DoesNotContain("Bard", offered);
    }

    /// <summary>Neither side has anything: empty, never a wildcard. That is what suppresses
    /// the Sky leftover bands' band B — "only other classes want this" said about a class you
    /// actually play is the one false claim that band exists to avoid, and an empty list is
    /// how the band knows it cannot make it.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NothingPickedAndNothingResolvedIsEmptyRatherThanEverything(bool nulls)
    {
        var offered = nulls
            ? QuestClassLens.Offered(null, null)
            : QuestClassLens.Offered([], []);

        Assert.Empty(offered);
    }

    /// <summary>A single pick answers alone — the strip's own "fewer than two is no lens to
    /// offer" rule then collapses it, but that is the STRIP's decision and not this one. The
    /// render still narrows to the class, which is what the player asked for.</summary>
    [Fact]
    public void OnePickAnswersAloneAndTheListIsNotPaddedFromIdentity()
    {
        Assert.Equal(["Cleric"], QuestClassLens.Offered(["Cleric"], ["Warrior", "Cleric"]));
    }

    // ---- the My Classes quick-select (DRA-216 D1, S4.3) -------------------------------
    //
    // A DIFFERENT question from Offered, in the same file because it is the same subject:
    // Offered asks "which classes is this view about" and answers picks-first; this asks
    // "which classes would a player mean by MY classes" and answers from identity ALONE. It
    // takes no picks parameter at all — a member that cannot see them cannot become a second
    // copy of the ternary above (traps 4 and 33), which is S4.3's "never stale class-filter
    // picks" expressed in a signature rather than in a comment.

    /// <summary>The plain case: identity ticks, in the lens's own row keys.</summary>
    [Fact]
    public void MyClassesTicksTheResolvedIdentity()
    {
        var mine = QuestClassLens.MyClasses(
            ["Warrior", "Cleric"], QuestClassFilter.Classes);

        Assert.Equal(["Cleric", "Warrior"], mine);
    }

    /// <summary>**Row order, not identity order.** The picker reports its ticks in row order,
    /// so an answer in identity order would store one spelling of a selection and read a
    /// different one back the moment the player touched any other row — the same selection
    /// with two representations, which is the shape trap 4 keeps charging for. Warrior is the
    /// character's primary here and the dump names it first; the rows are alphabetical and put
    /// it last.</summary>
    [Fact]
    public void MyClassesAnswersInRowOrderRatherThanIdentityOrder()
    {
        var mine = QuestClassLens.MyClasses(
            ["Warrior", "Bard", "Cleric"], QuestClassFilter.Classes);

        Assert.Equal(["Bard", "Cleric", "Warrior"], mine);
        // Named, so this cannot go green on a build that simply sorted identity:
        Assert.NotEqual(["Warrior", "Bard", "Cleric"], mine);
    }

    /// <summary>**The ROW's spelling ships, whatever identity's is.** The tick is painted by
    /// key, so a lowercase identity that came back lowercase would light nothing at all and
    /// the action would report success having changed the screen not at all — trap 17's
    /// invisible failure, one layer down.</summary>
    [Theory]
    [InlineData("shadow knight")]
    [InlineData("SHADOW KNIGHT")]
    [InlineData("Shadow Knight")]
    public void MyClassesShipsTheRowSpellingAndNotIdentitys(string spelling)
    {
        Assert.Equal(["Shadow Knight"],
            QuestClassLens.MyClasses([spelling], QuestClassFilter.Classes));
    }

    /// <summary>
    /// **A class the lens has no row for is dropped, and the rest still tick.**
    ///
    /// <para>Reachable, not theoretical: <c>AchievementsImport.UnlockedClasses</c> canonicalises
    /// and then deliberately KEEPS a name it could not resolve — "a class we do not recognise is
    /// still a class the dump says they hold" — which is right for identity and impossible for a
    /// filter. Dropping the whole answer instead would punish the player for the one class
    /// EQBuddy cannot read; storing the unknown name would put a pick behind no row, which is a
    /// selection they cannot reach to undo.</para>
    /// </summary>
    [Fact]
    public void AnIdentityClassTheLensHasNoRowForIsDroppedAndTheRestStillTick()
    {
        var mine = QuestClassLens.MyClasses(
            ["Warrior", "Frobnicator"], QuestClassFilter.Classes);

        Assert.Equal(["Warrior"], mine);
        Assert.DoesNotContain("Frobnicator", mine);
    }

    /// <summary>**Nothing resolved is EMPTY, which is how the caller knows to offer no button
    /// at all.** A character with no dump, no qualifying log evidence and no statement is a
    /// real and common state — the first minutes of a fresh install — and a "My Classes" that
    /// silently does nothing when clicked is the broken kind of no-op. An empty ROW list is
    /// the same answer for the same reason.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NothingResolvedMeansNoQuickSelectRatherThanAnEmptyOne(bool nulls)
    {
        Assert.Empty(QuestClassLens.MyClasses(nulls ? null : [], QuestClassFilter.Classes));
        Assert.Empty(QuestClassLens.MyClasses(["Warrior"], nulls ? null : []));
    }

    /// <summary>
    /// **It answers from identity even where the picks disagree — which is the requirement.**
    ///
    /// <para>S4.3 is "uses <c>Resolve</c>, never stale class-filter picks". The player is lensed
    /// to a friend's Bard (#104's reachable state) while their own character is a Warrior/Cleric;
    /// "My Classes" means the Warrior and the Cleric, or it means nothing. The member cannot
    /// consult picks — there is no parameter to pass them through — so this row is a statement
    /// about the SIGNATURE as much as the behaviour, and the guard under it is what keeps a
    /// later edit from adding one.</para>
    /// </summary>
    [Fact]
    public void MyClassesIsIdentityEvenWhenThePicksSayOtherwise()
    {
        // What Offered says about this same character, for contrast: picks win there.
        Assert.Equal(["Bard"], QuestClassLens.Offered(["Bard"], ["Warrior", "Cleric"]));

        Assert.Equal(["Cleric", "Warrior"],
            QuestClassLens.MyClasses(["Warrior", "Cleric"], QuestClassFilter.Classes));
    }

    /// <summary>The signature guard: no overload of this member may take the picks. A
    /// behaviour test cannot see a second parameter arriving with a helpful-looking default,
    /// and that parameter is the whole distance between this member and the ternary above
    /// (trap 34 — a rule that forbids the wrong thing cannot see a thing being added).</summary>
    [Fact]
    public void NoOverloadOfMyClassesCanBeHandedThePicks()
    {
        var overloads = typeof(QuestClassLens).GetMethods()
            .Where(m => m.Name == nameof(QuestClassLens.MyClasses))
            .ToList();

        Assert.NotEmpty(overloads);
        Assert.All(overloads, m => Assert.DoesNotContain(
            m.GetParameters(),
            p => p.Name!.Contains("pick", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// **The hover names what the click is about to do, and where it got it.**
    ///
    /// <para>The action REPLACES the selection, so the player is entitled to read what it will
    /// become before they commit — and the source is the difference between the game's own
    /// statement and a guess off the log. <c>SourceLabel</c> rides verbatim in the parenthetical,
    /// the same construction the identity note uses: Bevel's Helm-signed lock is that it is one
    /// table and nobody composes a second verb around it.</para>
    /// </summary>
    [Fact]
    public void TheQuickSelectsHoverNamesTheClassesAndTheSource()
    {
        var tip = ClassFilterLabel.MyClassesTip(["Cleric", "Warrior"], ClassSource.Achievements);

        Assert.Equal("Tick Cleric · Warrior (from your achievements)", tip);
        // VERBATIM, not re-worded — the assertion is against the table, not against a literal.
        Assert.Contains(CharacterClasses.SourceLabel(ClassSource.Achievements), tip,
            StringComparison.Ordinal);
        // Unabbreviated: the codes exist for the FACE's width budget (#184), not for prose.
        Assert.DoesNotContain("CLR", tip, StringComparison.Ordinal);
    }

    /// <summary>An unknown source says nothing rather than trailing an empty bracket — and
    /// nothing to tick has no tip at all, which is the state where there is no button to hover.
    /// <c>SourceLabel</c> answers <c>""</c> for Unknown, so without this the hover would read
    /// "Tick Cleric ()".</summary>
    [Fact]
    public void TheHoverDropsTheSourceWhenNothingKnowsOneAndIsEmptyWithNothingToTick()
    {
        Assert.Equal("Tick Cleric", ClassFilterLabel.MyClassesTip(["Cleric"], ClassSource.Unknown));
        Assert.Equal("", ClassFilterLabel.MyClassesTip([], ClassSource.Achievements));
    }

    /// <summary>
    /// **The quick-select writes no store of its own, and resolves no list of its own.**
    ///
    /// <para>Two source guards, both aimed at the failure this surface has already had.
    /// <c>OnClassCheckChanged</c> has been the picks store's ONE writer since the picker
    /// shipped; a quick-select that called <c>SetClasses</c> itself would be a second writer of
    /// one fact, and the two would agree until the day one of them was edited (trap 4). And
    /// <c>RefreshMyClassesAction</c> is handed identity by a render that has already resolved
    /// it — reaching for <c>ClassSourceFor</c> or <c>ClassesFor</c> from in there is exactly
    /// how <c>BuildClassStrip</c> earned DRA-181 (trap 33).</para>
    /// </summary>
    [Theory]
    [InlineData("private void SelectMyClasses()", "OnClassCheckChanged",
        new[] { "SetClasses", "ClassSourceFor", "_settings.Save" })]
    [InlineData("private void RefreshMyClassesAction(", "QuestClassLens.MyClasses(",
        new[] { "ClassSourceFor", "ClassesFor", "SelectedClasses" })]
    public void TheQuickSelectAsksTheOneProducerAndCallsTheOneWriter(
        string signature, string must, string[] mustNot)
    {
        var source = File.ReadAllText(
            Path.Combine(Root, "src", "EQBuddy", "QuestsView.xaml.cs"));

        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start > 0, $"{signature} has been renamed — this guard needs re-aiming");
        var end = source.IndexOf("\n    private ", start + 1, StringComparison.Ordinal);
        Assert.True(end > start, $"could not find the end of {signature}");
        // CODE only: the reasons are written in the comments above these methods and a guard
        // that cannot tell a call from a comment punishes writing them down.
        var body = string.Join('\n', source[start..end].Split('\n')
            .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)
                        && !line.TrimStart().StartsWith("///", StringComparison.Ordinal)));

        Assert.Contains(must, body, StringComparison.Ordinal);
        Assert.All(mustNot, bad => Assert.DoesNotContain(bad, body, StringComparison.Ordinal));
    }

    // ---- the "ONE producer" half -----------------------------------------------------
    //
    // A behaviour test over this file cannot see a fourth copy of the ternary appearing in a
    // file that never imports it — which is precisely how the third copy got in.

    /// <summary>Each row: a file that has to DECIDE which classes a surface is about, and why
    /// it is one of them. A new surface that narrows by class adds a row — the
    /// <c>ClassSourceWritersTests</c> must-list idiom, because a rule that forbids the wrong
    /// thing cannot see a missing thing (trap 34).</summary>
    public static TheoryData<string, string> CallSites => new()
    {
        {
            "src/EQBuddy/QuestsView.xaml.cs",
            "the desktop quest surface — it narrows the render AND builds the class-lens "
            + "chip strip, and those two disagreeing is the whole of DRA-181"
        },
        {
            "src/EQBuddy.Companion/CompanionProjection.Checklists.cs",
            "the phone's Sky leftover bands, which claim 'only other classes want this' "
            + "about the classes this answer names"
        },
    };

    [Theory]
    [MemberData(nameof(CallSites))]
    public void EverySurfaceThatNarrowsByClassAsksTheOneProducer(string file, string why)
    {
        var path = Path.Combine(Root, file);
        Assert.True(File.Exists(path), $"{file} has moved — update this list, it is {why}");

        Assert.Contains("QuestClassLens.Offered(", File.ReadAllText(path), StringComparison.Ordinal);
    }

    /// <summary>
    /// **The chip strip does not resolve its own list**, which is the regression in one line.
    ///
    /// <para>The strip is built inside a render that has already decided, so reaching for
    /// <c>ClassSourceFor</c> from in there can only produce a second, differently-argued
    /// answer to a question already answered (trap 33). Reverting <c>BuildClassStrip</c> to
    /// its old body reddens this on the spot — which is the prove-fail, and it is the only
    /// assertion here that fails for the Founder's exact screen.</para>
    /// </summary>
    [Fact]
    public void TheClassStripBuilderResolvesNothingOfItsOwn()
    {
        var source = File.ReadAllText(
            Path.Combine(Root, "src", "EQBuddy", "QuestsView.xaml.cs"));

        var start = source.IndexOf("private void BuildClassStrip()", StringComparison.Ordinal);
        Assert.True(start > 0, "BuildClassStrip has been renamed — this guard needs re-aiming");
        // To the next member at the same indent: the method's own body and nothing after it.
        var end = source.IndexOf("\n    private ", start + 1, StringComparison.Ordinal);
        Assert.True(end > start, "could not find the end of BuildClassStrip");
        // CODE only. The method's comment records what it used to call and why that was
        // wrong, which is worth keeping and is not a call — a guard that cannot tell the two
        // apart is one that punishes writing the reason down.
        var body = string.Join('\n', source[start..end].Split('\n')
            .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

        Assert.DoesNotContain("ClassSourceFor", body, StringComparison.Ordinal);
        Assert.Contains("_offered", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// The catch-all: nobody re-types the ternary.
    ///
    /// <para>The shape is deliberately the DECISION's and not "any emptiness test on
    /// something named class" — a scan that flags every <c>classes.Count > 0 ?</c> in the app
    /// gets an exemption list longer than the rule, and a rule made mostly of exemptions
    /// stops being read. So the pattern wants the false branch to name a resolved-identity
    /// source, which is what a copy of this decision has to choose between.</para>
    ///
    /// <para><b>And it is proved to FIRE on the two lines this slice deleted</b>, verbatim —
    /// a detector nobody has watched match anything reports clean forever, whatever it is
    /// aimed at (trap 78). The one live exemption is the second half of that proof: a real
    /// committed line that matches and is a different decision.</para>
    /// </summary>
    [Fact]
    public void NoOtherFileDecidesAClassListFromAPicksFirstTernary()
    {
        // `? <anything on this line> : … resolved-ish`. One line only — `\s` would span
        // newlines and match a `.Count > 0` against a `?` three statements later.
        var shape = new Regex(
            @"Count[ \t]*>[ \t]*0[ \t]*\?[^;\r\n]*(CharacterClassNames|[Rr]esolved|ClassSourceFor)");

        // THE PROVE-FAIL, before anything is scanned: the two copies this slice replaced,
        // exactly as they read on `main` at 443dea45.
        Assert.Matches(shape,
            "var myClasses = req.Classes.Count > 0 ? req.Classes : req.CharacterClassNames;");
        Assert.Matches(shape, "var classes = picks.Count > 0 ? picks : resolved.ToList();");

        // The exemption is a line that MATCHES and is not this decision, with the reason.
        (string File, string Why)[] exempt =
        [
            ("src/EQBuddy.Companion/CompanionProjection.Quests.cs",
                "the phone's IDENTITY line — nullable-or-absent rather than a choice between "
                + "two lists, and it deliberately never consults picks: Bevel's Helm-signed "
                + "lock says identity stays on screen after picks, it is not the filter."),
        ];
        var known = exempt.Select(e => e.File.Replace('/', Path.DirectorySeparatorChar))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var matched = new List<string>();
        var offenders = new List<string>();
        foreach (var path in Directory.EnumerateFiles(Path.Combine(Root, "src"), "*.cs",
                     SearchOption.AllDirectories))
        {
            if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                continue;
            if (!shape.IsMatch(File.ReadAllText(path))) continue;
            var rel = Path.GetRelativePath(Root, path);
            matched.Add(rel);
            if (!known.Contains(rel)) offenders.Add(rel);
        }

        Assert.True(offenders.Count == 0,
            "these files decide a class list from a picks-first ternary — call "
            + "QuestClassLens.Offered, or add a row above saying why this one is different: "
            + string.Join(", ", offenders));
        // And it is still aimed at live source: every exemption matches it today.
        Assert.All(exempt, row =>
        {
            Assert.Contains(row.File.Replace('/', Path.DirectorySeparatorChar), matched);
            Assert.True(row.Why.Length > 40, $"{row.File}: an exemption needs a real reason");
        });
    }
}
