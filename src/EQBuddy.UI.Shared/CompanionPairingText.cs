namespace EQBuddy.UI.Shared;

/// <summary>
/// Every word the EQBuddy Mobile pairing window says, for both widgets.
///
/// It lived as prose inside `EQBuddy/CompanionWindow.cs` until the Avalonia twin was
/// written (#208) and immediately proved why that does not hold: the copy told the player
/// to open "Windows Security → Firewall", which is not a place a CachyOS or macOS player
/// can go, and naming the page's fullscreen control by its glyph (⛶) drew tofu in the
/// fonts a bare Linux desktop ships. Two windows, one of them wrong, and nothing in a
/// build or a test can see it — the same shape as every other divergence CLAUDE.md's
/// "the decision goes in UI.Shared and all of them call it" rule is written about.
///
/// The firewall paragraph is the one that legitimately differs, so it differs HERE, by
/// operating system, rather than by which widget is asking.
/// </summary>
public static class CompanionPairingText
{
    public const string Title = "EQBuddy Mobile (Beta)";

    public const string Intro =
        "Turn a phone or tablet on the same Wi-Fi into a live EQBuddy display: scan the " +
        "code, the browser opens, and your timers, map and checklists follow you around " +
        "the house. Everything stays on your own network — nothing is hosted, nothing is " +
        "uploaded, and it's off unless you turn it on. Beta: it works, it just hasn't " +
        "been through as many camps as the rest of EQBuddy.";

    public const string EnableLabel = "Enable EQBuddy Mobile";

    public const string UrlHint =
        "Scanning not cooperating? Type this address in the device's browser instead — " +
        "the part after # is the pairing code, keep it:";

    /// <summary>Named in words, not by glyph. ⛶ has no coverage in a default Linux font
    /// set, so pointing at a control by drawing one leaves the reader with a box they
    /// cannot match to anything on the page (#148/#166, in prose).</summary>
    public const string HomeScreenHint =
        "Propping a tablet beside the monitor? Once the page is open, use the browser's " +
        "\"Add to Home Screen\" — it launches EQBuddy Mobile in its own window with no " +
        "address bar, and remembers the pairing code. The fullscreen button at the top of " +
        "the page does the same for one visit.";

    public const string RegenerateLabel = "New code (disconnects every paired device)";

    public const string RegenerateTip =
        "Mints a fresh pairing code; every device has to scan again";

    public const string GateHeading = "Screens offered to devices";

    public const string GateHint =
        "Untick anything you'd rather never leave this PC. Each device then picks its own " +
        "screens (the settings button on the page) from what's offered.";

    /// <summary>What connected devices there are, in words. Both widgets show the same
    /// line, so the pluralization is decided once.</summary>
    public static string Status(int clients) => clients switch
    {
        0 => "No device connected yet.",
        1 => "1 device connected.",
        var n => $"{n} devices connected.",
    };

    /// <summary>The honest firewall talk (see `CompanionServer`'s header), for the OS the
    /// player is actually on. A first listen prompts on Windows, prompts once and is
    /// remembered on macOS, and on most Linux desktops does not prompt at all — telling
    /// all three the same story is how a player concludes the feature is broken when the
    /// truth is that nothing asked them anything.</summary>
    public static string Firewall =>
        OperatingSystem.IsWindows()
            ? "First time on, Windows Firewall usually asks whether to allow EQBuddy — say " +
              "yes for private networks. If the page never loads on your device: that " +
              "prompt was missed (Windows Security → Firewall → Allow an app), or your " +
              "Wi-Fi keeps devices apart (guest networks often do — use the main " +
              "network). Best check: open the address above in that device's browser " +
              "right now."
        : OperatingSystem.IsMacOS()
            ? "First time on, macOS asks whether to allow incoming connections for " +
              "EQBuddy — say yes, and it remembers (System Settings → Network → Firewall " +
              "→ Options if you need to change it later). If the page never loads on your " +
              "device: that prompt was declined, or your Wi-Fi keeps devices apart (guest " +
              "networks often do — use the main network). Best check: open the address " +
              "above in that device's browser right now."
            : "Most Linux desktops will not prompt at all — if a firewall is running " +
              "(ufw, firewalld), the port above has to be opened by hand, and until it is, " +
              "the page simply never loads with nothing on screen to say why. The other " +
              "usual cause is a Wi-Fi that keeps devices apart (guest networks often do — " +
              "use the main network). Best check: open the address above in that device's " +
              "browser right now.";
}
