using System.Buffers.Binary;
using System.Text;

namespace EQBuddy.Tests;

/// <summary>Just enough TrueType to answer what the shipped faces actually carry —
/// their weight class, their family name, their registered features and their cmap.
/// WPF's own GlyphTypeface would be easier and is Windows-only, which would leave
/// this suite — the one that runs everywhere and gates every commit — unable to check
/// the font at all.
///
/// <para>Lifted out of <c>BundledFontFaceTests</c> in DRA-56, when the landing page
/// took the same three faces as a webfont and needed the same cmap read to prove the
/// page's own prose is renderable by them (<see cref="LandingSiteTests"/>). One
/// parser, two readers — the app's bundle and the site's.</para></summary>
internal sealed class SfntFacts
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
