using EQBuddy.Companion;
using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// EQBuddy Mobile launched 2026-08-14 and its preview gate is gone. That gate used to be
/// the belt beside the braces; <see cref="AppSettings.CompanionEnabled"/> is now the ONLY
/// thing standing between a dormant feature and a listening port, which makes pinning it
/// more important than it was, not less.
///
/// Constructing a host does not save settings — these write nothing to any profile.
/// </summary>
public class CompanionEnableTests
{
    [Fact]
    public void OffByDefaultMeansNoSocketAtAll()
    {
        // The shape of a fresh install. Nobody who hasn't asked for a second screen
        // should have a port open on their machine because they updated EQBuddy.
        var settings = new AppSettings();
        Assert.False(settings.CompanionEnabled);

        using var host = new CompanionHost(settings, "test");
        Assert.False(host.Running);
        Assert.Equal(0, host.ClientCount);
        Assert.Null(host.PairingUrl);
    }

    /// <summary>A TAKEN PORT MUST NOT BE A DEAD END.
    ///
    /// David, 2026-08-21: ticking Enable produced <i>"Couldn't listen on port 47998 — Only
    /// one usage of each socket address is normally permitted. (is another program using
    /// it? Change the port and try again.)"</i> and nothing else. Two things were wrong with
    /// that, and the second is the worse one:
    ///
    /// <list type="number">
    /// <item>Nothing owned the port in any table on his machine — not netstat in any state,
    /// not an HTTP.sys reservation, not the excluded-port ranges — and yet no process could
    /// bind it on any address, including 0.0.0.0. A kernel-level reservation from some
    /// driver or tunnel. Nothing EQBuddy can detect, argue with, or fix.</item>
    /// <item><b>"Change the port and try again" names an action the app does not offer.</b>
    /// <c>CompanionPort</c> appears in no Options screen and no dialog. That is the same
    /// defect as a surface naming an in-game command and handing over no way to run it,
    /// which this project has now paid for three times.</item>
    /// </list>
    ///
    /// So a taken port falls back to one the OS picks. The pairing URL carries the port
    /// anyway — a phone scans whatever it is given — so the only thing the fixed number was
    /// ever buying was a stable QR between sessions, which is worth less than the feature
    /// working at all.</summary>
    [Fact]
    public void APortTheMachineWillNotGiveUsFallsBackToOneItWill()
    {
        // Hold a port so the host cannot have it — the same situation as David's, arrived
        // at honestly rather than by mocking the failure.
        // Squat the port on EXACTLY the addresses the host will try. A wildcard holder is
        // not enough on Windows — .NET leaves ExclusiveAddressUse off, so a specific-address
        // bind sails past a 0.0.0.0 one — and a loopback holder is not enough either,
        // because the host binds LAN addresses. The only faithful way to say "this port is
        // not available to us" is to hold it where we would want it.
        var wanted = CompanionServer.LanAddresses().ToList();
        if (wanted.Count == 0) wanted = [System.Net.IPAddress.Loopback];

        var probe = new System.Net.Sockets.TcpListener(wanted[0], 0);
        probe.Start();
        var taken = ((System.Net.IPEndPoint)probe.LocalEndpoint).Port;
        var squatters = new List<System.Net.Sockets.TcpListener> { probe };
        foreach (var addr in wanted.Skip(1))
        {
            var l = new System.Net.Sockets.TcpListener(addr, taken);
            try { l.Start(); squatters.Add(l); } catch { /* one is enough to block us */ }
        }
        try
        {
            var settings = new AppSettings { CompanionEnabled = true, CompanionPort = taken };
            using var host = new CompanionHost(settings, "test");

            Assert.True(host.Running);
            Assert.NotNull(host.PairingUrl);
            // It says what it did rather than failing, and it is not an ERROR: nothing is
            // wrong that the player has to act on.
            Assert.Null(host.LastError);
            Assert.NotNull(host.Notice);
            Assert.Contains(taken.ToString(), host.Notice);
            // And the port it landed on is remembered, so the next launch does not have to
            // rediscover it.
            Assert.NotEqual(taken, settings.CompanionPort);
        }
        finally { foreach (var l in squatters) l.Stop(); }
    }

    [Fact]
    public void TurningItOnIsWhatOpensThePort()
    {
        var settings = new AppSettings { CompanionEnabled = true, CompanionPort = 0 };
        using var host = new CompanionHost(settings, "test");

        Assert.True(host.Running);
        Assert.Null(host.LastError);
        // A token is minted on first listen, and the pairing URL carries it in the
        // FRAGMENT so it never appears in an HTTP request line.
        Assert.NotNull(settings.CompanionToken);
        Assert.Contains("#" + settings.CompanionToken, host.PairingUrl);
    }

    [Fact]
    public void TickCostsNothingWhileNobodyIsPaired()
    {
        // The perf contract the whole feature rests on: with it off, a tick is a couple
        // of field reads and builds no projection, so a player who never uses this pays
        // nothing for it once a second forever.
        var settings = new AppSettings();
        using var host = new CompanionHost(settings, "test");
        var timers = new SpawnTimers(SpawnCatalog.LoadEmbedded(),
            new SpawnOverrides(), Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json"));

        var ex = Record.Exception(() => host.Tick(null, timers, "Dranak", DateTime.Now));
        Assert.Null(ex);
        Assert.False(host.Running);
    }
}
