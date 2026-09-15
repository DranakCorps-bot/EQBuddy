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
