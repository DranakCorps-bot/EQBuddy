using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia.Tests;

/// <summary>
/// Drops by Creature rendered headlessly: per-creature grouping with the kill
/// denominator, the quest badge, and the new-to-wiki marker fed by the host's mob-lookup
/// memo. Report/export formatting is Core's (DropsReport/WikiContribution).
///
/// It was a WINDOW until 2026-08-21 and these were its tests. It is a tab of the Kills &amp;
/// Drops theme now, and the assertions barely changed — which is the point of the surface
/// having taken IDropsHost from the start rather than the widget. What DID change is the
/// two badges: they were click-handled EMOJI, and emoji box outright under Wine (#148,
/// #166) while a bare vector only hit-tests where it is painted (#211). Both are
/// InlineIconButtons now, so the assertions look for the icon rather than the glyph.
/// </summary>
[Collection("avalonia")]
public sealed class DropsRenderTests
{
    private sealed class FakeHost : IDropsHost
    {
        public AppSettings Settings { get; } = new();
        public (string Character, string Server) Identity { get; init; } = ("", "");
        public string CurrentZoneName { get; init; } = "";
        public Func<string, MobLookupResult?> MobResult { get; init; } = _ => null;
        public HashSet<string> QuestItems { get; init; } = [];
        public List<string> LookupsFired { get; } = [];
        public StatsSnapshot CurrentSnapshot() => new();
        public string? CachedItemStats(string itemName) => null;
        public Task<string?> FetchItemTooltip(string itemName) => Task.FromResult<string?>(null);
        public MobLookupResult? WikiMobResult(string name) => MobResult(name);
        public void EnsureMobLookup(string name) => LookupsFired.Add(name);
        public List<string> Rechecks { get; } = [];
        public void RecheckMobLookup(string name) => Rechecks.Add(name);
        public bool IsRechecking(string name) => false;
        public bool IsActiveQuestItem(string name) => QuestItems.Contains(name);
        public void OpenQuestInfoForItem(string itemName) { }
        public int WikiPackOpened { get; private set; }
        public void ShowWikiPack() => WikiPackOpened++;
    }

    private static StatsSnapshot Snapshot() => new()
    {
        Mobs =
        [
            new MobSummary("a moss snake", 2, 2, 10, 1.0, 0,
                [new MobLoot("Snake Fang", 1, 50.0), new MobLoot("Snake Scales", 2, 100.0)]),
            new MobSummary("orc pawn", 1, 1, 8, 0.5, 12,
                [new MobLoot("Crude Stein", 1, null)]),
        ],
    };

    [AvaloniaFact]
    public void RendersCreatureGroupsWithRatesBadgesAndStars()
    {
        var host = new FakeHost
        {
            Identity = ("Tester", "p1999"),
            QuestItems = ["Crude Stein"],
            // a moss snake's page exists but lists no loot → every drop gets a ✦.
            MobResult = name => name == "a moss snake"
                ? new MobLookupResult(new MobInfo { Name = "a moss snake" },
                    ItemLookupState.Cached, DateTime.Now)
                : null,
        };
        var view = new DropsCardView(host);
        view.Render(Snapshot());

        Assert.Contains("a moss snake", host.LookupsFired);
        var text = view.Body.GetLogicalDescendants().OfType<TextBlock>()
            .Select(t => t.Text).ToList();
        Assert.Contains("a moss snake — 2 kills  ·  wiki page lists no loot yet", text);
        Assert.Contains("orc pawn — 1 kill", text);
        Assert.Contains("Snake Fang", text);
        Assert.Contains("  ×1  ·  50% of 2", text);    // observed rate keeps its denominator
        Assert.Contains("  ×1", text);                  // null rate renders count only
        // The two badges, as VECTORS. A glyph assertion could not tell an icon that is
        // drawn from one that is drawn AND clickable, which is the distinction #211 was.
        var icons = view.Body.GetLogicalDescendants().OfType<Button>()
            .Select(IconName).Where(n => n is not null).ToList();
        Assert.Contains("Map", icons);       // quest badge on Crude Stein
        Assert.Contains("Sparkle", icons);   // new-to-wiki marker
        // And a name that is NOT drawn here must not match — the assertion above was
        // vacuous for a week (trap 39), and this is what keeps it from going vacuous again.
        Assert.DoesNotContain("Phone", icons);
    }

    /// <summary>The wiki re-check ↻ on every creature heading (#226), with its freshness
    /// caption. Asserted rather than screenshotted, because an ABSENT control photographs
    /// as an unremarkable header (trap 29/34). The moss snake's page was read just now,
    /// so its button is inside the 30 s rule and disabled; the orc pawn's page was never
    /// read, so its button is live — and pressing it reaches the host.</summary>
    [AvaloniaFact]
    public void EveryCreatureHeadingCarriesAWikiRecheckAndItsFreshness()
    {
        var host = new FakeHost
        {
            MobResult = name => name == "a moss snake"
                ? new MobLookupResult(new MobInfo { Name = "a moss snake", PageTitle = "A Moss Snake" },
                    ItemLookupState.Cached, DateTime.UtcNow)
                : null,
        };
        var view = new DropsCardView(host);
        view.Render(Snapshot());

        var buttons = view.Body.GetLogicalDescendants().OfType<Button>()
            .Where(b => IconName(b) == "Refresh")
            .ToList();
        Assert.Equal(2, buttons.Count);                       // one per creature heading
        // Both LIVE, including the one inside the 30 s window (Bevel, 2026-08-22): the
        // debounce is the wiki's, not the button's — a greyed control reads as broken.
        Assert.All(buttons, b => Assert.True(b.IsEnabled));

        var text = view.Body.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains("wiki just now", text);
        Assert.Contains("wiki not read yet", text);

        foreach (var b in buttons)
            b.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        // Both presses reach the host; the WIKI debounce decides what happens next.
        Assert.Equal(["a moss snake", "orc pawn"], host.Rechecks.OrderBy(x => x).ToList());
    }

    /// <summary>Which catalog icon a button draws, by the name DesignSystem.Icon stamps
    /// on its Tag. This REPLACES a helper that parsed the expected path and compared
    /// ToString() on both sides: StreamGeometry.ToString() is the TYPE NAME, so every
    /// icon compared equal to every other and the #211 assertions could not fail
    /// (trap 39). Found when a count of two came back as four.</summary>
    private static string? IconName(Button b) =>
        (b.Content as global::Avalonia.Controls.PathIcon)?.Tag as string;

    [AvaloniaFact]
    public void FilterNarrowsAndReportsEmptiness()
    {
        var view = new DropsCardView(new FakeHost());
        view.Render(Snapshot());

        var filter = view.Body.GetLogicalDescendants().OfType<TextBox>().First();
        filter.Text = "orc";
        Dispatcher.UIThread.RunJobs();   // TextChanged lands via the dispatcher
        var text = view.Body.GetLogicalDescendants().OfType<TextBlock>()
            .Select(t => t.Text).ToList();
        Assert.Contains("orc pawn — 1 kill", text);
        Assert.DoesNotContain(text, t => t?.StartsWith("a moss snake") == true);

        filter.Text = "no such thing";
        Dispatcher.UIThread.RunJobs();
        text = view.Body.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        // The filter LEADS the signature, or an empty result hashes to the reset sentinel
        // and the stale rows stay on screen. Windows carried that bug until this fold.
        Assert.Contains("Nothing matches that filter.", text);
        Assert.DoesNotContain(text, t => t?.StartsWith("orc pawn") == true);
    }
}
