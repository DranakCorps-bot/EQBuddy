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

    /// <summary>Just enough TrueType to answer the questions above. WPF's own
    /// GlyphTypeface would be easier and is Windows-only, which would leave this
    /// suite — the one that runs everywhere and gates every commit — unable to check
    /// the font at all.</summary>
    private sealed class SfntFacts
    {
        private readonly byte[] _data;
        private readonly Dictionary<string, (int Offset, int Length)> _tables;

        private SfntFacts(byte[] data, Dictionary<string, (int, int)> tables)
        {
            _data = data;
            _tables = tables;
            WeightClass = U16(_tables["OS/2"].Offset + 4);
            Cmap = ReadCmap();
        }

        public int WeightClass { get; }

        public HashSet<int> Cmap { get; }

        public static SfntFacts Read(byte[] data)
        {
            var count = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(4));
            var tables = new Dictionary<string, (int, int)>();
            for (var i = 0; i < count; i++)
            {
                var record = 12 + i * 16;
                var tag = Encoding.ASCII.GetString(data, record, 4);
                tables[tag] = (
                    (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(record + 8)),
                    (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(record + 12)));
            }
            return new SfntFacts(data, tables);
        }

        /// <summary>The Windows/Unicode/en-US record for a name ID, which is the one
        /// every Windows shaper reads.</summary>
        public string? Name(int nameId)
        {
            var (offset, _) = _tables["name"];
            var count = U16(offset + 2);
            var storage = offset + U16(offset + 4);
            for (var i = 0; i < count; i++)
            {
                var record = offset + 6 + i * 12;
                if (U16(record) != 3 || U16(record + 2) != 1 || U16(record + 4) != 0x409) continue;
                if (U16(record + 6) != nameId) continue;
                return Encoding.BigEndianUnicode.GetString(
                    _data, storage + U16(record + 10), U16(record + 8));
            }
            return null;
        }

        /// <summary>Every feature tag registered in GSUB or GPOS.</summary>
        public HashSet<string> Features(string table)
        {
            var found = new HashSet<string>(StringComparer.Ordinal);
            if (!_tables.TryGetValue(table, out var located)) return found;

            // Header: version (4), then offsets to ScriptList, FeatureList, LookupList.
            var featureList = located.Offset + U16(located.Offset + 6);
            var count = U16(featureList);
            for (var i = 0; i < count; i++)
                found.Add(Encoding.ASCII.GetString(_data, featureList + 2 + i * 6, 4));
            return found;
        }

        private HashSet<int> ReadCmap()
        {
            var (offset, _) = _tables["cmap"];
            var found = new HashSet<int>();
            var count = U16(offset + 2);
            for (var i = 0; i < count; i++)
            {
                var record = offset + 4 + i * 8;
                var subtable = offset + (int)BinaryPrimitives.ReadUInt32BigEndian(
                    _data.AsSpan(record + 4));
                // Format 4 covers the BMP, format 12 the supplementary planes the
                // pictographs live in. The build emits both; read whichever appear.
                switch (U16(subtable))
                {
                    case 4: ReadFormat4(subtable, found); break;
                    case 12: ReadFormat12(subtable, found); break;
                }
            }
            return found;
        }

        private void ReadFormat4(int subtable, HashSet<int> into)
        {
            var segCount = U16(subtable + 6) / 2;
            var ends = subtable + 14;
            var starts = ends + segCount * 2 + 2;
            var deltas = starts + segCount * 2;
            var rangeOffsets = deltas + segCount * 2;

            for (var seg = 0; seg < segCount; seg++)
            {
                int start = U16(starts + seg * 2), end = U16(ends + seg * 2);
                if (start == 0xFFFF) continue;
                var rangeOffset = U16(rangeOffsets + seg * 2);
                for (var cp = start; cp <= end && cp != 0xFFFF; cp++)
                {
                    if (rangeOffset == 0)
                    {
                        if (((cp + U16(deltas + seg * 2)) & 0xFFFF) != 0) into.Add(cp);
                        continue;
                    }
                    var glyphAt = rangeOffsets + seg * 2 + rangeOffset + (cp - start) * 2;
                    if (glyphAt + 1 < _data.Length && U16(glyphAt) != 0) into.Add(cp);
                }
            }
        }

        private void ReadFormat12(int subtable, HashSet<int> into)
        {
            var groups = (int)BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(subtable + 12));
            for (var i = 0; i < groups; i++)
            {
                var group = subtable + 16 + i * 12;
                var start = (int)BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(group));
                var end = (int)BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(group + 4));
                for (var cp = start; cp <= end; cp++) into.Add(cp);
            }
        }

        private int U16(int at) => BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(at));
    }
}
