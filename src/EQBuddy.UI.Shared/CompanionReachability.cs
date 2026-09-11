namespace EQBuddy.UI.Shared;

/// <summary>What the pairing window has actually OBSERVED about whether any device can
/// reach this PC. Ordered from "nothing yet" to "a real device got here".</summary>
public enum CompanionReach
{
    /// <summary>EQBuddy Mobile is switched off; there is nothing to reach.</summary>
    NotRunning,

    /// <summary>Running, code on screen, and not enough time has passed to conclude
    /// anything. Saying "blocked" here would be guessing at a stopwatch.</summary>
    Waiting,

    /// <summary>Browsers HAVE connected, and every one of them came from this machine.
    /// The dangerous state: it looks like success and proves nothing.</summary>
    OnlyThisPc,

    /// <summary>The code has been up long enough and nothing at all has connected.</summary>
    NothingArrived,

    /// <summary>A device that is not this machine completed a connection. Whatever else
    /// is wrong, the network path is open.</summary>
    Reached,
}

/// <summary>
/// Does anything OTHER than this PC actually reach the companion listener?
///
/// <para>DRA-64. A Founder smoke found the phone unable to load the companion page while
/// "pasting the full URL into the PC's browser works" — and that second fact was read as
/// evidence the server was fine. It is not evidence of anything. The server binds LAN
/// addresses only, so the PC's browser connects to the PC's own LAN address, and Windows
/// routes a machine's traffic to its own address internally: it never crosses the wire and
/// is never filtered by the inbound firewall. The one test everybody reaches for first is
/// the one test that cannot fail for the reason being investigated.</para>
///
/// <para>So this class refuses to reason about it. It asks the WIRE (trap 75): has a
/// connection ever arrived whose remote address is not one of ours? That single bit
/// separates "nothing is getting through" from "something got here and was refused" —
/// and the second half is what DRA-60's #550/#552 already made the page say out loud.
/// A same-machine connection is counted SEPARATELY rather than ignored, because telling
/// the player their own test proved nothing is the entire lesson of this incident.</para>
///
/// <para>The escalation copy names causes in the order they actually bite, and names the
/// firewall one the way it actually goes wrong. Windows' allow-list is keyed on the
/// EXECUTABLE PATH but displayed by NAME, so a player who was told "check Windows
/// Security → Firewall → Allow an app" finds "eqbuddy.exe" already ticked, concludes the
/// firewall is fine, and stops — while the rule is pointing at an install path this build
/// no longer runs from. That is exactly what DRA-64 turned out to be: the v2 build runs
/// from <c>%LOCALAPPDATA%\EQBuddy Evolved\publish\</c> and every allow rule on the machine
/// named the v1 path. Hence <see cref="FirewallRuleCommand"/> — the rule has to name THIS
/// file, so the window prints this file's path and hands over the whole command
/// (CLAUDE.md: a surface that needs a command must SHIP the command).</para>
///
/// <para>Nothing here reads or writes the firewall. Checking is read-only advice and
/// fixing is one elevated command the player runs knowingly; EQBuddy makes no netsh or
/// elevation calls of its own, exactly as <c>CompanionServer</c>'s header has said since
/// the spike.</para>
/// </summary>
public static class CompanionReachability
{
    /// <summary>How long the code sits on screen before silence becomes a finding. A scan,
    /// an unlock and a page load is a handful of seconds; a minute of nothing is not
    /// someone still reaching for their phone.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(45);

    /// <summary>The verdict, as a pure function of what was measured.</summary>
    /// <param name="running">Is the listener up.</param>
    /// <param name="offBoxConnects">Connections accepted from an address that is not this
    /// machine's — cumulative over the listener's life.</param>
    /// <param name="sameMachineConnects">Connections accepted from this machine itself.</param>
    /// <param name="sinceShown">How long the player has been looking at the pairing code.
    /// The clock starts when the window opens, not when the app did: a PC that has been
    /// running all evening has not been "failing to pair" all evening.</param>
    public static CompanionReach Verdict(
        bool running, int offBoxConnects, int sameMachineConnects, TimeSpan sinceShown)
    {
        if (!running) return CompanionReach.NotRunning;
        if (offBoxConnects > 0) return CompanionReach.Reached;
        // Deliberately BEFORE the patience gate. A player who just tested from this PC's
        // browser is holding a result they are about to misread, and the correction is
        // worth more the sooner it arrives.
        if (sameMachineConnects > 0) return CompanionReach.OnlyThisPc;
        return sinceShown < Patience ? CompanionReach.Waiting : CompanionReach.NothingArrived;
    }

    /// <summary>The one line that states the measurement. Never a cause — this sentence
    /// may only say what was observed.</summary>
    public static string Headline(CompanionReach reach) => reach switch
    {
        CompanionReach.Reached =>
            "A device on your network has reached this PC.",
        CompanionReach.OnlyThisPc =>
            "Only this PC's own browser has reached EQBuddy so far.",
        CompanionReach.NothingArrived =>
            "Nothing has reached this PC yet.",
        CompanionReach.Waiting =>
            "Waiting for a device to connect…",
        _ => "",
    };

    /// <summary>The paragraph under the headline. <see cref="CompanionReach.OnlyThisPc"/>
    /// is the one that exists because of DRA-64: it spends its whole length taking away a
    /// result the player believes, so it says WHY the result is empty rather than just
    /// asserting that it is.</summary>
    public static string Detail(CompanionReach reach) => reach switch
    {
        CompanionReach.Reached =>
            "The network path is open, so a page that still looks wrong is a problem on " +
            "the device — reload it, and if it asks for the code again, rescan.",
        CompanionReach.OnlyThisPc =>
            "That test cannot tell you anything about your phone. When this PC opens its " +
            "own address, the connection never leaves the machine and is never checked " +
            "against the firewall — Windows always lets a PC talk to itself. It will work " +
            "whether or not a phone can get in. Scan or type the address on the phone " +
            "itself; until something that is not this PC connects, nothing below has been " +
            "ruled out.",
        CompanionReach.NothingArrived =>
            "No connection has arrived from anywhere, which means the phone is not being " +
            "refused — it is not getting here at all. In order of likelihood:",
        CompanionReach.Waiting =>
            "Scan the code, or type the address into the device's browser.",
        _ => "",
    };

    /// <summary>Does this verdict warrant showing the "it isn't getting here" checklist?
    /// <see cref="CompanionReach.OnlyThisPc"/> counts: nothing has been ruled out.</summary>
    public static bool ShowsChecklist(CompanionReach reach) =>
        reach is CompanionReach.NothingArrived or CompanionReach.OnlyThisPc;

    /// <summary>Why the firewall cause gets missed, and the path the rule must name. The
    /// path is passed in rather than read here so the window shows the file it is actually
    /// running as, and so this stays a pure function.</summary>
    public static string FirewallCause(string exePath) =>
        OperatingSystem.IsWindows()
            ? "1. Windows Firewall has not been told to allow THIS copy of EQBuddy. Its " +
              "allow-list is keyed on the program's location but shows only the name, so " +
              "an \"eqbuddy.exe\" already ticked there can easily be an older install — " +
              "finding one and assuming you are covered is the usual way this is missed. " +
              $"The rule has to name this exact file:\n\n{exePath}\n\n" +
              "The button below copies a command that adds it; run it once in an " +
              "Administrator PowerShell, then reload the page on the phone."
            : OperatingSystem.IsMacOS()
            ? "1. macOS has not been told to allow incoming connections for EQBuddy — " +
              "System Settings → Network → Firewall → Options. If the first-run prompt " +
              "was declined, this is where it is undone."
            : "1. A local firewall (ufw, firewalld) is blocking the port; most Linux " +
              "desktops never prompt, so nothing will have told you. The port has to be " +
              "opened by hand.";

    /// <summary>The causes that are not the firewall, in the order they bite. Kept apart
    /// from <see cref="FirewallCause"/> so the OS branch does not have to repeat them.</summary>
    public static string OtherCauses =>
        "2. This PC's network is set to Public. Windows blocks this on purpose there — " +
        "set your home Wi-Fi to Private (Settings → Network → Wi-Fi → your network).\n" +
        "3. The phone is on a different network from this PC — a guest SSID, or cellular " +
        "with Wi-Fi off. Check the phone's Wi-Fi name against this PC's.\n" +
        "4. Your Wi-Fi keeps devices apart. Guest networks usually do, and some routers " +
        "call it \"AP isolation\"; a network doing that cannot carry this feature at all.";

    /// <summary>The command that adds the rule, naming the running executable and the
    /// port actually bound. Scoped to the Private profile and to this one port, because a
    /// diagnostic aid has no business opening more than the thing being diagnosed — and a
    /// player on a Public network is told to fix the network category instead
    /// (<see cref="OtherCauses"/>), never to punch a hole in the Public profile.</summary>
    public static string FirewallRuleCommand(string exePath, int port) =>
        $"New-NetFirewallRule -DisplayName \"EQBuddy Mobile\" -Direction Inbound " +
        $"-Action Allow -Program \"{exePath}\" -Protocol TCP -LocalPort {port} " +
        $"-Profile Private";

    /// <summary>Label for the copy button beside the command.</summary>
    public const string CopyCommandLabel = "Copy firewall command";

    /// <summary>Said after the copy, so the player knows the click did something AND that
    /// the click is not the fix — the elevation is theirs to give (trap 17's shape: an
    /// affordance whose effect is invisible has to say what it did).</summary>
    public const string CopiedNotice =
        "Copied. Paste it into an Administrator PowerShell, then reload the page on the " +
        "phone.";
}
