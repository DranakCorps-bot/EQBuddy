namespace EQBuddy.UI.Shared;

/// <summary>
/// **WHAT THE WIDGET'S RIGHT-CLICK MENU SHOWS WHILE IT IS MINIMIZED — the ≤4 lock.**
///
/// Bevel's cog/Options IA faces (`docs/BEVEL-cog-options-ia-faces.md` §B), Helm-signed
/// 2026-09-08 ~2:13 PM CT and amended by the owner ~2:15 PM CT the same day. The menu is
/// ONE menu — it hangs off the shared root border, so the minimized bar and the expanded
/// widget have always shown the same eleven rows — and the finding was the click path a
/// minimized player actually walks: expand, hunt, choose, then the same again to get back.
///
/// **Four rows, and they are all DOORS.** Every one opens a surface you look away for:
/// <see cref="MiniRows"/> in that order. Nothing else survives the ≤4 test on the mini
/// surface — click-through, Edit HUD, session history, the data chores and the help links
/// are all still one right-click away on the EXPANDED widget, which is where a player who
/// is not mid-pull already is.
///
/// **`Guide…` is the shell's door AND the progression surface, and that is the ~2:15
/// amendment rather than a tidy-up.** `Open EQBuddy…` (OE-2) and `Quests…` were two rows
/// that both meant "leave the overlay and go read something", and one of them was a second
/// "open the app" label on a bar that already IS the app. They are one row now: it opens
/// the Evolved shell on the Guide room, and if the shell was closed that same click is the
/// OE-2 recovery. The recovery did not move — the LABEL did, and the label now names the
/// destination rather than the window.
///
/// **Why this list lives in UI.Shared rather than in the XAML alone.** The rows are declared
/// in `MainWindow.xaml` and hidden by `Tag="expanded"`, which is exactly the kind of pair
/// that goes quiet: a row added without the tag is an eleventh item on the mini menu that
/// compiles, runs, photographs as an ordinary menu (trap 29) and fails no test. The list
/// here is what `WidgetMenuTests` reads the XAML against, in both directions — a row missing
/// from mini and a row that should not be there.
/// </summary>
public static class WidgetMenuPolicy
{
    /// <summary>The four rows a MINIMIZED widget's menu shows, verbatim and in order.
    /// These are the `Header` attributes in `MainWindow.xaml`; `WidgetMenuTests` asserts
    /// the file against them, so a rename here without a rename there fails the build.</summary>
    public static readonly IReadOnlyList<string> MiniRows =
    [
        "Options…",
        "World…",
        "Mobile…",
        "Guide…",
    ];

    /// <summary>The marker on every menu item and separator that belongs to the EXPANDED
    /// widget only. It is a <c>Tag</c> rather than a name list in code because the menu is
    /// declared in XAML and a new row's author edits the XAML — the tag is the thing they
    /// are looking at when they decide.</summary>
    public const string ExpandedOnlyTag = "expanded";

    /// <summary>The row that opens the Evolved shell on the Guide room (and recovers it when
    /// the ✕ took it). Named here because three places have to agree on the spelling: the
    /// XAML row, the `EQBUDDY_EXPAND` dump fact the E2E suite asserts, and the
    /// "No longer on the widget" list, which names the door a player still has (trap 59).</summary>
    public const string GuideRow = "Guide…";

    /// <summary>The <c>page:room</c> address <see cref="GuideRow"/> opens. It is the Quests
    /// page's wire key — the ROOM was renamed to Guide, the ADDRESS was not, because
    /// `page:room` is persisted and read back and the shell must never re-spell a room
    /// (<see cref="ShellPages"/>' own rule).</summary>
    public static string GuideAddress => ShellPages.Key(ShellPage.Quests);
}
