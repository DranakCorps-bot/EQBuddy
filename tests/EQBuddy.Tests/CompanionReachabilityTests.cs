using System.Net;
using EQBuddy.Companion;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// DRA-64. The Founder's phone could not load the companion page; the PC's own browser
/// could, and that was read as "the server is fine". It is not a reading the evidence
/// supports — the PC's browser never crosses the wire — and the pairing window agreed
/// with the wrong reading out loud by counting that browser as "1 device connected".
///
/// <para>These are the arithmetic and the words. The socket half (a real connection from
/// a real second machine) is the one thing that cannot be tested from one box, which is
/// exactly why <see cref="CompanionServer.IsSameMachine"/> is a pure function taking the
/// address set: the decision is testable even though the topology is not.</para>
/// </summary>
public sealed class CompanionReachabilityTests
{
    private static readonly TimeSpan Patient = CompanionReachability.Patience + TimeSpan.FromSeconds(1);
    private static readonly TimeSpan Impatient = TimeSpan.FromSeconds(1);

    // ---------- the verdict ----------

    [Fact]
    public void OffAltogetherIsNotAFailure()
    {
        Assert.Equal(CompanionReach.NotRunning,
            CompanionReachability.Verdict(running: false, 0, 0, Patient));
    }

    [Fact]
    public void SilenceIsNotAFindingUntilTheClockSaysSo()
    {
        // The negative that keeps this honest: a player who has had the window open for a
        // second has not yet failed at anything.
        Assert.Equal(CompanionReach.Waiting,
            CompanionReachability.Verdict(running: true, 0, 0, Impatient));
        Assert.Equal(CompanionReach.NothingArrived,
            CompanionReachability.Verdict(running: true, 0, 0, Patient));
    }

    [Fact]
    public void ThisPcsOwnBrowserNeverReadsAsReached()
    {
        // THE bug. Any number of same-machine connections, however long they have been
        // happening, must never produce Reached.
        foreach (var elapsed in new[] { Impatient, Patient })
        foreach (var n in new[] { 1, 5, 50 })
            Assert.Equal(CompanionReach.OnlyThisPc,
                CompanionReachability.Verdict(running: true, offBoxConnects: 0,
                    sameMachineConnects: n, elapsed));
    }

    [Fact]
    public void OneRealDeviceOutranksEveryLocalBrowser()
    {
        Assert.Equal(CompanionReach.Reached,
            CompanionReachability.Verdict(running: true, offBoxConnects: 1,
                sameMachineConnects: 99, Patient));
    }

    [Fact]
    public void ADeviceThatGotHereEndsTheChecklistImmediately()
    {
        // Counted at accept, so this holds for a phone refused with a stale token too:
        // it arrived, therefore nothing on the "it isn't getting here" list applies.
        Assert.False(CompanionReachability.ShowsChecklist(
            CompanionReachability.Verdict(running: true, 1, 0, Impatient)));
    }

    [Fact]
    public void TheChecklistShowsForBothWaysOfGettingNowhere()
    {
        Assert.True(CompanionReachability.ShowsChecklist(CompanionReach.NothingArrived));
        Assert.True(CompanionReachability.ShowsChecklist(CompanionReach.OnlyThisPc));
        // ...and for no others. A checklist under "a device has reached this PC" would be
        // advice to fix something that is working.
        Assert.False(CompanionReachability.ShowsChecklist(CompanionReach.Reached));
        Assert.False(CompanionReachability.ShowsChecklist(CompanionReach.Waiting));
        Assert.False(CompanionReachability.ShowsChecklist(CompanionReach.NotRunning));
    }

    // ---------- the words ----------

    [Fact]
    public void EveryVerdictWithAChecklistAlsoHasSomethingToSay()
    {
        foreach (var reach in Enum.GetValues<CompanionReach>())
        {
            if (reach == CompanionReach.NotRunning) continue;
            Assert.False(string.IsNullOrWhiteSpace(CompanionReachability.Headline(reach)));
            Assert.False(string.IsNullOrWhiteSpace(CompanionReachability.Detail(reach)));
        }
    }

    [Fact]
    public void TheOnlyThisPcCopyExplainsWhyTheTestWasEmpty()
    {
        // It exists to take away a result the player believes, so asserting it is
        // non-empty proves nothing. It has to name the mechanism.
        var detail = CompanionReachability.Detail(CompanionReach.OnlyThisPc);
        Assert.Contains("never leaves the machine", detail);
        Assert.Contains("firewall", detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NothingArrivedSaysTheDeviceIsNotBeingRefused()
    {
        // The distinction DRA-60's page work turns on: refused is a different bug from
        // never arriving, and the PC is the only side that can tell them apart.
        Assert.Contains("not being refused",
            CompanionReachability.Detail(CompanionReach.NothingArrived));
    }

    [Fact]
    public void TheFirewallAdviceNamesThisExeAndWarnsAboutTheOldOne()
    {
        // The whole of DRA-64: a rule existed, for a path this build no longer runs from,
        // and the old copy sent the player to a list that shows names rather than paths.
        var path = @"C:\Users\david\AppData\Local\EQBuddy Evolved\publish\EQBuddy.exe";
        var cause = CompanionReachability.FirewallCause(path);
        Assert.Contains(path, cause);
        if (OperatingSystem.IsWindows())
        {
            Assert.Contains("older install", cause);
            // The sentence the superseded copy got wrong must not come back.
            Assert.DoesNotContain("Allow an app", cause);
        }
    }

    [Fact]
    public void TheCommandNamesTheRunningFileAndTheBoundPort()
    {
        var path = @"C:\Program Files\EQBuddy\EQBuddy.exe";
        var cmd = CompanionReachability.FirewallRuleCommand(path, 47859);
        Assert.Contains($"\"{path}\"", cmd);     // quoted: the real path has a space in it
        Assert.Contains("47859", cmd);
        Assert.Contains("-Direction Inbound", cmd);
        // Scoped, deliberately: a diagnostic aid does not open the Public profile.
        Assert.Contains("-Profile Private", cmd);
        Assert.DoesNotContain("-Profile Any", cmd);
    }

    [Fact]
    public void TheOtherCausesAreOrderedAndMentionTheNetworkCategory()
    {
        var other = CompanionReachability.OtherCauses;
        Assert.Contains("Public", other);
        Assert.Contains("different network", other);
        Assert.Contains("isolation", other);
    }

    // ---------- the connected line ----------

    [Fact]
    public void TheConnectedLineNeverCallsThisPcsBrowserADevice()
    {
        // The exact line the Founder's PC showed while no phone had ever reached it.
        var line = CompanionPairingText.Status(clients: 1, offBox: 0);
        Assert.DoesNotContain("1 device connected", line);
        Assert.Contains("this PC", line, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no device yet", line, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheConnectedLineStillCountsRealDevices()
    {
        Assert.Equal("No device connected yet.", CompanionPairingText.Status(0, 0));
        Assert.Equal("1 device connected.", CompanionPairingText.Status(1, 1));
        Assert.Equal("3 devices connected.", CompanionPairingText.Status(3, 3));
        // A phone AND the PC's browser: the phone is the news, the browser is a footnote.
        Assert.Equal("1 device connected (plus this PC's own browser).",
            CompanionPairingText.Status(clients: 2, offBox: 1));
        Assert.Equal("2 devices connected (plus this PC's own browser).",
            CompanionPairingText.Status(clients: 3, offBox: 2));
    }

    [Fact]
    public void TheOldSingleArgumentLineStillMeansWhatItSaid()
    {
        // Callers that cannot tell the origin apart get the old behaviour, not a line
        // claiming every browser is this PC's.
        Assert.Equal("1 device connected.", CompanionPairingText.Status(1));
        Assert.Equal("4 devices connected.", CompanionPairingText.Status(4));
    }

    // ---------- who counts as "us" ----------

    private static readonly IReadOnlySet<IPAddress> Machine = new HashSet<IPAddress>
    {
        IPAddress.Parse("10.0.0.84"),
        IPAddress.Parse("100.118.30.124"),
        IPAddress.Loopback,
    };

    [Theory]
    [InlineData("127.0.0.1", "127.0.0.1")]   // loopback
    [InlineData("10.0.0.84", "10.0.0.84")]   // the PC's browser on the PC's own LAN address
    public void OurOwnConnectionsAreRecognised(string remote, string local)
    {
        Assert.True(CompanionServer.IsSameMachine(
            IPAddress.Parse(remote), IPAddress.Parse(local), Machine));
    }

    [Fact]
    public void AnAddressWeHoldOnANOTHERAdapterIsStillUs()
    {
        // Real on the machine this was found on: Wi-Fi 10.0.0.84 and Tailscale
        // 100.118.30.124 are both this PC, and the source/destination test alone misses it.
        Assert.True(CompanionServer.IsSameMachine(
            IPAddress.Parse("100.118.30.124"), IPAddress.Parse("10.0.0.84"), Machine));
    }

    [Fact]
    public void ARealPhoneOnTheSameSubnetIsNotUs()
    {
        // The negative the whole feature rests on. Same network, one octet apart.
        Assert.False(CompanionServer.IsSameMachine(
            IPAddress.Parse("10.0.0.85"), IPAddress.Parse("10.0.0.84"), Machine));
    }

    [Fact]
    public void AnEmptyAddressSetDoesNotMakeEveryoneUs()
    {
        // MachineAddresses() catches its own exceptions, so an enumeration that failed
        // returns few addresses. Degrading toward "that was a phone" is the safe way for
        // this to be wrong: it retracts a diagnosis rather than inventing one.
        var none = (IReadOnlySet<IPAddress>)new HashSet<IPAddress>();
        Assert.False(CompanionServer.IsSameMachine(
            IPAddress.Parse("10.0.0.85"), IPAddress.Parse("10.0.0.84"), none));
        Assert.True(CompanionServer.IsSameMachine(
            IPAddress.Parse("127.0.0.1"), null, none));
    }

    [Fact]
    public void ThisMachinesRealAddressesAreActuallyEnumerated()
    {
        // Guards the wiring, not the arithmetic: an empty set here would silently turn
        // every local browser into a phone and the verdict back into the old lie.
        var addresses = CompanionServer.MachineAddresses();
        Assert.Contains(IPAddress.Loopback, addresses);
        Assert.True(addresses.Count >= 2);
    }
}

/// <summary>
/// The same claim against real sockets. One box cannot produce a connection from a second
/// machine, so what is provable here is the half that actually went wrong: a browser on
/// THIS PC, doing exactly what the Founder did, must move the same-machine counter and
/// leave the off-box one at zero — through the accept path, not through a helper.
/// </summary>
public sealed class CompanionSelfConnectIsNotADeviceTests : IDisposable
{
    private const string Token = "0123456789abcdef0123456789abcdef";
    private readonly CompanionServer _server = new(new CompanionServerOptions
    {
        Token = Token,
        Port = 0,
        Addresses = [IPAddress.Loopback],
        HeartbeatInterval = TimeSpan.FromMilliseconds(200),
    });

    public CompanionSelfConnectIsNotADeviceTests() => _server.Start();

    public void Dispose() => _server.Dispose();

    [Fact]
    public async Task FetchingThePageFromThisPcIsNotADeviceReachingIt()
    {
        Assert.Equal(0, _server.SameMachineConnects);
        Assert.Equal(0, _server.OffBoxConnects);

        using var http = new System.Net.Http.HttpClient();
        var page = await http.GetStringAsync($"http://127.0.0.1:{_server.Port}/",
            new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);
        Assert.Contains("<html", page, StringComparison.OrdinalIgnoreCase);

        Assert.True(_server.SameMachineConnects >= 1);
        Assert.Equal(0, _server.OffBoxConnects);
        // And therefore the verdict the window draws is the corrective one, not success.
        Assert.Equal(CompanionReach.OnlyThisPc, CompanionReachability.Verdict(
            running: true, _server.OffBoxConnects, _server.SameMachineConnects,
            CompanionReachability.Patience + TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task APairedBrowserOnThisPcIsConnectedButIsNotADevice()
    {
        using var ws = new System.Net.WebSockets.ClientWebSocket();
        var ct = new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token;
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{_server.Port}/ws?token={Token}"), ct);

        // A fully paired, token-correct, live WebSocket — and still not a phone.
        Assert.Equal(1, _server.ClientCount);
        Assert.Equal(0, _server.OffBoxClientCount);
        Assert.Equal(0, _server.OffBoxConnects);
        Assert.Contains("no device yet",
            CompanionPairingText.Status(_server.ClientCount, _server.OffBoxClientCount),
            StringComparison.OrdinalIgnoreCase);

        await ws.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "bye", ct);
    }

    [Fact]
    public async Task ARefusedConnectionStillCountsAsHavingArrived()
    {
        // Counted at accept, before auth. A phone with a stale token is a phone that got
        // here, and telling its owner to go fixing their firewall would be the same class
        // of wrong answer as the one this replaced.
        using var http = new System.Net.Http.HttpClient();
        using var res = await http.GetAsync($"http://127.0.0.1:{_server.Port}/ws?token=wrong",
            new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, res.StatusCode);
        Assert.True(_server.SameMachineConnects >= 1);
    }
}
