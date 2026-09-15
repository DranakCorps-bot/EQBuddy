using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **The illustration lock, at SET level.** Every individual landing picture already had a
/// recipe; what nothing recorded was the SET — which files the page draws, and the palette
/// they must all be shot at. That lived in one commit message (<c>06c66462</c>, the Founder's
/// uniform-BlueGrey T4 look, 2026-09-10) and nowhere a tool could read.
///
/// <para>That gap has a specific, arriving failure. <c>shoot.ps1</c>'s default <c>-Theme</c> is
/// <b>Turquoise</b> — correct for <c>docs/screenshots/</c>, and wrong for this page. So the next
/// person who re-runs a landing shot the obvious way commits one Turquoise still into a BlueGrey
/// set, the page goes quietly mixed, and the only thing that ever said otherwise was a sentence
/// in a merge commit. DRA-56 was itself dispatched to re-shoot this set to Turquoise, six hours
/// after the Founder had settled it the other way — the stale instruction is not hypothetical,
/// it is the card that produced this file.</para>
///
/// <para>The page also makes two promises out loud, to a cold visitor, in its own prose:
/// *"Every capture and clip on this page is a real build driven by the repo's own harness"* and
/// *"this page makes no third-party requests"*. Both are now assertions rather than good
/// intentions.</para>
///
/// <para><b>Both halves of the pairing (trap 34).</b> A manifest that only listed recipes could
/// not see a picture the page added without one; a scan that only read the page could not see a
/// recipe that had rotted away. <see cref="EveryPictureAndClipThePageDrawsHasARecipe"/> compares
/// the two SETS, so either drift fails.</para>
///
/// <para><b>And every detector here is proven to FIRE (trap 78)</b>, against the states this
/// slice actually passed through — <see cref="StaticFacesWithNoSixFiftyFace"/> is the stylesheet
/// DRA-56 would have shipped if the weight scale had not been measured, and
/// <see cref="ThirdPartyMarkup"/> is the shape the no-requests promise forbids. A guard aimed at
/// nothing is green for no reason at all.</para>
/// </summary>
public class LandingSiteTests
{
    /// <summary>The one palette the whole landing is shot at (Founder T4 look, 2026-09-10).
    /// Deliberately NOT <c>shoot.ps1</c>'s default — see the class summary.</summary>
    private const string LandingTheme = "BlueGrey";

    /// <summary>
    /// **The curated must-list: every visual asset the landing draws, and how to regenerate it.**
    /// A row is (file under <c>site/assets/</c>, the script that produces it, the arguments that
    /// reproduce THIS committed file). Add a picture to the page, add its row here — that is the
    /// illustration lock, and the set comparison below is what makes it unskippable.
    /// </summary>
    public static readonly (string Asset, string Recipe)[] Manifest =
    [
        // --- the 17 stills that come straight through their own shoot.ps1 recipe ---
        ("img/creature-kills.png", "shoot.ps1 -Shot creature-kills -Theme BlueGrey"),
        ("img/damage-breakout.png", "shoot.ps1 -Shot damage-breakout -Theme BlueGrey"),
        ("img/gearloot-gear.png", "shoot.ps1 -Shot gearloot-gear -Theme BlueGrey"),
        ("img/hud-chips-deadlines.png", "shoot.ps1 -Shot hud-chips-deadlines -Theme BlueGrey"),
        ("img/hud-expand-buffs.png", "shoot.ps1 -Shot hud-expand-buffs -Theme BlueGrey"),
        ("img/hud-expand-loot.png", "shoot.ps1 -Shot hud-expand-loot -Theme BlueGrey"),
        ("img/mini-bar-chips.png", "shoot.ps1 -Shot mini-bar-chips -Theme BlueGrey"),
        ("img/mini-bar.png", "shoot.ps1 -Shot mini-bar -Theme BlueGrey"),
        ("img/shell-gear.png", "shoot.ps1 -Shot shell-gear -Theme BlueGrey"),
        ("img/shell-home.png", "shoot.ps1 -Shot shell-home -Theme BlueGrey"),
        ("img/shell-live.png", "shoot.ps1 -Shot shell-live -Theme BlueGrey"),
        ("img/shell-progress-history.png", "shoot.ps1 -Shot shell-progress-history -Theme BlueGrey"),
        ("img/shell-quests-sky-guide.png", "shoot.ps1 -Shot shell-quests-sky-guide -Theme BlueGrey"),
        ("img/shell-quests-sky-guide-card.png", "shoot.ps1 -Shot shell-quests-sky-guide-card -Theme BlueGrey"),
        ("img/shell-quests-sky-guide-folded.png", "shoot.ps1 -Shot shell-quests-sky-guide-folded -Theme BlueGrey"),
        ("img/shell-world-drops.png", "shoot.ps1 -Shot shell-world-drops -Theme BlueGrey"),
        ("img/spawns-window.png", "shoot.ps1 -Shot spawns-window -Theme BlueGrey"),

        // The phone is the one still that is not a shoot.ps1 shot: it renders through the real
        // projection in the screenshot fixture, themed by an env hook rather than a -Theme.
        ("img/mobile-map-phone.png",
            "mobile-harness.ps1 + EQBUDDY_SHOOT_THEME=BlueGrey ScreenshotFixtureTests, "
            + "headless Edge at 1476x2532"),

        // --- the clips; the recorder's own default is the landing theme (asserted below) ---
        ("media/tray-build-loop.gif", "record-tray-gifs.ps1 -Gif tray-build-loop"),
        ("media/tray-click-keep.gif", "record-tray-gifs.ps1 -Gif tray-click-keep"),
        ("media/tray-drag-reorder.gif", "record-tray-gifs.ps1 -Gif tray-drag-reorder"),
        ("media/tray-hover-peek.gif", "record-tray-gifs.ps1 -Gif tray-hover-peek"),
        ("media/tray-peek-park-resize.gif", "record-tray-gifs.ps1 -Gif tray-peek-park-resize"),
    ];

    /// <summary>**The pairing.** The page's pictures and the manifest's rows are the same set.
    /// A picture with no recipe is an illustration nobody can check; a recipe for a picture the
    /// page no longer draws is a list rotting into decoration.</summary>
    [Fact]
    public void EveryPictureAndClipThePageDrawsHasARecipe()
    {
        var drawn = DrawnAssets(Html).OrderBy(a => a, StringComparer.Ordinal).ToArray();
        var recipes = Manifest.Select(r => r.Asset)
            .OrderBy(a => a, StringComparer.Ordinal).ToArray();

        var undocumented = drawn.Except(recipes, StringComparer.Ordinal).ToArray();
        var orphaned = recipes.Except(drawn, StringComparer.Ordinal).ToArray();

        Assert.True(undocumented.Length == 0,
            "site/index.html draws an illustration with no recipe in LandingSiteTests.Manifest: "
            + string.Join(", ", undocumented)
            + ". An illustration of our own UI is a capture with a recipe, or it does not ship.");

        Assert.True(orphaned.Length == 0,
            "LandingSiteTests.Manifest names an asset the page no longer draws: "
            + string.Join(", ", orphaned) + ". Delete the row, or restore the picture.");
    }

    /// <summary>A recipe row naming a file that is not committed is a broken picture on a public
    /// page — the asset 404s and the visitor sees alt text.</summary>
    [Theory]
    [MemberData(nameof(ManifestRows))]
    public void EveryRecipeRowNamesACommittedFile(string asset, string recipe)
    {
        var path = Path.Combine(SiteDir, "assets", asset.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(path),
            $"site/assets/{asset} is not committed, but the page draws it. Regenerate with: {recipe}");
        Assert.True(new FileInfo(path).Length > 0, $"site/assets/{asset} is empty.");
    }

    /// <summary>**One palette across the whole landing.** Every row that can name a theme names
    /// the SAME one. A set shot half at one palette and half at another is the defect the
    /// Founder's T4 pass existed to remove.</summary>
    [Fact]
    public void TheWholeSetIsOneTheme()
    {
        var themed = Manifest
            .Where(r => r.Recipe.Contains("Theme", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(themed);

        var wrong = themed
            .Where(r => !r.Recipe.Contains(LandingTheme, StringComparison.Ordinal))
            .Select(r => r.Asset)
            .ToArray();

        Assert.True(wrong.Length == 0,
            $"the landing is one palette ({LandingTheme}, Founder T4 look 2026-09-10) and these "
            + "rows name another: " + string.Join(", ", wrong));
    }

    /// <summary>**The reason the manifest must spell <c>-Theme</c> out.** <c>shoot.ps1</c>'s
    /// default is the palette for <c>docs/screenshots/</c>, not for this page — so a landing
    /// re-shoot run the obvious, argument-free way produces the WRONG picture and nothing
    /// complains. The day those two agree, this test fails and the explicit arguments above can
    /// be simplified on purpose rather than by accident.</summary>
    [Fact]
    public void TheStillRecipeDefaultIsNotTheLandingThemeSoEveryRowPassesItExplicitly()
    {
        var shootDefault = Regex.Match(
            ReadRepoFile(Path.Combine("scripts", "shoot.ps1")),
            @"\$Theme\s*=\s*'(?<theme>\w+)'");

        Assert.True(shootDefault.Success, "could not read shoot.ps1's default -Theme.");
        Assert.NotEqual(LandingTheme, shootDefault.Groups["theme"].Value);

        var shots = Manifest.Where(r => r.Recipe.Contains("shoot.ps1", StringComparison.Ordinal));
        Assert.All(shots, row => Assert.Contains($"-Theme {LandingTheme}", row.Recipe));
    }

    /// <summary>**A recipe is only a recipe if it RUNS.** Every `-Shot` the manifest names is a
    /// shot `shoot.ps1` actually defines, and the palette every row passes is one
    /// `ThemePalettes` actually ships. Without this the manifest is a set of strings that look
    /// like commands — and the failure it prevents is the worst kind, because you only discover
    /// it at the moment you need to re-shoot.</summary>
    [Fact]
    public void EveryRecipeNamesAShotAndAPaletteThatReallyExist()
    {
        var declared = Regex.Matches(
                ReadRepoFile(Path.Combine("scripts", "shoot.ps1")),
                @"^\s*'(?<shot>[a-z0-9-]+)'\s*=\s*@\{", RegexOptions.Multiline)
            .Select(m => m.Groups["shot"].Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(declared);

        var unknown = Manifest
            .Select(r => Regex.Match(r.Recipe, @"-Shot (?<shot>[a-z0-9-]+)"))
            .Where(m => m.Success && !declared.Contains(m.Groups["shot"].Value))
            .Select(m => m.Groups["shot"].Value)
            .ToArray();

        Assert.True(unknown.Length == 0,
            "the manifest names shots shoot.ps1 does not define: " + string.Join(", ", unknown));

        Assert.Contains(
            $"[\"{LandingTheme}\"]",
            ReadRepoFile(Path.Combine("src", "EQBuddy.UI.Shared", "ThemePalettes.cs")));
    }

    /// <summary>The clip recorder is the other way round: the landing is its only consumer, so
    /// its default IS the landing theme and the committed clips reproduce with no arguments.
    /// If that default moves, the recipes above stop being reproducible.</summary>
    [Fact]
    public void TheClipRecorderStillDefaultsToTheLandingTheme()
    {
        var recorded = Regex.Match(
            ReadRepoFile(Path.Combine("scripts", "record-tray-gifs.ps1")),
            @"\$Theme\s*=\s*'(?<theme>\w+)'");

        Assert.True(recorded.Success, "could not read record-tray-gifs.ps1's default -Theme.");
        Assert.Equal(LandingTheme, recorded.Groups["theme"].Value);
    }

    /// <summary>
    /// **Every weight the stylesheet asks for has a real face behind it.** This is the CrossOver
    /// bug from <see cref="BundledFontFaceTests"/> arriving on the web: a family that ships only
    /// some of the weights its own CSS names does not fail, it falls through to whatever the
    /// visitor's machine has, so the page renders in two typefaces at once and only on machines
    /// that are not ours.
    ///
    /// <para>DRA-56 walked straight into it. Inter was ONE variable file declaring
    /// <c>font-weight: 100 900</c>, which covers everything; EQBuddy Sans is three static faces
    /// at 400/600/700, and the sheet asked for <b>650</b> in seven places. CSS rounds 650 UP to
    /// 700, which would have collapsed the pills and the CTA into the heading weight — a
    /// hierarchy lost silently. The seven were remapped to 600 deliberately.</para>
    /// </summary>
    [Fact]
    public void EveryWeightTheStylesheetAsksForHasAFaceBehindIt() =>
        Assert.Empty(WeightsWithNoFace(Css));

    /// <summary>**The prove-fail.** The stylesheet DRA-56 would have shipped had it not measured
    /// the faces: the three real static blocks, and a 650 with nothing to resolve to.</summary>
    [Fact]
    public void TheWeightScannerFindsAWeightWithNoFace() =>
        Assert.Equal([650], WeightsWithNoFace(StaticFacesWithNoSixFiftyFace));

    /// <summary>And it does not fire on the variable-font sheet that was genuinely fine — a
    /// single face declaring a RANGE covers every weight in it. Every equality assertion
    /// deserves one negative (trap 39).</summary>
    [Fact]
    public void TheWeightScannerAcceptsAVariableFaceCoveringTheRange() =>
        Assert.Empty(WeightsWithNoFace(VariableFaceCoveringEverything));

    /// <summary>
    /// **The page's own prose is renderable by the face the page ships.** EQBuddy Sans is a
    /// SUBSET of Noto (831 codepoints, built for the app's glyph needs), and the app's needs are
    /// not a prose page's: one typographic quote, arrow or dash the subset never included falls
    /// through to the fallback stack, so a single character renders in Segoe UI mid-sentence.
    ///
    /// <para>Measured at the swap: 0 of the 94 distinct characters on the page were missing. This
    /// asserts it stays that way — of all three faces, because a character present in Regular and
    /// absent from Bold breaks only the bold runs.</para>
    /// </summary>
    [Theory]
    [InlineData("EQBuddySans.ttf")]
    [InlineData("EQBuddySans-SemiBold.ttf")]
    [InlineData("EQBuddySans-Bold.ttf")]
    public void EveryCharacterThePageShowsIsInTheShippedFace(string face)
    {
        var path = Path.Combine(SiteDir, "assets", "fonts", face);
        Assert.True(File.Exists(path), $"site/assets/fonts/{face} is not committed.");

        var cmap = SfntFacts.Read(File.ReadAllBytes(path)).Cmap;
        var missing = VisibleText(Html)
            .Where(c => !char.IsControl(c) && !cmap.Contains(c))
            .Distinct()
            .Select(c => $"U+{(int)c:X4} '{c}'")
            .ToArray();

        Assert.True(missing.Length == 0,
            $"site/index.html shows characters {face} has no glyph for, so they render in the "
            + "fallback face mid-sentence: " + string.Join(", ", missing));
    }

    /// <summary>The face the stylesheet names is the family the files actually declare. A
    /// <c>@font-face</c> src is a file, but <c>font-family</c> is a NAME — get it wrong and the
    /// page silently renders in the fallback with no error anywhere (trap 40, one layer over).
    /// </summary>
    [Fact]
    public void TheStylesheetNamesTheFamilyTheFilesDeclare()
    {
        var declared = SfntFacts.Read(
            File.ReadAllBytes(Path.Combine(SiteDir, "assets", "fonts", "EQBuddySans.ttf")))
            .Name(1);

        Assert.Equal("EQBuddy Sans", declared);
        Assert.Contains($"font-family: \"{declared}\"", Css);
    }

    /// <summary>**The page's second spoken promise.** Nothing it loads at paint time comes from
    /// another origin — no CDN font, no analytics, no hosted stylesheet. Navigation links to
    /// GitHub are fine: a visitor CLICKING one is not this page fetching anything.</summary>
    [Fact]
    public void ThePageFetchesNothingFromAThirdParty() =>
        Assert.Empty(RemoteLoads(Html).Concat(RemoteLoads(Css)));

    /// <summary>**The prove-fail.** A hosted font and an analytics script are exactly what the
    /// promise forbids, and the scanner sees both.</summary>
    [Fact]
    public void TheRequestScannerFindsAHostedFontAndAnAnalyticsScript() =>
        Assert.Equal(
            ["https://fonts.googleapis.com/css2?family=Inter", "https://example.test/a.js"],
            RemoteLoads(ThirdPartyMarkup));

    /// <summary>And it leaves an ordinary outbound LINK alone — the footer's licence and repo
    /// links are navigation, not loads, and a guard that fired on them would be unusable.
    /// </summary>
    [Fact]
    public void TheRequestScannerLeavesNavigationLinksAlone() =>
        Assert.Empty(RemoteLoads(
            """<a href="https://github.com/DranakCorps-bot/EQBuddy">the repo</a>"""));

    // ----- the committed negatives -----

    /// <summary>What the stylesheet looked like between swapping the faces and measuring the
    /// weight scale: three real static faces, and a 650 with no face to resolve to.</summary>
    private const string StaticFacesWithNoSixFiftyFace = """
        @font-face { font-family: "EQBuddy Sans"; src: url("../fonts/EQBuddySans.ttf"); font-weight: 400; }
        @font-face { font-family: "EQBuddy Sans"; src: url("../fonts/EQBuddySans-SemiBold.ttf"); font-weight: 600; }
        @font-face { font-family: "EQBuddy Sans"; src: url("../fonts/EQBuddySans-Bold.ttf"); font-weight: 700; }
        body { font-family: "EQBuddy Sans", "Segoe UI", sans-serif; }
        .topbar .brand { font-weight: 650; }
        .kicker .num { font-weight: 700; }
        """;

    /// <summary>The Inter sheet this slice replaced. One face, one RANGE, everything covered —
    /// it was never broken, and the scanner must agree.</summary>
    private const string VariableFaceCoveringEverything = """
        @font-face {
          font-family: "Inter";
          src: url("../fonts/InterVariable.woff2") format("woff2");
          font-weight: 100 900;
        }
        body { font-family: "Inter", "Segoe UI", sans-serif; }
        .topbar .brand { font-weight: 650; }
        .kicker .num { font-weight: 700; }
        """;

    /// <summary>The two shapes the no-third-party-requests promise forbids.</summary>
    private const string ThirdPartyMarkup = """
        <link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Inter">
        <a href="https://github.com/DranakCorps-bot/EQBuddy">repo</a>
        <script src="https://example.test/a.js"></script>
        """;

    // ----- the scanners, as pure functions over text -----

    /// <summary>Every asset under <c>site/assets/img</c> or <c>site/assets/media</c> the page
    /// draws, keyed the way the manifest keys them.</summary>
    internal static HashSet<string> DrawnAssets(string html) =>
        Regex.Matches(html, @"src=""assets/(?<asset>(?:img|media)/[^""]+)""")
            .Select(m => m.Groups["asset"].Value)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>Weights the sheet asks for that no <c>@font-face</c> of the primary family
    /// supplies. <c>400</c> and <c>700</c> are always asked for, declared or not: body copy is
    /// 400 and every <c>h1</c>-<c>h6</c>, <c>b</c> and <c>strong</c> is bold by default.</summary>
    internal static IReadOnlyList<int> WeightsWithNoFace(string css)
    {
        var faces = new List<(string Family, int Lo, int Hi)>();
        var blocks = Regex.Matches(css, @"@font-face\s*\{(?<body>[^}]*)\}");
        foreach (Match block in blocks)
        {
            var body = block.Groups["body"].Value;
            var family = Regex.Match(body, @"font-family:\s*""(?<name>[^""]+)""");
            var weight = Regex.Match(body, @"font-weight:\s*(?<lo>\d+)(?:\s+(?<hi>\d+))?");
            if (!family.Success) continue;

            var lo = weight.Success ? int.Parse(weight.Groups["lo"].Value) : 400;
            var hi = weight.Success && weight.Groups["hi"].Success
                ? int.Parse(weight.Groups["hi"].Value)
                : lo;
            faces.Add((family.Groups["name"].Value, lo, hi));
        }

        // The family the page is actually SET in — the first one the body's stack names.
        var primary = Regex.Match(
            Regex.Replace(css, @"@font-face\s*\{[^}]*\}", ""),
            @"font-family:\s*""(?<name>[^""]+)""");
        if (!primary.Success) return [];

        var name = primary.Groups["name"].Value;
        var asked = Regex.Matches(
                Regex.Replace(css, @"@font-face\s*\{[^}]*\}", ""),
                @"font-weight:\s*(?<w>\d+)")
            .Select(m => int.Parse(m.Groups["w"].Value))
            .Concat([400, 700])
            .ToHashSet();

        return asked
            .Where(w => !faces.Any(f =>
                string.Equals(f.Family, name, StringComparison.Ordinal) && w >= f.Lo && w <= f.Hi))
            .Order()
            .ToArray();
    }

    /// <summary>Every absolute URL the markup makes the browser FETCH — stylesheets, scripts,
    /// images, media and <c>@font-face</c> sources. An <c>&lt;a href&gt;</c> is navigation and is
    /// deliberately not one.</summary>
    internal static IReadOnlyList<string> RemoteLoads(string markup)
    {
        var found = new List<string>();

        foreach (Match tag in Regex.Matches(markup, @"<(?<tag>link|script|img|source|video)\b[^>]*>"))
        {
            var attr = tag.Groups["tag"].Value == "link" ? "href" : "src";
            var url = Regex.Match(tag.Value, $@"{attr}=""(?<url>[^""]*)""");
            if (url.Success && IsRemote(url.Groups["url"].Value))
                found.Add(url.Groups["url"].Value);
        }

        foreach (Match src in Regex.Matches(markup, @"src:\s*url\(""(?<url>[^""]*)""\)"))
            if (IsRemote(src.Groups["url"].Value))
                found.Add(src.Groups["url"].Value);

        return found;

        static bool IsRemote(string url) =>
            url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("//", StringComparison.Ordinal);
    }

    /// <summary>The characters a visitor can actually see: text content, plus the <c>alt</c> and
    /// <c>title</c> values that surface when an image fails or a pointer rests.</summary>
    internal static string VisibleText(string html)
    {
        var text = new StringBuilder();

        var stripped = Regex.Replace(html, @"<!--.*?-->", " ", RegexOptions.Singleline);
        stripped = Regex.Replace(
            stripped, @"<(script|style)\b[^>]*>.*?</\1>", " ", RegexOptions.Singleline);

        foreach (Match attr in Regex.Matches(stripped, @"\b(?:alt|title)=""(?<v>[^""]*)"""))
            text.Append(attr.Groups["v"].Value).Append(' ');

        text.Append(Regex.Replace(stripped, @"<[^>]*>", " "));

        return text.Replace("&amp;", "&").Replace("&nbsp;", " ").ToString();
    }

    // ----- fixtures -----

    public static TheoryData<string, string> ManifestRows()
    {
        var rows = new TheoryData<string, string>();
        foreach (var (asset, recipe) in Manifest) rows.Add(asset, recipe);
        return rows;
    }

    private static string Html => ReadRepoFile(Path.Combine("site", "index.html"));

    private static string Css =>
        ReadRepoFile(Path.Combine("site", "assets", "css", "landing.css"));

    private static string SiteDir => Path.Combine(RepoRoot(), "site");

    private static string ReadRepoFile(string relative)
    {
        var path = Path.Combine(RepoRoot(), relative);
        Assert.True(File.Exists(path), $"{relative} is gone.");
        return File.ReadAllText(path);
    }

    private static string RepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "EQBuddy.slnx")))
            d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
