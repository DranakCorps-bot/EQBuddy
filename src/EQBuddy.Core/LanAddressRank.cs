namespace EQBuddy.Core;

/// <summary>
/// Which of this machine's IPv4 addresses to print on the pairing QR (David,
/// 2026-08-15: "Scanning the QR code didn't bring up the mobile interface").
///
/// <para>The original rule was "private addresses first", which cannot separate the
/// address a tablet can reach from one it cannot: a Hyper-V / VirtualBox / WSL host
/// adapter at 192.168.200.1 is exactly as RFC1918-private as the real LAN at 10.0.0.84,
/// and Windows often enumerates the virtual one first. The QR then encodes an address
/// that exists only inside this PC, and the scan appears to do nothing at all.</para>
///
/// <para>The signal that actually distinguishes them is a DEFAULT GATEWAY: the
/// interface that routes to the rest of the network is the interface the tablet shares.
/// Virtual host-only adapters have no gateway; nor do most VPN tunnels worth skipping
/// here. Ranking is a pure function of (address, has-gateway, adapter description,
/// is-wireless) so it can be tested without a network — the machine that showed the bug
/// has three candidates and reproducing it any other way means owning the same
/// hardware.</para>
///
/// <para><b>Wi-Fi breaks the tie between two REAL networks (#264, brhanson2-cyber,
/// 2026-09-02: "the link it gives me is the ip address of my ethernet, not my wifi... i
/// make sure my phone is on the same wifi").</b> A gatewayed ethernet and a gatewayed
/// Wi-Fi score identically under the rules above, so NIC enumeration order decided the
/// QR — the same failure mode the gateway rule was written to end, one level up. The
/// device doing the scanning is on Wi-Fi by definition, so when this PC is on both, the
/// wireless address is the one it shares for certain; the wired one is only reachable if
/// the two happen to be the same network, which is common and is not guaranteed.</para>
///
/// <para>The preference is deliberately SMALLER than every penalty above (5 against 10,
/// 25, 50, 100), so it can only ever separate two adapters that are otherwise equal. A
/// wireless adapter with no gateway still loses to a wired one with a gateway, and a
/// Hyper-V/WSL/Tailscale adapter that happens to look wireless still loses to the real
/// LAN — those demotions are what this class exists for and nothing here weakens
/// them.</para>
///
/// <para>It is a TIEBREAK, not an answer: a PC whose Wi-Fi is on a phone hotspot while
/// the ethernet is the house LAN is ranked wrong by this rule and cannot be ranked right
/// by any rule, because nothing on this machine knows which network the phone is on.
/// That is why the pairing window also offers the list (<see cref="LanAddressCandidate"/>)
/// — the reporter asked "how do I force it", and a ranking can never be that answer.</para>
/// </summary>
public static class LanAddressRank
{
    /// <summary>Adapter-description fragments that mean "this is not the network your
    /// tablet is on". Matched case-insensitively against the NIC description. Tailscale
    /// and ZeroTier are reachable in their own way, but a QR scanned from a tablet on
    /// the house Wi-Fi will not reach them, so they lose to the real LAN.</summary>
    private static readonly string[] VirtualAdapterHints =
    [
        "hyper-v", "virtualbox", "vmware", "docker", "wsl", "vethernet",
        "tailscale", "zerotier", "loopback", "tap-", "tun", "openvpn",
        "wireguard", "bluetooth", "npcap", "pseudo",
    ];

    /// <summary>Adapter-description fragments that mean "this is the wireless NIC".
    /// A fallback, not the primary signal: <c>NetworkInterfaceType.Wireless80211</c> is
    /// what the caller asks the OS, and this catches the drivers and platforms that do
    /// not report it. Matched case-insensitively, and it can only ever be a TIEBREAK —
    /// a virtual switch bridged onto Wi-Fi ("vEthernet (WiFi)") matches both this list
    /// and <see cref="VirtualAdapterHints"/>, and the 50-point demotion wins.</summary>
    private static readonly string[] WirelessAdapterHints =
    [
        "wi-fi", "wifi", "wireless", "wlan", "802.11",
    ];

    /// <summary>How much a wireless adapter beats an otherwise identical wired one
    /// (#264). Smaller than every penalty in <see cref="Score"/> on purpose — see the
    /// class note.</summary>
    public const int WirelessPreference = 5;

    /// <summary>Lower sorts earlier. The components are additive so a real LAN address
    /// on a gatewayed physical adapter always beats every combination of penalties.</summary>
    public static int Score(string address, bool hasGateway, string adapterDescription) =>
        Score(address, hasGateway, adapterDescription, isWireless: false);

    /// <inheritdoc cref="Score(string, bool, string)"/>
    /// <param name="isWireless">What the OS says about the adapter
    /// (<c>NetworkInterfaceType.Wireless80211</c>). The description is consulted as well,
    /// so a caller that cannot answer may pass false.</param>
    public static int Score(string address, bool hasGateway, string adapterDescription, bool isWireless)
    {
        var score = 0;
        // The decisive one: no route off this machine, no tablet.
        if (!hasGateway) score += 100;
        var desc = adapterDescription ?? "";
        foreach (var hint in VirtualAdapterHints)
            if (desc.Contains(hint, StringComparison.OrdinalIgnoreCase)) { score += 50; break; }
        // 100.64/10 is carrier-grade NAT, which in practice here means a mesh VPN.
        if (IsCarrierGradeNat(address)) score += 25;
        if (!IsPrivate(address)) score += 10;
        // The tiebreak, and only ever a tiebreak: the device scanning the QR is on
        // Wi-Fi, so of two otherwise-equal real networks that is the shared one (#264).
        if (isWireless || LooksWireless(desc)) score -= WirelessPreference;
        return score;
    }

    /// <summary>Does this adapter description read as a wireless NIC? Exposed so the
    /// pairing window can LABEL a row the same way the ranking scored it — two answers
    /// to one question is how a picker ends up disagreeing with its own order.</summary>
    public static bool LooksWireless(string? adapterDescription)
    {
        var desc = adapterDescription ?? "";
        foreach (var hint in WirelessAdapterHints)
            if (desc.Contains(hint, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>RFC1918. Kept as the tiebreak it always was, no longer as the only rule.</summary>
    public static bool IsPrivate(string address)
    {
        var b = Octets(address);
        if (b is null) return false;
        return b[0] == 192 && b[1] == 168
            || b[0] == 10
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31);
    }

    /// <summary>100.64.0.0/10 — Tailscale and friends live here.</summary>
    public static bool IsCarrierGradeNat(string address)
    {
        var b = Octets(address);
        return b is not null && b[0] == 100 && b[1] >= 64 && b[1] <= 127;
    }

    /// <summary>Rank a set of candidates best-first. Stable, so equally-scored addresses
    /// keep the order the OS enumerated them in.</summary>
    public static IReadOnlyList<LanAddressCandidate> Rank(IEnumerable<LanAddressCandidate> candidates) =>
        [.. candidates.OrderBy(c => c.Score)];

    /// <summary>Which address the pairing QR prints: the player's pin when this machine
    /// still HAS it, the best-ranked one otherwise, null when there is nothing to print.
    ///
    /// <para>A pure function on purpose. The fallback is the half that matters and it is
    /// the half a socket test cannot reach: a pin is made on the home Wi-Fi and read
    /// again in a hotel, and a QR pointing at an address that no longer exists is a scan
    /// that silently reaches nothing — the exact bug this class was written for.</para>
    /// </summary>
    /// <param name="bound">Addresses actually listening, best first.</param>
    /// <param name="pinned">What the player chose, or null/empty for automatic.</param>
    public static string? Resolve(IReadOnlyList<string> bound, string? pinned)
    {
        if (bound is null || bound.Count == 0) return null;
        if (!string.IsNullOrWhiteSpace(pinned))
            foreach (var address in bound)
                if (string.Equals(address, pinned.Trim(), StringComparison.OrdinalIgnoreCase))
                    return address;
        return bound[0];
    }

    private static int[]? Octets(string address)
    {
        var parts = (address ?? "").Split('.');
        if (parts.Length != 4) return null;
        var octets = new int[4];
        for (var i = 0; i < 4; i++)
            if (!int.TryParse(parts[i], out octets[i])) return null;
        return octets;
    }
}

/// <summary>
/// One address this PC can be paired on, carrying the facts the ranking used — so the
/// pairing window can OFFER the list rather than only printing the winner.
///
/// <para>#264 is why this exists at all. The ranking can separate a real LAN from a
/// virtual adapter, and now a Wi-Fi from an ethernet, but it cannot know which network
/// the player's phone is on — and the reporter's question was literally "how do I force
/// it to give me a link using the wifi ip". A rule cannot answer that; a list can.</para>
/// </summary>
/// <param name="Address">Dotted-quad IPv4, exactly as it will appear in the URL.</param>
/// <param name="AdapterDescription">The NIC's own description, for naming the row.</param>
/// <param name="HasGateway">Does this interface route off the machine.</param>
/// <param name="IsWireless">What the OS said (the description is checked as well).</param>
public sealed record LanAddressCandidate(
    string Address, string AdapterDescription, bool HasGateway, bool IsWireless)
{
    /// <summary>Lower sorts earlier — one call into <see cref="LanAddressRank.Score"/>,
    /// never a second copy of the rules.</summary>
    public int Score => LanAddressRank.Score(Address, HasGateway, AdapterDescription, IsWireless);

    /// <summary>True when either the OS or the adapter's own name says wireless. The
    /// label and the score ask this same question, so a row cannot read "Wi-Fi" while
    /// having been ranked as anything else.</summary>
    public bool Wireless => IsWireless || LanAddressRank.LooksWireless(AdapterDescription);
}
