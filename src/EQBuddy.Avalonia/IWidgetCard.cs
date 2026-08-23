using Avalonia.Controls;
using EQBuddy.Core;

namespace EQBuddy.Avalonia;

/// <summary>
/// A surface the widget or a theme window hosts — the Avalonia twin of
/// <c>EQBuddy/IWidgetCard.cs</c>, name for name (Fable 5's PR A, 2026-08-22).
///
/// **Why this lane got the seam second, and why it had to get it.** On WPF a card is an
/// object: each host builds its own and nothing is ever moved. On Avalonia the same
/// surfaces were ~17 <c>MainWindow</c> fields composed into panels, handed to
/// <c>ProgressWindow</c> by reference through <c>IProgressHost.ProgressTabBody</c>. That is
/// a control moving between two <c>TopLevel</c>s, and **Avalonia throws on it**:
/// <c>Attempt to call InvalidateArrange on wrong LayoutManager</c> — an open upstream bug
/// since 11.2 (avalonia#12753, #17906, #21267), still present in the 12.1.1 we ship, with
/// no public API that makes it safe. Six attempts to sequence the hand-off failed because
/// the operation is unsupported, not mis-sequenced.
///
/// It survived this long only because a closed window's presentation source is cleared, so
/// the reopen move passed by null — until it did not, which is the crash that shipped to
/// Linux and macOS in every theme window and was fixed in 1.99.4.
///
/// → **The rule, and it is absolute: a control NEVER moves between two windows here.**
/// Every host builds its own instance through <see cref="ProgressSurfaceSet"/>, and no host
/// interface returns a <c>Control</c> it did not just create. Guarded by
/// <c>SurfaceOwnershipTests</c>.
/// </summary>
internal interface IWidgetCard
{
    /// <summary>The section key — the same one <c>UI.Shared.OverlaySections</c>,
    /// <c>SectionOrder</c> and <c>HiddenSections</c> use, so a card needs no separate
    /// identity and cannot disagree with the settings about who it is.</summary>
    string Key { get; }

    /// <summary>What hangs inside the card's expander, or in the theme window's tab.</summary>
    Control Body { get; }

    /// <summary>Paint from this tick. Called only while the surface is actually shown —
    /// which replaces the <c>ProgressTabShowing</c> gates that used to sit inside the paint
    /// code. One rule, in the host, exactly as WPF's <c>ThemeCardView</c> has it.</summary>
    void Render(StatsSnapshot snapshot);
}

/// <summary>
/// What a card may ask the widget for — the Avalonia twin of WPF's <c>ICardContext</c>,
/// and deliberately the same six members so a reader of one lane can read the other.
///
/// Small on purpose, and it should stay that way. Every member is something a card
/// genuinely cannot answer for itself: opening a shared window, reaching the wiki cache,
/// or asking for the current tick. Anything computable from a snapshot belongs in
/// <c>UI.Shared</c> instead. **If this interface starts growing, that is the signal that
/// something is being pushed into cards that should have gone to UI.Shared.**
/// </summary>
internal interface ICardContext
{
    /// <summary>Open the shared item-info popup on this item.</summary>
    void ShowItemInfo(string itemName);

    /// <summary>Does any tracked quest want this item? Drives the quest badge.</summary>
    bool IsActiveQuestItem(string itemName);

    /// <summary>Hover text for an item, quest marker included.</summary>
    string? QuestAwareTooltip(string itemName, string? baseTip);

    /// <summary>Cached wiki stats for an item, or the hint that a click fetches them.</summary>
    string ItemHoverStats(string itemName);

    /// <summary>Open the Quest Tracker filtered to this item's quests.</summary>
    void OpenQuestInfoForItem(string itemName);

    /// <summary>The tick most recently painted, for a surface that needs to repaint itself
    /// after the player changes one of its own filters.</summary>
    StatsSnapshot CurrentSnapshot();
}

/// <summary>
/// The five surfaces the PROGRESS THEME hosts (docs/Themes.md), as one value.
///
/// **A factory result, never a cached field.** <c>MainWindow.NewProgressSurfaces()</c>
/// builds a fresh set on every call and hands it to whoever asked; the widget keeps none.
/// That is what makes the never-move rule structural rather than remembered — there is no
/// shared instance for a second host to take.
/// </summary>
/// <param name="Experience">XP, AAs, skill-ups, dings and what they unlocked.</param>
/// <param name="Money">Coin, and the items the log saw sold (#74).</param>
/// <param name="Motes">The Potential upgrade-currency ladder (#49, flipwon).</param>
/// <param name="Faction">Standing, per faction, with the per-kill deltas.</param>
/// <param name="Raids">Raid targets cleared — witnessed, or imported from achievements.</param>
internal sealed record ProgressSurfaceSet(
    ProgressCardView Experience,
    MoneyCardView Money,
    MotesCardView Motes,
    FactionCardView Faction,
    RaidsCardView Raids);
