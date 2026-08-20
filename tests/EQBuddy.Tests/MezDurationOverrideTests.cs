using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// A player's typed mez duration outranks everything EQBuddy works out, exactly as a
/// typed spawn duration does (a Reddit ask relayed by David, 2026-08-20: "is there a way
/// to set the timer for mezzes manually? I seem to need to do a timer reset multiple
/// times per session").
///
/// Before this there was no way to say it. Mez durations came from the catalog, were
/// overwritten by whatever a clean fade measured, and a wrong learned value could only be
/// removed by deleting `mez-durations.json` by hand — which is not a thing a player
/// should have to know.
/// </summary>
public class MezDurationOverrideTests
{
    private static readonly DateTime T0 = new(2026, 8, 20, 20, 0, 0);

    private static MezTracker Tracker(MezOverrides? overrides = null)
    {
        var t = new MezTracker();
        if (overrides is not null) t.AttachOverrides(overrides);
        return t;
    }

    /// <summary>With nothing typed and nothing learned, the shipped duration stands.</summary>
    [Fact]
    public void TheCatalogIsTheStartingPoint()
    {
        var (seconds, source) = Tracker().ResolveDuration("Mesmerize");
        Assert.Equal(24, seconds);
        Assert.Equal(MezDurationSource.Catalog, source);
    }

    /// <summary>A typed value beats the catalog...</summary>
    [Fact]
    public void ATypedDurationOutranksTheCatalog()
    {
        var o = new MezOverrides();
        var t = Tracker(o);
        o.Set("Mesmerize", 44);

        var (seconds, source) = t.ResolveDuration("Mesmerize");
        Assert.Equal(44, seconds);
        Assert.Equal(MezDurationSource.Typed, source);
    }

    /// <summary>...and it beats what EQBuddy measured, which is the whole point: the
    /// player is correcting an inference, so an inference must not win it back.</summary>
    [Fact]
    public void ATypedDurationOutranksAMeasuredOne()
    {
        var o = new MezOverrides();
        var t = Tracker(o);
        Land(t, "Mesmerize", "skeleton", T0);
        Fade(t, "Mesmerize", "skeleton", T0.AddSeconds(1 + 30));   // 30s asleep
        Assert.Equal(MezDurationSource.Learned, t.ResolveDuration("Mesmerize").Source);

        o.Set("Mesmerize", 44);
        Assert.Equal((44, MezDurationSource.Typed), t.ResolveDuration("Mesmerize"));

        // ...and a later clean fade still cannot take it back.
        Land(t, "Mesmerize", "skeleton", T0.AddMinutes(5));
        Fade(t, "Mesmerize", "skeleton", T0.AddMinutes(5).AddSeconds(1 + 36));
        Assert.Equal((44, MezDurationSource.Typed), t.ResolveDuration("Mesmerize"));
    }

    /// <summary>Learning goes on underneath a typed value, so clearing the box lands on
    /// what EQBuddy has seen SINCE — not on whatever it knew the day you typed over it.
    /// That is the difference between a manual value being a pause and being a
    /// lobotomy.</summary>
    [Fact]
    public void ClearingATypedValueFallsBackToWhatWasLearnedInTheMeantime()
    {
        var o = new MezOverrides();
        var t = Tracker(o);
        o.Set("Mesmerize", 44);

        Land(t, "Mesmerize", "skeleton", T0);
        Fade(t, "Mesmerize", "skeleton", T0.AddSeconds(1 + 36));   // learned quietly
        Assert.Equal(44, t.ResolveDuration("Mesmerize").Seconds);

        o.Set("Mesmerize", null);
        Assert.Equal((36, MezDurationSource.Learned), t.ResolveDuration("Mesmerize"));
    }

    /// <summary>One typed value covers every rank of the spell. A character casts one
    /// rank at a time, and asking for the same number twice would be a worse answer than
    /// being occasionally stale after an upgrade.</summary>
    [Fact]
    public void ATypedDurationCoversEveryRankOfThatSpell()
    {
        var o = new MezOverrides();
        var t = Tracker(o);
        o.Set("Mesmerization IV", 44);

        Assert.Equal(44, t.ResolveDuration("Mesmerization").Seconds);
        Assert.Equal(44, t.ResolveDuration("Mesmerization II").Seconds);
        Assert.Equal(44, o.Find("Mesmerization VII"));
    }

    /// <summary>Typed values survive a restart, and are NOT put through the learned
    /// store's healing pass on the way in. That pass exists because the cache is
    /// guessing — it floors to the server tick and refuses anything under the catalog
    /// base. A player saying "mine is shorter than the book" is allowed to say it.</summary>
    [Fact]
    public void TypedValuesPersistAndAreNeverHealed()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mez-overrides-{Guid.NewGuid():N}.json");
        try
        {
            var o = MezOverrides.Load(path);
            o.Set("Mesmerize", 11);          // under the 24s catalog base, and 11 is no tick
            o.Set("Enthrall", 50);

            var reborn = MezOverrides.Load(path);
            Assert.Equal(11, reborn.Find("Mesmerize"));
            Assert.Equal(50, reborn.Find("Enthrall"));
            Assert.Equal(11, Tracker(reborn).ResolveDuration("Mesmerize").Seconds);
        }
        finally { File.Delete(path); }
    }

    /// <summary>A corrupt file costs the typed values and nothing else.</summary>
    [Fact]
    public void ACorruptFileFailsSafely()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mez-overrides-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{ this is not json");
            var o = MezOverrides.Load(path);
            Assert.Null(o.Find("Mesmerize"));
            Assert.Equal(24, Tracker(o).ResolveDuration("Mesmerize").Seconds);   // catalog stands
        }
        finally { File.Delete(path); }
    }

    /// <summary>The typed duration is what a landing's chip actually counts down —
    /// resolution and the chip must not be two different answers.</summary>
    [Fact]
    public void AChipCountsDownTheTypedDuration()
    {
        var o = new MezOverrides();
        var t = Tracker(o);
        o.Set("Mesmerize", 44);
        Land(t, "Mesmerize", "skeleton", T0);

        var chip = Assert.Single(t.Snapshot(T0.AddSeconds(1)));
        Assert.Equal(T0.AddSeconds(1 + 44), chip.ExpiresAt);
    }

    // ---- a bare number here is SECONDS, not minutes ----

    [Theory]
    [InlineData("24", 24)]           // the whole reason this parser is separate
    [InlineData("44", 44)]
    [InlineData("90s", 90)]
    [InlineData("1m", 60)]
    [InlineData("1m30s", 90)]
    [InlineData("1:30", 90)]
    [InlineData("7.5", 7.5)]
    public void ABareNumberIsSeconds(string text, double seconds) =>
        Assert.Equal(seconds, MezDurationText.Parse(text));

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("soon")]
    public void NoiseParsesToNothing(string text) => Assert.Null(MezDurationText.Parse(text));

    /// <summary>The spawn parser is untouched — "24" there still means 24 MINUTES. The
    /// two live side by side and mean different things on purpose.</summary>
    [Fact]
    public void TheSpawnParserStillReadsBareNumbersAsMinutes() =>
        Assert.Equal(1440, SpawnDurationText.Parse("24"));

    // ---- helpers: the two log lines that drive a mez's life ----

    private static void Land(MezTracker t, string spell, string target, DateTime at)
    {
        // The cast is what makes an unexplained landing trustworthy (class summary).
        t.Apply(new SpellCastEvent(at, spell));
        t.Apply(new MezzedEvent(at.AddSeconds(1), target));
    }

    private static void Fade(MezTracker t, string spell, string target, DateTime at) =>
        t.Apply(new SpellWornOffEvent(at, spell, target));
}
