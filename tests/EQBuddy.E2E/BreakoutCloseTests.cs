using System.Text.Json;

namespace EQBuddy.E2E;

/// <summary>
/// THE ✕ ON A FLOATING WINDOW IS A TRANSIENT CLOSE (OE-7) — it takes the window off screen
/// and writes NOTHING.
///
/// This is the half of the seat a unit test cannot reach. <c>HudExpandTests</c> proves every
/// kind has a chip to be summoned back from, which is the premise; whether the ✕ still
/// reaches <c>AppSettings.DisabledBreakouts</c> is a fact about a running widget and a file
/// on disk. Until OE-7 it added the kind and called <c>Save()</c>, so a player who clicked a
/// 11px glyph over their game lost the window until they found the Settings list that put it
/// back (discussion #45 is why it was permanent, and it is the whole reason the seat had to
/// ship the chips in the same PR).
///
/// **The settings FILE is asserted, not the dump's copy of it.** The old code's write was
/// `Add` + `Save`, so the thing that would prove a regression is bytes on disk — and reading
/// them is also what makes this proof against a future in which the in-memory list is
/// mutated without persisting.
///
/// [Collection("e2e")] because these launch a real always-on-top widget and two at once
/// would fight for the desktop (traps 57 / 61).
/// </summary>
[Collection("e2e")]
public sealed class BreakoutCloseTests
{
    /// <summary>The profile's own <c>settings.json</c>, as the app last left it.</summary>
    private static string[] DisabledBreakouts(AppHarness app)
    {
        using var doc = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(app.ProfileDir, "settings.json")));
        return doc.RootElement.TryGetProperty("DisabledBreakouts", out var list)
            ? [.. list.EnumerateArray().Select(e => e.GetString() ?? "")]
            : [];
    }

    /// <summary>
    /// ✕ on the Damage float: the window goes, the setting does not move.
    ///
    /// **The negative is anchored to a positive on the far side of the close** (trap 62).
    /// "DisabledBreakouts still has one entry" is true before the app has done anything at
    /// all, so asserting it after a launch would pass against a widget that never opened a
    /// float — and it would have passed against the pre-OE-7 tree for the first second or so
    /// too. `breakoutsClosed` can only reach 1 once <c>BreakoutWindow.Dismiss</c> has run and
    /// <c>BreakoutHost</c> has recorded it, and the old code's `Save()` sat in that same
    /// dispatcher callback — so a write, if there were one, is on disk by the time this wait
    /// returns.
    ///
    /// The prediction, written before it ran (trap 23): the fixture profile ships
    /// `DisabledBreakouts = ["Healing"]` here deliberately, so the number to hold is ONE both
    /// before and after — not zero, which any empty-list bug would also produce.
    /// </summary>
    [Fact]
    public void ClosingAFloatDoesNotWriteTheSetting()
    {
        using var app = new AppHarness(settings =>
        {
            settings.Minimized = true;
            // "dps" is seeded and then STRIPPED by MigratePromotedHudStats on load — it is
            // here because that migration reads the star before removing it, and a profile
            // WITHOUT it is read as "this player had the Damage window off" and gets
            // "Damage" written into DisabledBreakouts. Seeding it is what makes the Damage
            // float open at all, which is the window this test closes.
            settings.MiniStats = ["kills", "dps"];
            // Damage may open (no star gates it since SA-1); Healing is the seeded off row
            // and the number this test holds. The other four have no star, so they stay shut
            // without needing a row of their own.
            settings.DisabledBreakouts = ["Healing"];
            settings.DefaultRulesVersion = int.MaxValue;
            settings.TrackedRules.Clear();
        }, new Dictionary<string, string> { ["EQBUDDY_BREAKOUTCLOSE"] = "Damage" });
        app.Launch();

        Assert.Equal(["Healing"], DisabledBreakouts(app));

        app.WaitForDump("breakoutsClosed", 1, "the ✕ to record a transient close");
        // And the whole point: nothing reached the file. Read AFTER the wait above, so the
        // moment this is true at is "the close has happened", not "the app has started".
        Assert.Equal(["Healing"], DisabledBreakouts(app));
        app.WaitForDump("breakoutsDisabled", 1, "the setting to be untouched in memory too");
    }

    /// <summary>
    /// Nothing is closed until something closes it — the state every player who has
    /// configured nothing sees.
    ///
    /// Same shape as <c>HudExpandTests.NothingIsExpandedUntilSomethingExpandsIt</c> and for
    /// the same reason: a `WaitForDump(key, 0)` straight after a launch is satisfied by the
    /// zero that was already there. `hudGlance` is the positive that can only be written once
    /// <c>HudBarView.Render</c> has run, which is the tick that also runs the gate this is
    /// about.
    /// </summary>
    [Fact]
    public void NothingIsClosedUntilSomethingClosesIt()
    {
        using var app = new AppHarness(settings =>
        {
            settings.Minimized = true;
            // "dps" is seeded and then STRIPPED by MigratePromotedHudStats on load — it is
            // here because that migration reads the star before removing it, and a profile
            // WITHOUT it is read as "this player had the Damage window off" and gets
            // "Damage" written into DisabledBreakouts. Seeding it is what makes the Damage
            // float open at all, which is the window this test closes.
            settings.MiniStats = ["kills", "dps"];
            settings.DisabledBreakouts = ["Healing"];
            settings.DefaultRulesVersion = int.MaxValue;
            settings.TrackedRules.Clear();
        });
        app.Launch();

        app.WaitForDump("hudGlance", "xp", "the collapsed bar to draw its trio");
        app.WaitForDump("breakoutsClosed", 0, "no float to have been dismissed");
        app.WaitForDump("breakoutsDisabled", 1, "the seeded row and nothing else");
    }
}
