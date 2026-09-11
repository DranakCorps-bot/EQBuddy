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

    /// <summary>The address picker's heading. It only appears when this PC actually has
    /// more than one address to offer — a picker with one row is furniture that implies
    /// a choice the player does not have.</summary>
    public const string AddressLabel = "Address the code points at";

    /// <summary>#264, brhanson2-cyber: "the link it gives me is the ip address of my
    /// ethernet, not my wifi... How do I force it to give me a link using the wifi ip".
    /// The ranking now prefers Wi-Fi, and this is the part a ranking cannot do — say what
    /// the choice means and hand it over.</summary>
    public const string AddressHint =
        "This PC is on more than one network. EQBuddy picks the Wi-Fi one, because that is " +
        "the network your phone is on — pick a different one here if the page won't load.";

    /// <summary>The "let EQBuddy decide" row, which is where a fresh profile starts and
    /// the way back from a pin that turned out to be wrong.</summary>
    public const string AddressAuto = "Choose automatically";

    /// <summary>One row of the picker. Wireless is NAMED because it is the whole question
    /// being asked; nothing is called "wired", because the list also holds VPN and virtual
    /// adapters and calling one of those ethernet would be a guess presented as a
    /// fact.</summary>
    public static string AddressChoice(string address, string adapterDescription, bool wireless)
    {
        var desc = (adapterDescription ?? "").Trim();
        var suffix = wireless
            ? desc.Length == 0 ? "Wi-Fi" : $"Wi-Fi · {desc}"
            : desc;
        return suffix.Length == 0 ? address : $"{address} — {suffix}";
    }

    public const string RegenerateLabel = "New code (disconnects every paired device)";

    public const string RegenerateTip =
        "Mints a fresh pairing code; every device has to scan again";

    public const string GateHeading = "Screens offered to devices";

    public const string GateHint =
        "Untick anything you'd rather never leave this PC. Each device then picks its own " +
        "screens (the settings button on the page) from what's offered.";

    /// <summary>What connected devices there are, in words. Both widgets show the same
    /// line, so the pluralization is decided once.</summary>
    public static string Status(int clients) => Status(clients, clients);

    /// <summary>The same line, told apart by ORIGIN (DRA-64).
    ///
    /// <para>A count alone was actively misleading. The companion server binds LAN
    /// addresses only, so the first thing anybody does when a phone won't load is paste
    /// the URL into the PC's own browser — and this line then read "1 device connected",
    /// which is true of a browser and reads as "a device paired". The Founder's smoke
    /// ended with the PC reporting success while no phone had ever reached the machine.
    /// A browser on this PC is NEVER counted as a device here; it is named as what it
    /// is.</para></summary>
    /// <param name="clients">Total connected browsers.</param>
    /// <param name="offBox">Of those, the ones that are not this PC's own browser.</param>
    public static string Status(int clients, int offBox) => (clients, offBox) switch
    {
        (0, _) => "No device connected yet.",
        (_, 0) when clients == 1 => "This PC's own browser is connected — no device yet.",
        (_, 0) => $"{clients} browsers on this PC connected — no device yet.",
        (_, 1) when clients == 1 => "1 device connected.",
        (_, 1) => "1 device connected (plus this PC's own browser).",
        _ when clients == offBox => $"{offBox} devices connected.",
        _ => $"{offBox} devices connected (plus this PC's own browser).",
    };

    /// <summary>The honest firewall talk (see `CompanionServer`'s header), for the OS the
    /// player is actually on. A first listen prompts on Windows, prompts once and is
    /// remembered on macOS, and on most Linux desktops does not prompt at all — telling
    /// all three the same story is how a player concludes the feature is broken when the
    /// truth is that nothing asked them anything.
    ///
    /// <para>This paragraph is now only the EXPECTATION — what should happen the first
    /// time. Everything it used to say about diagnosing a failure was removed in DRA-64,
    /// for two reasons. It listed causes nobody had measured, when the server can measure
    /// the only one that matters (<see cref="CompanionReachability"/>). And its concrete
    /// advice was wrong in the exact case it was written for: "Windows Security →
    /// Firewall → Allow an app" shows program NAMES, so a player whose EQBuddy moved
    /// install paths finds an "eqbuddy.exe" already ticked and concludes they are
    /// covered. Worse, it told the player to test by opening the address on the PC — the
    /// one test that passes no matter what the firewall does.</para></summary>
    public static string Firewall =>
        OperatingSystem.IsWindows()
            ? "First time on, Windows Firewall usually asks whether to allow EQBuddy — say " +
              "yes, and tick Private networks. If that prompt never appeared, or was " +
              "dismissed, the phone's connection is dropped with nothing on screen to say " +
              "so; the line below is EQBuddy watching for exactly that."
        : OperatingSystem.IsMacOS()
            ? "First time on, macOS asks whether to allow incoming connections for " +
              "EQBuddy — say yes, and it remembers (System Settings → Network → Firewall " +
              "→ Options if you need to change it later). If that prompt was declined, the " +
              "phone's connection is dropped silently; the line below watches for that."
            : "Most Linux desktops will not prompt at all — if a firewall is running " +
              "(ufw, firewalld), the port above has to be opened by hand, and until it is, " +
              "the page simply never loads with nothing on screen to say why. The line " +
              "below is EQBuddy watching for exactly that.";
}
