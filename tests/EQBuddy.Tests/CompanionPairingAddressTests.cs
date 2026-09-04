using EQBuddy.Companion;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// #264 (brhanson2-cyber, 2026-09-02): <i>"When setting up eqbuddy mobile the link it gives
/// me is the ip address of my ethernet, not my wifi. How do I force it to give me a link
/// using the wifi ip (i make sure my phone is on the same wifi)"</i>.
///
/// <para>Two halves, and only one of them is a ranking. <see cref="LanAddressRankTests"/>
/// owns the rule (Wi-Fi breaks the tie against ethernet, and no demotion is weakened by
/// it). This file owns the half a rule cannot do: the machine binds every LAN address it
/// has, so which one gets PRINTED is a choice, and the reporter asked for it by name.</para>
///
/// <para>These run against whatever NICs the machine actually has — one on CI, several on
/// a real desk — so every assertion is written to be true at either count. What they pin
/// is the wiring, not the hardware.</para>
/// </summary>
// CompanionHost persists on pair/enable/pin, so these write the shared profile's
// settings.json — see SettingsFileCollection.
[Collection(SettingsFileCollection.Name)]
public class CompanionPairingAddressTests
{
    private static AppSettings Enabled() => new() { CompanionEnabled = true, CompanionPort = 0 };

    [Fact]
    public void EveryAddressThePickerOffersIsOneSomethingIsActuallyListeningOn()
    {
        // A picker that lists an address nobody is listening on is worse than no picker:
        // it hands the player a way to make the QR unreachable and calls it a fix. So
        // this CONNECTS to each offered endpoint rather than comparing the list to
        // itself — an assertion that reads the same list twice cannot fail (trap 39).
        var settings = Enabled();
        using var host = new CompanionHost(settings, "test");
        Assert.True(host.Running);
        Assert.NotEmpty(host.PairingAddresses);

        var port = int.Parse(host.PairingUrl!.Split(':')[2].Split('/')[0]);
        foreach (var candidate in host.PairingAddresses)
        {
            using var probe = new System.Net.Sockets.TcpClient();
            Assert.True(probe.ConnectAsync(candidate.Address, port).Wait(TimeSpan.FromSeconds(5)),
                $"the picker offers {candidate.Address}:{port} and nothing answers there");
        }
    }

    [Fact]
    public void ThePickerIsOfferedInRankedOrder()
    {
        // The first row is the default, so the list has to agree with the rule that chose
        // it — a picker sorted any other way makes "EQBuddy picked the Wi-Fi one" a lie
        // about the row sitting at the top.
        var settings = Enabled();
        using var host = new CompanionHost(settings, "test");

        var scores = host.PairingAddresses.Select(c => c.Score).ToList();
        Assert.Equal([.. scores.Order()], scores);
    }

    [Fact]
    public void WithNoPinTheUrlPrintsTheRankedFirstAddress()
    {
        var settings = Enabled();
        using var host = new CompanionHost(settings, "test");

        Assert.Null(settings.CompanionPairingAddress);
        Assert.Null(host.PinnedPairingAddress);
        Assert.Equal(host.PairingAddresses[0].Address, host.PairingAddress);
        Assert.Contains($"http://{host.PairingAddress}:", host.PairingUrl);
    }

    [Fact]
    public void PinningAnAddressChangesWhatTheQrCarries()
    {
        var settings = Enabled();
        using var host = new CompanionHost(settings, "test");

        // The LAST offered address — on a one-NIC machine that is the same one, which
        // still proves the pin round-trips; on a real desk it proves it OVERRIDES.
        var wanted = host.PairingAddresses[^1].Address;
        host.SetPairingAddress(wanted);

        Assert.Equal(wanted, host.PinnedPairingAddress);
        Assert.Equal(wanted, settings.CompanionPairingAddress);       // and it was written
        Assert.Equal(wanted, host.PairingAddress);
        Assert.Contains($"http://{wanted}:", host.PairingUrl);
        // Nothing restarts: every LAN address is already listening, so a pin only changes
        // which one we PRINT — paired devices are not disconnected by choosing again.
        Assert.True(host.Running);
        Assert.Contains("#" + settings.CompanionToken, host.PairingUrl);
    }

    [Fact]
    public void APinThisMachineCannotHonourIsIgnoredRatherThanPrinted()
    {
        // TEST-NET-3, which no machine has. A pin made on the home Wi-Fi and read again
        // somewhere else is exactly this, and a QR pointing at an address that does not
        // exist is a scan that silently reaches nothing — the bug LanAddressRank exists
        // to prevent, arriving through the fix for it.
        var settings = new AppSettings
        {
            CompanionEnabled = true, CompanionPort = 0, CompanionPairingAddress = "203.0.113.9",
        };
        using var host = new CompanionHost(settings, "test");

        Assert.Equal("203.0.113.9", host.PinnedPairingAddress);       // the pin is kept
        Assert.Equal(host.PairingAddresses[0].Address, host.PairingAddress);
        Assert.DoesNotContain("203.0.113.9", host.PairingUrl);
    }

    [Fact]
    public void ClearingThePinHandsTheChoiceBackToTheRanking()
    {
        var settings = Enabled();
        using var host = new CompanionHost(settings, "test");

        host.SetPairingAddress(host.PairingAddresses[^1].Address);
        host.SetPairingAddress(null);

        Assert.Null(host.PinnedPairingAddress);
        Assert.Null(settings.CompanionPairingAddress);
        Assert.Equal(host.PairingAddresses[0].Address, host.PairingAddress);
    }

    [Fact]
    public void BlankIsAutomaticNotAnAddress()
    {
        // The picker's "Choose automatically" row passes null, but a hand-edited
        // settings.json can carry "" or "  " and must mean the same thing.
        var settings = Enabled();
        using var host = new CompanionHost(settings, "test");

        host.SetPairingAddress("   ");
        Assert.Null(host.PinnedPairingAddress);
        Assert.Equal(host.PairingAddresses[0].Address, host.PairingAddress);
    }

    [Fact]
    public void NothingIsPrintedOrOfferedWhileStopped()
    {
        var settings = new AppSettings();
        using var host = new CompanionHost(settings, "test");

        Assert.False(host.Running);
        Assert.Null(host.PairingUrl);
        Assert.Null(host.PairingAddress);
        Assert.Empty(host.PairingAddresses);
    }

    [Fact]
    public void ARowNamesWiFiAndNeverGuessesAtWired()
    {
        // Wireless is NAMED because it is the whole question the reporter asked. Nothing
        // is called "wired": the list also carries VPN and virtual adapters, and calling
        // one of those ethernet would be a guess presented as a fact.
        Assert.Equal("192.168.1.42 — Wi-Fi · Intel(R) Wi-Fi 6E AX211",
            CompanionPairingText.AddressChoice("192.168.1.42", "Intel(R) Wi-Fi 6E AX211", wireless: true));
        Assert.Equal("192.168.1.9 — Realtek Gaming 2.5GbE",
            CompanionPairingText.AddressChoice("192.168.1.9", "Realtek Gaming 2.5GbE", wireless: false));
        Assert.DoesNotContain("Wi-Fi",
            CompanionPairingText.AddressChoice("192.168.1.9", "Realtek Gaming 2.5GbE", wireless: false));
        // A caller that named its own addresses (tests) has no adapter to describe.
        Assert.Equal("127.0.0.1", CompanionPairingText.AddressChoice("127.0.0.1", "", wireless: false));
        Assert.Equal("127.0.0.1 — Wi-Fi", CompanionPairingText.AddressChoice("127.0.0.1", "", wireless: true));
    }

    [Fact]
    public void AnExplicitlyBoundServerStillOffersItsAddresses()
    {
        // The deterministic shape, without asking what NICs this machine has: bind
        // loopback on purpose and check the candidate survives the bind alongside the
        // IPAddress it came from.
        using var server = new CompanionServer(new CompanionServerOptions
        {
            Token = "0123456789abcdef0123456789abcdef",
            Port = 0,
            Addresses = [System.Net.IPAddress.Loopback],
        });
        server.Start();

        Assert.Equal(["127.0.0.1"], server.BoundCandidates.Select(c => c.Address));
        Assert.Equal(server.BoundAddresses.Count, server.BoundCandidates.Count);
    }
}
