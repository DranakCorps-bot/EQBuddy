using System.Buffers.Binary;
using System.Text;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>The other half of <see cref="IconFontCoverageTests"/>, and the reason
/// it was not enough. That test pins which CODEPOINTS the bundled font carries; it
/// never opens the .ttf, so it was blind to everything about the font that is not a
/// cmap entry — which is where the real defect was (reported 2026-08-21 from CrossOver
/// on macOS):
///
/// The family shipped ONE face, Regular/400, while the WPF app asks for SemiBold or
/// Bold in 71 places. With no face to resolve to, WPF synthesises the weight by
/// smearing the Regular outlines wider without touching their sidebearings or their
/// kern pairs — so every bold run has broken letterfit, and only bold runs do. On
/// Windows it never appears, because Segoe UI Variable supplies the real faces.
///
/// The same blindness hid a second one: Theme.xaml's SectionLabel style asks for
/// Typography.Capitals=AllSmallCaps on ~40 headings, and the build script had
/// dropped smcp/c2sc as "unused features". WPF does not synthesise small caps, so
/// those headings silently lost both their case and their tracking.
///
/// So this reads the tables directly. A missing weight, a dropped feature, or a
/// face that quietly renames its family are each one assertion here and invisible
/// everywhere else — no compiler, no XAML parse, and no screenshot taken on Windows
/// can see any of them.
/// </summary>
public class BundledFontFaceTests
{
    private const string Family = "EQBuddy Sans";

    /// <summary>Every weight the WPF app can ask for. WPF matches a FontWeight to a
    /// face by usWeightClass; anything not in this list is synthesised.</summary>
    public static TheoryData<string, string, int> Faces => new()
    {
        { "EQBuddySans.ttf", "Regular", 400 },
        { "EQBuddySans-SemiBold.ttf", "SemiBold", 600 },
        { "EQBuddySans-Bold.ttf", "Bold", 700 },
    };

    [Theory]
    [MemberData(nameof(Faces))]
    public void EachBundledFaceCarriesItsWeightAndFamily(string file, string style, int weightClass)
    {
        var font = OpenFace(file);

        // nameID 16/17 (typographic family/subfamily) are what put three files in
        // one family. Split them and WPF sees three families of one weight each,
        // which is the same defect as shipping Regular alone.
        Assert.Equal(Family, font.Name(16));
        Assert.Equal(style, font.Name(17));
        Assert.Equal(weightClass, font.WeightClass);
    }

    [Theory]
    [MemberData(nameof(Faces))]
    public void EachBundledFaceKeepsTheLayoutFeaturesTheAppRequests(string file, string style, int weightClass)
    {
        _ = style;
        _ = weightClass;
        var font = OpenFace(file);

        // kern: the whole reason a text font is bundled rather than an icon-only
        // one. smcp + c2sc: Typography.Capitals=AllSmallCaps in Theme.xaml.
        Assert.Contains("kern", font.Features("GPOS"));
        Assert.Contains("smcp", font.Features("GSUB"));
        Assert.Contains("c2sc", font.Features("GSUB"));
    }

    /// <summary>Every face must carry the icons, not just the Regular one. A bold
    /// run containing a section icon resolves to the BOLD face, and Wine's
    /// DirectWrite has no fallback to catch what that face is missing — it boxes
    /// (WineFonts.cs). The manifest is one file for the family, so it is only
    /// truthful if all three agree with it.</summary>
    [Theory]
    [MemberData(nameof(Faces))]
    public void EveryFaceCoversTheWholeIconManifest(string file, string style, int weightClass)
    {
        _ = style;
        _ = weightClass;
        var font = OpenFace(file);
        var manifest = File.ReadAllLines(Path.Combine(FontsDir, "EQBuddySans.codepoints.txt"))
            .Where(l => l.Length > 0)
            .Select(l => Convert.ToInt32(l, 16))
            .ToList();

        var missing = manifest.Where(cp => !font.Cmap.Contains(cp)).ToList();

        Assert.True(missing.Count == 0,
            $"{file} is missing codepoints the family manifest promises — re-run " +
            "scripts/build-icon-font.py:\n" +
            string.Join("\n", missing.Select(cp => $"  U+{cp:X5}")));
    }

    /// <summary>The csproj is what actually puts a face in the .exe; a face on disk
    /// that nobody packs is a weight WPF still cannot resolve at runtime.</summary>
    [Fact]
    public void EveryBundledFaceIsPackedAsAResource()
    {
        var csproj = File.ReadAllText(Path.Combine(SrcDir, "EQBuddy", "EQBuddy.csproj"));

        foreach (var file in Faces.Select(row => (string)row[0]))
            Assert.Contains($@"<Resource Include=""Fonts\{file}"" />", csproj);
    }

    // ---- the Font picker's bundled faces (discussion #1046, DRA-1048) ----

    /// <summary>Every face the picker bundles: folder, file, typographic family, style, weight.
    /// The same 400/600/700 the app asks for, for the same reason as above — a picked face
    /// whose SemiBold is synthesised is the CrossOver letterfit defect on Windows.</summary>
    public static TheoryData<string, string, string, string, int> PickerFaces => new()
    {
        { "AtkinsonHyperlegibleNext", "AtkinsonHyperlegibleNext-Regular.ttf", "Atkinson Hyperlegible Next", "Regular", 400 },
        { "AtkinsonHyperlegibleNext", "AtkinsonHyperlegibleNext-SemiBold.ttf", "Atkinson Hyperlegible Next", "SemiBold", 600 },
        { "AtkinsonHyperlegibleNext", "AtkinsonHyperlegibleNext-Bold.ttf", "Atkinson Hyperlegible Next", "Bold", 700 },
        { "OpenSans", "OpenSans-Regular.ttf", "Open Sans", "Regular", 400 },
        { "OpenSans", "OpenSans-SemiBold.ttf", "Open Sans", "SemiBold", 600 },
        { "OpenSans", "OpenSans-Bold.ttf", "Open Sans", "Bold", 700 },
    };

    /// <summary>The three files of a family must group under ONE typographic family, or WPF
    /// sees three one-weight families and synthesises the other two. Upstream statics carry
    /// nameID 16/17 only on the non-RIBBI face (SemiBold) and let Regular/Bold fall back to
    /// IDs 1/2 — which is what WPF does too, so that is what is read here.</summary>
    [Theory]
    [MemberData(nameof(PickerFaces))]
    public void EachPickerFaceCarriesItsWeightUnderOneFamily(string folder, string file, string family,
        string style, int weightClass)
    {
        var font = OpenPickerFace(folder, file);
        Assert.Equal(family, font.Name(16) ?? font.Name(1));
        Assert.Equal(style, font.Name(17) ?? font.Name(2));
        Assert.Equal(weightClass, font.WeightClass);
        // Theme.xaml asks for tabular numerals on every TextBlock; a face without tnum lets
        // counters and countdowns jitter as they tick.
        Assert.Contains("tnum", font.Features("GSUB"));
    }

    /// <summary>Each family the picker names is the family the files carry, and the folder
    /// <see cref="UI.Shared.AppFontChoice"/> addresses is the folder they are in — a renamed
    /// folder or a typo'd family is a pick that silently draws Segoe UI.</summary>
    [Fact]
    public void EveryBundledPickerOptionMatchesTheFacesOnDisk()
    {
        var bundled = UI.Shared.AppFontChoice.Options.Where(o => o.IsBundled).ToList();
        Assert.NotEmpty(bundled);
        var rows = PickerFaces.Select(r => ((string)r[0], (string)r[2])).Distinct().ToList();
        Assert.Equal(rows.OrderBy(r => r.Item1),
            bundled.Select(o => (o.BundledFolder!, o.Family)).OrderBy(r => r.Item1));
    }

    /// <summary>OFL 1.1 travels with the font: each bundled family's folder carries its own
    /// licence text.</summary>
    [Fact]
    public void EveryPickerFamilyShipsItsOflLicence()
    {
        foreach (var folder in PickerFaces.Select(r => (string)r[0]).Distinct())
        {
            var path = Path.Combine(FontsDir, folder, "OFL.txt");
            Assert.True(File.Exists(path), $"{folder} has no OFL.txt");
            Assert.Contains("SIL OPEN FONT LICENSE Version 1.1", File.ReadAllText(path));
        }
    }

    [Fact]
    public void EveryPickerFaceIsPackedAsAResource()
    {
        var csproj = File.ReadAllText(Path.Combine(SrcDir, "EQBuddy", "EQBuddy.csproj"));
        foreach (var row in PickerFaces)
            Assert.Contains($@"<Resource Include=""Fonts\{row[0]}\{row[1]}"" />", csproj);
    }

    private static SfntFacts OpenPickerFace(string folder, string file)
    {
        var path = Path.Combine(FontsDir, folder, file);
        Assert.True(File.Exists(path), $"{folder}/{file} is not in src/EQBuddy/Fonts");
        return SfntFacts.Read(File.ReadAllBytes(path));
    }

    private static string SrcDir =>
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src");

    private static string FontsDir => Path.Combine(SrcDir, "EQBuddy", "Fonts");

    private static SfntFacts OpenFace(string file)
    {
        var path = Path.Combine(FontsDir, file);
        Assert.True(File.Exists(path),
            $"{file} is not in src/EQBuddy/Fonts — run scripts/build-icon-font.py");
        return SfntFacts.Read(File.ReadAllBytes(path));
    }
}
