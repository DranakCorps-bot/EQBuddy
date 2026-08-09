using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// Share strings travel through guild chat and Discord — hostile territory for exact
/// text. These tests pin the round trip, the paste-tolerance, the checksum, and the
/// import sanitizer (a crafted string must never smuggle what the editor can't type).
/// </summary>
public class WatchRuleShareTests
{
    private static TrackedRule Sample() => new()
    {
        Name = "CH chain 2nd",
        Pattern = "Cassius begins casting Complete Heal",
        Kind = WatchKind.Text,
        AlertBanner = true,
        AlertSound = true,
        AlertSpeech = true,
        AlertSoundName = "Alarm",
        AlertColor = "Purple",
        AlertDelaySeconds = 2.5,
    };

    [Fact]
    public void RoundTripPreservesEveryShareableField()
    {
        var encoded = WatchRuleShare.Encode([Sample()]);
        Assert.StartsWith("EQB1.", encoded);

        var rules = WatchRuleShare.TryDecode(encoded, out var error);
        Assert.Equal("", error);
        var r = Assert.Single(rules!);
        Assert.Equal("CH chain 2nd", r.Name);
        Assert.Equal("Cassius begins casting Complete Heal", r.Pattern);
        Assert.Equal(WatchKind.Text, r.Kind);
        Assert.True(r.AlertBanner);
        Assert.True(r.AlertSound);
        Assert.True(r.AlertSpeech);
        Assert.Equal("Alarm", r.AlertSoundName);
        Assert.Equal("Purple", r.AlertColor);
        Assert.Equal(2.5, r.AlertDelaySeconds);
    }

    [Fact]
    public void ImportedRulesGetFreshIdentity()
    {
        var original = Sample();
        var imported = WatchRuleShare.TryDecode(WatchRuleShare.Encode([original]), out _)!.Single();
        // A shared rule must never collide with the sharer's cooldowns/baselines.
        Assert.NotEqual(original.Id, imported.Id);
        Assert.True(imported.Enabled);
        Assert.True(imported.Pinned);
    }

    [Fact]
    public void TokenIsExtractedFromSurroundingChatText()
    {
        var encoded = WatchRuleShare.Encode([Sample()]);
        var chat = $"[guild] Cassius: here's my CH cue rule, paste into EQBuddy → {encoded} ← enjoy";
        Assert.NotNull(WatchRuleShare.TryDecode(chat, out _));
    }

    [Fact]
    public void TruncationIsCaughtByTheChecksum()
    {
        var encoded = WatchRuleShare.Encode([Sample()]);
        // Chop the middle out, keeping prefix and checksum shape intact.
        var broken = encoded[..(encoded.Length / 2)] + encoded[^11..];
        Assert.Null(WatchRuleShare.TryDecode(broken, out var error));
        Assert.Contains("cut off", error);
    }

    [Fact]
    public void GarbageIsRefusedWithAReadableReason()
    {
        Assert.Null(WatchRuleShare.TryDecode("have you tried turning it off and on", out var error));
        Assert.Contains("EQB1", error);
    }

    [Fact]
    public void CustomSoundPathsStayOnTheSharersMachine()
    {
        var rule = Sample();
        rule.AlertSoundName = @"C:\Users\cassius\sounds\airhorn.wav";
        var imported = WatchRuleShare.TryDecode(WatchRuleShare.Encode([rule]), out _)!.Single();
        Assert.Equal("", imported.AlertSoundName);
    }

    [Fact]
    public void SeveralRulesTravelTogether()
    {
        var heard = Sample();
        var castNow = Sample();
        castNow.Name = "CH cast NOW";
        castNow.AlertDelaySeconds = 6;

        var rules = WatchRuleShare.TryDecode(WatchRuleShare.Encode([heard, castNow]), out _);
        Assert.Equal(2, rules!.Count);
        Assert.Equal(6, rules[1].AlertDelaySeconds);
    }

    [Fact]
    public void SpellFadeClassRulesShareCleanly()
    {
        var rule = new TrackedRule
        {
            Name = "CC broke",
            Kind = WatchKind.SpellFade,
            SpellFilter = SpellFilter.AnyCrowdControl,
            AlertBanner = true,
        };
        var imported = WatchRuleShare.TryDecode(WatchRuleShare.Encode([rule]), out _)!.Single();
        Assert.Equal(SpellFilter.AnyCrowdControl, imported.SpellFilter);
        Assert.Contains("AnyCrowdControl", WatchRuleShare.Describe(imported));
    }

    [Fact]
    public void DescribeSummarizesWhatTheImportWillDo()
    {
        var text = WatchRuleShare.Describe(Sample());
        Assert.Contains("CH chain 2nd", text);
        Assert.Contains("banner", text);
        Assert.Contains("sound: Alarm", text);
        Assert.Contains("delay 2.5s", text);
    }
}
