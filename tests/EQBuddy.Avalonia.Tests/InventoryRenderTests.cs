using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia.Tests;

/// <summary>
/// The Inventory tab over a synthetic dump — no game folder, no network.
///
/// It was TWO windows until 2026-08-21 (<c>InventoryWindow</c> and <c>GearLockerWindow</c>),
/// and these were their tests. They read the same file, so Windows folded them into one tab
/// with two pivots a release earlier and this build did not — which is the 1.98.1 parity
/// gap: <see cref="LootSurface.Hosted"/> is SHARED Core vocabulary that named a tab this
/// widget could not draw.
///
/// The assertions barely changed, and that is the point of the surface taking delegates
/// rather than a live widget: the same inputs, the same expected text, one host instead of
/// two. What is NEW here is the pivot — the half that could not exist while they were
/// separate windows — and the absence of a scroller (trap 36).
/// </summary>
[Collection("avalonia")]
public sealed class InventoryRenderTests : IDisposable
{
    private readonly string _profile = Directory.CreateTempSubdirectory("eqbuddy-inventory-render-").FullName;

    public InventoryRenderTests() =>
        // The pivot SAVES, so this suite writes settings now where the two windows never
        // did. Isolate the profile before anything can reach the real one.
        Environment.SetEnvironmentVariable("EQBUDDY_APPDATA", _profile);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("EQBUDDY_APPDATA", null);
        try { Directory.Delete(_profile, recursive: true); }
        catch (Exception ex) { Console.Error.WriteLine($"profile cleanup failed: {ex.Message}"); }
    }

    private static InventoryFile.Snapshot Snapshot() =>
        new("/game/Vahlara_teek-Inventory.txt", DateTime.Now.AddMinutes(-5), new Dictionary<string, int>())
        {
            Entries =
            [
                new InventoryFile.Entry("Primary", "Rusty Broad Sword", 1),
                new InventoryFile.Entry("General 1", "Backpack", 1),
                new InventoryFile.Entry("General 1-Slot1", "Bone Chips", 3),
                new InventoryFile.Entry("Bank 1", "Fine Steel Long Sword", 1),
            ],
            SinceDump = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Words of Odus"] = 2,
            },
        };

    private InventoryView View(Func<bool, InventoryFile.Snapshot?> latest,
        EqlWikiItemService? wiki = null, bool byContainer = false)
    {
        var view = new InventoryView(
            new AppSettings { InventoryByContainer = byContainer },
            wiki ?? new EqlWikiItemService(Path.Combine(_profile, "items"),
                _ => Task.FromResult<string?>(null)),
            latest, () => [], () => null);
        view.Render();
        return view;
    }

    private static List<string?> Texts(Control body) =>
        body.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();

    [AvaloniaFact]
    public void ByBagShowsWornBagsBankAndLootSinceTheDump()
    {
        var texts = Texts(View(_ => Snapshot(), byContainer: true).Body);

        Assert.Contains("Worn", texts);
        Assert.Contains("Rusty Broad Sword", texts);
        Assert.Contains("Backpack  (General 1 — 1 item)", texts);
        Assert.Contains("Bone Chips ×3", texts);
        Assert.Contains("Elsewhere", texts);
        Assert.Contains("Fine Steel Long Sword", texts);
        Assert.Contains("Looted since this dump (1)", texts);
        Assert.Contains("Words of Odus ×2", texts);
        Assert.Contains(texts, t => t?.StartsWith("Vahlara_teek-Inventory.txt — written") == true);
    }

    [AvaloniaFact]
    public void WithoutADumpItExplainsTheOutputfileRecipe()
    {
        // Both pivots, because the empty state is the one every new player meets and the
        // two used to be two windows that could disagree about it.
        foreach (var byContainer in new[] { false, true })
            Assert.Contains(Texts(View(_ => null, byContainer: byContainer).Body),
                t => t?.StartsWith("No inventory dump found yet") == true);
    }

    [AvaloniaFact]
    public void BySlotOffersOneCountedFetchForItemsWithoutStats()
    {
        // A name no catalog or cache knows: it must land in the not-fetched group and
        // be offered by the explicit, counted fetch button — never fetched silently.
        var wiki = new EqlWikiItemService(Path.Combine(_profile, "items"),
            _ => Task.FromResult<string?>(null));
        var snapshot = new InventoryFile.Snapshot(
            "/game/Vahlara_teek-Inventory.txt", DateTime.Now, new Dictionary<string, int>())
        {
            Entries = [new InventoryFile.Entry("Primary", "Zzz Testblade of Nowhere", 1)],
        };
        var view = View(_ => snapshot, wiki);

        Assert.Contains("STATS NOT FETCHED YET (1)", Texts(view.Body));
        var fetch = view.Body.GetLogicalDescendants().OfType<Button>()
            .Single(b => b.Content?.ToString()?.StartsWith("⇣ fetch stats for") == true);
        Assert.True(fetch.IsVisible);
        Assert.Equal("⇣ fetch stats for 1 item", fetch.Content);
    }

    /// <summary>The half that could not exist while these were two windows: ONE tab, and
    /// the checkbox actually swaps which surface is drawn. By slot is the default because
    /// "what should I swap" is the actionable question; by bag is the occasional lookup,
    /// and only it names where a thing physically is.</summary>
    [AvaloniaFact]
    public void ThePivotSwapsBetweenRankedSlotsAndWhereThingsAre()
    {
        var settings = new AppSettings();
        var view = new InventoryView(settings,
            new EqlWikiItemService(Path.Combine(_profile, "items"),
                _ => Task.FromResult<string?>(null)),
            _ => Snapshot(), () => [], () => null);
        view.Render();

        // By slot: the comparison advice, and no bag structure.
        Assert.Contains(Texts(view.Body), t => t?.StartsWith("★ = what you're wearing") == true);
        Assert.DoesNotContain("Backpack  (General 1 — 1 item)", Texts(view.Body));

        var pivot = view.Body.GetLogicalDescendants().OfType<CheckBox>()
            .Single(c => (c.Content as string ?? "").StartsWith("Group by bag"));
        pivot.IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.True(settings.InventoryByContainer);   // and it persists, like the Wishlist pivot
        Assert.Contains("Backpack  (General 1 — 1 item)", Texts(view.Body));
        Assert.DoesNotContain(Texts(view.Body), t => t?.StartsWith("★ = what you're wearing") == true);

        pivot.IsChecked = false;
        Dispatcher.UIThread.RunJobs();
        Assert.False(settings.InventoryByContainer);
        Assert.Contains(Texts(view.Body), t => t?.StartsWith("★ = what you're wearing") == true);
    }

    /// <summary>Trap 36, asserted rather than remembered. A lifted view that brings its own
    /// ScrollViewer sits inside the host's, gets measured with INFINITE height, never
    /// overflows, never scrolls — and still handles the wheel, so the outer scroller never
    /// sees it. It cost David a working mouse wheel on Windows for a day, and nothing in a
    /// diff, a unit test or a screenshot shows it: the scrollbar is right there and looks
    /// correct. This is the one thing that CAN see it.</summary>
    [AvaloniaFact]
    public void ItBringsNoScrollerOfItsOwnBecauseScrollingBelongsToTheHost()
    {
        var view = View(_ => Snapshot());
        Assert.Empty(view.Body.GetLogicalDescendants().OfType<ScrollViewer>());
    }

    /// <summary>The tab's headline. Null rather than "0" before there is a dump, because
    /// the honest statement then is not "you have no upgrades" — it is "EQBuddy has not been
    /// told what you own"; and the real count once there is one, since that number is what
    /// a player uses to decide whether to switch tabs at all.
    ///
    /// The fixture's two swords produce "2 upgrades" against the shipped item catalog. That
    /// was NOT the number predicted when this was written, and the prediction was the thing
    /// at fault: both are real catalog items with real stats, so an unworn Fine Steel Long
    /// Sword genuinely beats what this character has on. Asserted against the count the
    /// arithmetic produces rather than a spelled-out "2", so a catalog refresh moving the
    /// number is not a red test.</summary>
    [AvaloniaFact]
    public void TheBadgeSaysNothingUntilThereIsSomethingToSay()
    {
        Assert.Null(View(_ => null).Badge);

        var view = View(_ => Snapshot());
        var upgrades = Texts(view.Body).Count(t => t?.StartsWith("⬆ ") == true);
        Assert.Equal(upgrades > 0 ? $"{upgrades} upgrade{(upgrades == 1 ? "" : "s")}" : null,
            view.Badge);
    }
}
