using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// Abilities that take over a basic attack. Reported by a monk who uses Round Kick and
/// Tiger Claw but only saw "Kick" and "Strike" in the damage breakdown.
///
/// The cause: the substituted attack keeps logging under the original verb ("You kick orc
/// pawn for 15 points of damage."), and the game says so exactly once, when the ability is
/// earned — lines transcribed from eqlog_Hugzee, 2026-07-30 10:18.
/// </summary>
public class SkillSubstitutionTests
{
    private static string At(int hh, int mm, string msg) =>
        $"[Thu Jul 30 {hh:D2}:{mm:D2}:00 2026] {msg}";

    private static SessionStats Replay(params string[] lines)
    {
        var stats = new SessionStats { CharacterName = "Hugzee" };
        foreach (var line in lines)
            if (LogParser.Parse(line) is { } evt) stats.Apply(evt);
        return stats;
    }

    [Theory]
    [InlineData("You will now use Round Kick instead of Kick while attacking.", "Round Kick", "Kick")]
    [InlineData("You will now use Slam instead of Bash while attacking.", "Slam", "Bash")]
    public void SubstitutionIsParsed(string line, string ability, string replaced)
    {
        var e = Assert.IsType<SkillSubstitutionEvent>(LogParser.Parse($"[Thu Jul 30 10:18:00 2026] {line}"));
        Assert.Equal(ability, e.Ability);
        Assert.Equal(replaced, e.Replaced);
    }

    /// <summary>The announcement is the only thing that says these hits are Round Kick.</summary>
    [Fact]
    public void HitsAfterTheSwapAreFiledUnderTheAbility()
    {
        var s = Replay(
            At(10, 18, "You will now use Round Kick instead of Kick while attacking."),
            At(10, 20, "You kick an orc centurion for 15 points of damage."),
            At(10, 21, "You kick an orc centurion for 12 points of damage.")).Snapshot();

        var row = Assert.Single(s.DamageBySource, d => d.Name == "Round Kick");
        Assert.Equal(27, row.Total);
        Assert.Equal(2, row.Hits);
        Assert.DoesNotContain(s.DamageBySource, d => d.Name == "Kick");
    }

    /// <summary>Hits from before the swap really were plain kicks, so they stay put rather
    /// than being retroactively relabelled.</summary>
    [Fact]
    public void HitsBeforeTheSwapKeepTheOldName()
    {
        var s = Replay(
            At(10, 0, "You kick an orc centurion for 9 points of damage."),
            At(10, 18, "You will now use Round Kick instead of Kick while attacking."),
            At(10, 20, "You kick an orc centurion for 15 points of damage.")).Snapshot();

        Assert.Equal(9, s.DamageBySource.Single(d => d.Name == "Kick").Total);
        Assert.Equal(15, s.DamageBySource.Single(d => d.Name == "Round Kick").Total);
    }

    /// <summary>The substitution outlives the session. The game announces it once, when the
    /// ability is earned — often days before the session being watched — and a 60-minute
    /// idle gap in between must not silently turn Round Kick back into Kick.</summary>
    [Fact]
    public void TheSubstitutionSurvivesASessionRollover()
    {
        var stats = Replay(
            At(10, 18, "You will now use Round Kick instead of Kick while attacking."),
            At(10, 20, "You kick an orc centurion for 15 points of damage."));

        // Two hours later: SessionGap has passed, so this starts a fresh session.
        foreach (var line in (string[])[At(12, 30, "You kick an orc centurion for 11 points of damage.")])
            if (LogParser.Parse(line) is { } evt) stats.Apply(evt);

        var s = stats.Snapshot();
        var row = Assert.Single(s.DamageBySource, d => d.Name == "Round Kick");
        Assert.Equal(11, row.Total);   // the new session only
    }

    /// <summary>Only melee is affected — a spell called "Kick" (or a substitution naming
    /// something a spell also uses) must not rewrite spell rows.</summary>
    [Fact]
    public void SpellDamageIsNotRelabelled()
    {
        var s = Replay(
            At(10, 18, "You will now use Round Kick instead of Kick while attacking."),
            At(10, 20, "Orc centurion has taken 30 damage from your Kick.")).Snapshot();

        // A DoT tick's row is "<Spell> (DoT)" since 2026-09-29 — still the spell's own name,
        // never the melee substitution's.
        Assert.Single(s.DamageBySource, d => d.Name == "Kick" + OutputKinds.DotSuffix);
        Assert.DoesNotContain(s.DamageBySource, d => d.Name.StartsWith("Round Kick"));
    }

    /// <summary>The biggest-hit line should name the ability too, not the verb it hides behind.</summary>
    [Fact]
    public void TheBiggestHitNamesTheAbility()
    {
        var s = Replay(
            At(10, 18, "You will now use Round Kick instead of Kick while attacking."),
            At(10, 20, "You kick an orc centurion for 54 points of damage.")).Snapshot();

        Assert.Contains("Round Kick", s.MaxHitDesc);
    }
}
