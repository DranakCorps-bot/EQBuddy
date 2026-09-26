namespace EQBuddy.E2E;

/// <summary>
/// **ONE read of the dump, looked up by KEY** — what <see cref="AppHarness.WaitForDumpMoment"/>
/// answers, and the shape every two-host agreement assertion in this suite now takes its pair
/// from (DRA-248).
///
/// <para><b>Why a keyed moment and not another positional helper.</b> DRA-225 gave the Camps
/// and Drops pairs <see cref="AppHarness.WaitForDumpValues"/> plus `ShellHostTests.Ten`, which
/// names ten positions so the assertion still reads as its pair. The sibling sites run from
/// one pair to twenty-two, and a tuple per arity would be a family of helpers each of which
/// can silently swap two positions. A key cannot be off by one: `m["kills"]` beside
/// `m["shellLiveKillRows"]` reads as the pair it is about, whatever the count.</para>
///
/// <para><b>A key the wait did not require is REFUSED, not answered -1.</b> The whole point of
/// the moment is that every value in it came off a read that carried every key; a key read
/// that the wait never asked for would answer from no read at all, and -1 compared with -1 is
/// an agreement between two absences (trap 39).</para>
/// </summary>
public sealed class DumpMoment
{
    private readonly Dictionary<string, int> _values = new(StringComparer.Ordinal);

    internal DumpMoment(IReadOnlyList<string> keys, IReadOnlyList<int> values)
    {
        for (var i = 0; i < keys.Count; i++)
            _values[keys[i]] = values[i];
    }

    public int this[string key] => _values.TryGetValue(key, out var value)
        ? value
        : throw new ArgumentException(
            $"'{key}' is not one of the keys this moment was waited for "
            + $"({string.Join(", ", _values.Keys)}) — add it to the WaitForDumpMoment call, "
            + "so it comes off the same read as the value it is compared with.", nameof(key));
}
