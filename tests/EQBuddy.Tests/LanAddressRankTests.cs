using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

// David's machine, 2026-08-15: "the Mobile link doesn't work. Scanning the QR code
// didn't bring up the mobile interface". EQBuddy was enabled, listening, and bound to
// three addresses — 10.0.0.84 (the real LAN), 100.118.30.124 (Tailscale) and
// 192.168.200.1 (a virtual host adapter). The old rule ordered by RFC1918 privateness
// alone, which scores the virtual adapter identically to the real LAN, so NIC
// enumeration order picked the QR's address. A tablet on the house Wi-Fi can reach
// exactly one of those three.
public class LanAddressRankTests
{
    // The real machine, as reported by Get-NetTCPConnection.
    private const string Lan = "10.0.0.84";
    private const string Tailscale = "100.118.30.124";
    private const string VirtualHost = "192.168.200.1";

    private static List<string> Ordered(params (string Ip, bool Gw, string Desc)[] candidates) =>
        [.. candidates
            .OrderBy(c => LanAddressRank.Score(c.Ip, c.Gw, c.Desc))
            .Select(c => c.Ip)];

    [Fact]
    public void TheRealLanWinsOverAVirtualAdapterAndAMeshVpn()
    {
        // Deliberately listed with the real LAN LAST, which is the ordering that broke
        // it: if the rule works only when Windows happens to enumerate favourably, it
        // does not work.
        var order = Ordered(
            (VirtualHost, false, "Hyper-V Virtual Ethernet Adapter"),
            (Tailscale, false, "Tailscale Tunnel"),
            (Lan, true, "Intel(R) Ethernet Controller I225-V"));
        Assert.Equal(Lan, order[0]);
    }

    [Fact]
    public void AGatewayBeatsNoGatewayEvenWhenBothLookPrivate()
    {
        // The decisive signal, isolated: same shape of address, only the route differs.
        Assert.True(LanAddressRank.Score("10.0.0.84", hasGateway: true, "Ethernet")
            < LanAddressRank.Score("192.168.200.1", hasGateway: false, "Ethernet"));
    }

    [Fact]
    public void AVirtualAdapterLosesEvenIfItSomehowReportsAGateway()
    {
        // Docker and WSL adapters have been seen advertising one.
        Assert.True(LanAddressRank.Score(Lan, true, "Realtek Gaming GbE")
            < LanAddressRank.Score("172.17.0.1", true, "Docker Desktop vEthernet"));
    }

    [Fact]
    public void TailscaleIsRecognisedByItsAddressRangeNotOnlyItsName()
    {
        // A mesh VPN under an unfamiliar adapter name still must not win the QR.
        Assert.True(LanAddressRank.IsCarrierGradeNat(Tailscale));
        Assert.True(LanAddressRank.Score(Lan, true, "Ethernet")
            < LanAddressRank.Score(Tailscale, true, "Unknown Adapter"));
    }

    [Fact]
    public void APublicAddressLosesToAPrivateOne()
    {
        Assert.False(LanAddressRank.IsPrivate("203.0.113.9"));
        Assert.True(LanAddressRank.Score(Lan, true, "Ethernet")
            < LanAddressRank.Score("203.0.113.9", true, "Ethernet"));
    }

    [Theory]
    [InlineData("192.168.1.10")]
    [InlineData("10.255.255.254")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    public void Rfc1918RangesAreRecognised(string ip) => Assert.True(LanAddressRank.IsPrivate(ip));

    [Theory]
    [InlineData("172.15.0.1")]   // just below the 172.16–31 block
    [InlineData("172.32.0.1")]   // just above it
    [InlineData("100.63.0.1")]   // just below CGNAT
    [InlineData("not.an.ip.at.all")]
    public void NearMissesAreNotTreatedAsPrivate(string ip) => Assert.False(LanAddressRank.IsPrivate(ip));

    [Fact]
    public void GarbageNeverThrows()
    {
        // LanAddresses() runs during startup; a malformed address must not take the
        // pairing window down with it.
        LanAddressRank.Score("", false, "");
        LanAddressRank.Score("1.2.3", true, null!);
        LanAddressRank.Score("1.2.3", true, null!, isWireless: true);
        Assert.False(LanAddressRank.IsCarrierGradeNat("...."));
        Assert.False(LanAddressRank.LooksWireless(null));
    }

    // ================= #264: Wi-Fi vs ethernet =================
    //
    // brhanson2-cyber, 2026-09-02: "When setting up eqbuddy mobile the link it gives me is
    // the ip address of my ethernet, not my wifi. How do I force it to give me a link
    // using the wifi ip (i make sure my phone is on the same wifi)". Both his adapters
    // have a gateway and neither is virtual, so every rule above scored them IDENTICALLY
    // and Windows' enumeration order picked the QR — which is the same failure the gateway
    // rule was written to end, one level up.

    private const string Wired = "192.168.1.9";
    private const string WiFi = "192.168.1.42";
    private const string WiFiNic = "Intel(R) Wi-Fi 6E AX211 160MHz";
    private const string WiredNic = "Realtek Gaming 2.5GbE Family Controller";

    [Fact]
    public void WiFiBreaksTheTieAgainstEthernetWhenBOTHAreRealNetworks()
    {
        // The device doing the scanning is on Wi-Fi by definition, so of two otherwise
        // equal networks that is the one it certainly shares.
        Assert.True(LanAddressRank.Score(WiFi, hasGateway: true, WiFiNic, isWireless: true)
            < LanAddressRank.Score(Wired, hasGateway: true, WiredNic, isWireless: false));
    }

    [Fact]
    public void TheWiFiTiebreakNeverOverTURNSAGateway()
    {
        // A wireless adapter that is up but on no network routes nowhere. The preference
        // is 5 against the gateway rule's 100 precisely so this can never invert.
        Assert.True(LanAddressRank.Score(Wired, hasGateway: true, WiredNic)
            < LanAddressRank.Score(WiFi, hasGateway: false, WiFiNic, isWireless: true));
    }

    [Fact]
    public void AHyperVSwitchBRIDGEDOntoWiFiStillLosesToTheRealLan()
    {
        // Windows names a virtual switch after the NIC it is bridged onto — "vEthernet
        // (Wi-Fi)" matches BOTH hint lists, and it must still lose. This is the demotion
        // #264 was not allowed to weaken, asserted at the exact point it could have.
        Assert.True(LanAddressRank.Score(Wired, true, WiredNic)
            < LanAddressRank.Score("192.168.200.1", true, "Hyper-V Virtual Ethernet Adapter (Wi-Fi)", true));
        // WSL's, same shape, and a Tailscale interface that calls itself wireless.
        Assert.True(LanAddressRank.Score(Wired, true, WiredNic)
            < LanAddressRank.Score("172.29.0.1", true, "vEthernet (WSL (Hyper-V firewall))", true));
        Assert.True(LanAddressRank.Score(Wired, true, WiredNic)
            < LanAddressRank.Score(Tailscale, true, "Wireless Mesh Adapter", true));
    }

    [Fact]
    public void TheReportersMachineNowRanksWiFiFirstAndTheRestUnchanged()
    {
        // Listed with Wi-Fi LAST — if it only works when the OS enumerates favourably it
        // does not work — and with the 2026-08-15 machine's other two still present, so
        // the new tiebreak is shown not to have disturbed the old order.
        var order = Ordered(
            (VirtualHost, false, "Hyper-V Virtual Ethernet Adapter"),
            (Tailscale, false, "Tailscale Tunnel"),
            (Wired, true, WiredNic),
            (WiFi, true, WiFiNic));
        // Wi-Fi, then ethernet, then the two the 2026-08-15 fix demoted — in the order it
        // put them (the mesh VPN carries the CGNAT penalty on top of the virtual one).
        Assert.Equal([WiFi, Wired, VirtualHost, Tailscale], order);
    }

    [Fact]
    public void TheThreeArgumentScoreIsStillTheWirelessFreeAnswer()
    {
        // The old overload is what every existing caller and test uses; it must mean
        // exactly "not wireless" rather than quietly acquiring a new opinion.
        Assert.Equal(LanAddressRank.Score(Wired, true, WiredNic, isWireless: false),
                     LanAddressRank.Score(Wired, true, WiredNic));
    }

    [Theory]
    [InlineData("Intel(R) Wi-Fi 6E AX211 160MHz")]
    [InlineData("Killer(R) Wireless-AC 1550i")]
    [InlineData("TP-Link WLAN 802.11ac Adapter")]
    [InlineData("Broadcom 802.11n Network Adapter")]
    public void TheAdapterNameIsTheFallbackWhenTheOsWillNotSayWireless(string description) =>
        Assert.True(LanAddressRank.LooksWireless(description));

    [Theory]
    [InlineData("Realtek Gaming 2.5GbE Family Controller")]
    [InlineData("Intel(R) Ethernet Controller I225-V")]
    [InlineData("")]
    public void AWiredAdapterIsNotMistakenForAWirelessOne(string description) =>
        Assert.False(LanAddressRank.LooksWireless(description));

    // ================= #264: the picker's half =================
    //
    // A ranking cannot know which network the phone is on — a PC whose Wi-Fi is a phone
    // hotspot while the ethernet is the house LAN is ranked wrong by the rule above and
    // cannot be ranked right by any rule. The reporter asked "how do I force it"; Resolve
    // is the answer, and its FALLBACK is the part that has to be exercised here.

    [Fact]
    public void WithNoPinTheRankedFirstAddressIsWhatThePairingUrlPrints() =>
        Assert.Equal(WiFi, LanAddressRank.Resolve([WiFi, Wired], null));

    [Fact]
    public void APinnedAddressWinsOverTheRanking() =>
        Assert.Equal(Wired, LanAddressRank.Resolve([WiFi, Wired], Wired));

    [Fact]
    public void APinForAnAddressThisMachineNoLongerHasFallsBackToTheRanking()
    {
        // The laptop-in-a-hotel case. A QR pointing at an address that does not exist is
        // a scan that silently reaches nothing, which is worse than ignoring the pin.
        Assert.Equal(WiFi, LanAddressRank.Resolve([WiFi, Wired], "10.0.0.84"));
        Assert.Equal(WiFi, LanAddressRank.Resolve([WiFi, Wired], "   "));
        Assert.Null(LanAddressRank.Resolve([], Wired));
    }

    [Fact]
    public void ACandidateScoresThroughTheSameRulesItIsLabelledBy()
    {
        // One question, one answer: a row that reads "Wi-Fi" must have been ranked as
        // wireless, or the picker disagrees with its own order.
        var byName = new LanAddressCandidate(WiFi, WiFiNic, HasGateway: true, IsWireless: false);
        Assert.True(byName.Wireless);
        Assert.Equal(LanAddressRank.Score(WiFi, true, WiFiNic, isWireless: true), byName.Score);

        var wired = new LanAddressCandidate(Wired, WiredNic, HasGateway: true, IsWireless: false);
        Assert.False(wired.Wireless);
        Assert.True(LanAddressRank.Rank([wired, byName])[0].Address == WiFi);
    }
}
