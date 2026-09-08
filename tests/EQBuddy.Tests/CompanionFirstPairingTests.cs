using System.Text.RegularExpressions;
using EQBuddy.Companion;
using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// **A device that has never subscribed is served EVERY surface the PC offers — so the
/// page has to say what it wants BEFORE it has been told what exists.**
///
/// The owner scanned the pairing QR on a phone that had never paired, and the page hung
/// on "connecting…" and never came back (2026-09-07). Nothing was wrong with the QR, the
/// address, the firewall, the handshake or the protocol: the page connected, the server
/// answered 101, and then served it all eleven surfaces on every push for the life of the
/// connection, because <see cref="CompanionSnapshot.ForSubscription"/> treats a null
/// subscription as "everything" and the page never sent one.
///
/// It never sent one because the only two <c>sendSubscribe()</c> calls were "a RETURNING
/// device, on open" (<c>if (choice) sendSubscribe()</c> — and <c>choice</c> is null until
/// the first snapshot builds it) and "the player touched the ⚙". A freshly paired phone is
/// neither, and the ⚙ is behind the page that will not respond.
///
/// Measured on the owner's own profile at the time of the report: 724 KB on connect, then
/// **186 KB per push** against **2.2 KB** for the two panels a phone actually opens with —
/// 174 KB of it the quest section, for a surface the device does not draw. The pump pushes
/// as fast as the session moves, so an active fight is that payload many times a second,
/// each one a JSON.parse on the phone's only thread.
///
/// <para>THE COMMENT ON <c>CompanionQuestsTests.AddingTheQuestSurfaceLaterShipsTheCatalogAgain</c>
/// already wrote down the assumption this violates — *"the connect push (unsubscribed =
/// everything) spends the catalog before the page narrows"*. The narrowing was real for a
/// returning device and had never existed for a new one. Trap 20's shape: the thing to look
/// for is the call that is NOT there.</para>
///
/// <para>There is no JS runner here, so the page half is asserted against the shipped file,
/// the way <c>CompanionRepaintGateTests</c> and <c>CompanionPageUpdateTests</c> do — with a
/// committed NEGATIVE each, because a regex that cannot fail reads as coverage (trap 39).
/// The behavioural proof is the harness, and it is repeatable:
/// <c>pwsh -NoProfile -File scripts/mobile-harness.ps1</c> then load
/// <c>dist/mobile-harness/harness.html</c> in a browser with a CLEAN profile and read
/// <c>window.__SENT</c> — empty on the pre-fix page, one subscribe on this one.</para>
/// </summary>
public class CompanionFirstPairingTests
{
    private static string PageSource()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "src", "EQBuddy.Companion", "Web", "index.html"));
        Assert.True(File.Exists(path), $"the shipped page moved: {path}");
        return File.ReadAllText(path);
    }

    /// <summary>The body of <c>ws.onopen = () =&gt; { … }</c>, which is the only moment a
    /// device that has never paired can speak before the PC has spent a push on it.</summary>
    private static string OnOpenHandler()
    {
        var m = Regex.Match(PageSource(),
            @"ws\.onopen = \(\) => \{(?<body>.*?)\r?\n    \};", RegexOptions.Singleline);
        Assert.True(m.Success, "index.html no longer has a ws.onopen handler to read.");
        return m.Groups["body"].Value;
    }

    [Fact]
    public void ThePageSubscribesOnOpenWhetherOrNotThisDeviceHasPairedBefore()
    {
        var body = OnOpenHandler();
        Assert.Contains("sendSubscribe(", body);

        // The negative, and it is the whole bug: the call must not be behind the
        // existence of a saved choice, because a first pairing has none.
        Assert.DoesNotContain("if (choice) sendSubscribe", body);
        Assert.DoesNotContain("if (choice) sendSubscribe", PageSource());
    }

    /// <summary>The open handler can only guess (there is no <c>offered</c> yet), so the
    /// page states its picks again the moment <c>ensureChoice()</c> has made them. Without
    /// this, a PC whose gate differs from the device's first-run guess would leave the
    /// subscription permanently wrong in one direction or the other.</summary>
    [Fact]
    public void ThePageRestatesItsPicksOnceTheFirstSnapshotHasBuiltThem()
    {
        var page = PageSource();
        var m = Regex.Match(page, @"ensureChoice\(\);(?<after>.*?)render\(\);", RegexOptions.Singleline);
        Assert.True(m.Success, "the snapshot handler no longer calls ensureChoice() before render().");
        Assert.Contains("sendSubscribe()", m.Groups["after"].Value);
    }

    /// <summary>The subscription belongs to the SOCKET, not to the device: the server
    /// throws <c>WsClient</c> away on every disconnect, so a reconnect that skipped the
    /// re-ask would be an unsubscribed device again — the same bug, arriving through the
    /// backoff loop instead of through the QR.</summary>
    [Fact]
    public void ReconnectingForgetsWhatThisPageHadAlreadyAskedFor()
    {
        var m = Regex.Match(PageSource(),
            @"function connect\(\) \{(?<body>.*?)ws = new WebSocket", RegexOptions.Singleline);
        Assert.True(m.Success, "index.html no longer opens its socket in connect().");
        Assert.Contains("subscribedTo = null", m.Groups["body"].Value);
    }

    // ---------------- the wire fact the page fix leans on ----------------

    /// <summary>Why the page has to speak at all, asserted against the real projection
    /// rather than against the page's source: a null subscription is served every section,
    /// and the narrowed one is a fraction of it. If this ever stops being true the page's
    /// opening subscribe can be revisited — and until then it cannot.</summary>
    [Fact]
    public void AnUnsubscribedDeviceIsServedEverySurfaceAndAPickedOneIsNot()
    {
        var snap = Everything();

        var silent = snap.ForClient(null, new CompanionClientState());
        var picked = snap.ForClient(
            [CompanionSurfaces.Spawns, CompanionSurfaces.Session], new CompanionClientState());

        // The sections a phone opening on spawns+session never draws, and was being sent
        // anyway, on every push. Quests is the expensive one — 174 KB of the owner's 186.
        Assert.NotNull(silent.Quests);
        Assert.NotNull(silent.Combat);
        Assert.NotNull(silent.Loot);
        Assert.Null(picked.Quests);
        Assert.Null(picked.Combat);
        Assert.Null(picked.Loot);

        // Both still carry the envelope, which is what lets the page build its ⚙ picker
        // from `offered` while subscribed to two names — the reason an opening ask made
        // before the first snapshot is safe.
        Assert.Equal(snap.Offered, picked.Offered);
        Assert.NotNull(picked.Session);
        Assert.NotNull(picked.Spawns);

        Assert.True(picked.ToJson().Length * 4 < silent.ToJson().Length,
            $"narrowing bought almost nothing ({picked.ToJson().Length} vs " +
            $"{silent.ToJson().Length} bytes) — this test is the reason the page narrows.");
    }

    /// <summary>An opening ask naming a surface the PC has gated off is answered, not
    /// ignored: the page turns <c>notOffered</c> into "not shared by the PC" instead of a
    /// panel that never fills. It is what makes it safe to ask before `offered` is known.</summary>
    [Fact]
    public void AnOpeningAskForAGatedSurfaceComesBackNamed()
    {
        var snap = Everything() with { Offered = [CompanionSurfaces.Session] };
        var narrowed = snap.ForClient(
            [CompanionSurfaces.Spawns, CompanionSurfaces.Session], new CompanionClientState());

        Assert.NotNull(narrowed.NotOffered);
        Assert.Contains(CompanionSurfaces.Spawns, narrowed.NotOffered);
        Assert.DoesNotContain(CompanionSurfaces.Session, narrowed.NotOffered);
    }

    /// <summary>A snapshot with every surface filled, which is what an unsubscribed device
    /// is served. The catalog is three quests rather than the shipped twelve hundred, so
    /// the ratio below is a FLOOR on what narrowing buys, never a flattering measurement.</summary>
    private static CompanionSnapshot Everything()
    {
        var catalog = new QuestCatalog
        {
            Quests =
            [
                new QuestEntry
                {
                    Name = "The Falchion", Url = "https://eqlwiki.com/The_Falchion",
                    StartZone = "Crushbone", QuestGiver = "Ambassador DVinn", Classes = "Paladin",
                    Items = [new QuestItemNeed { Name = "Blue Orc Head", Qty = 1 }],
                    Rewards = ["The Falchion"],
                },
                new QuestEntry
                {
                    Name = "Bone Chip Bounty", Url = "https://eqlwiki.com/Bone_Chips",
                    StartZone = "Kaladim", QuestGiver = "Gnilbin",
                    Items = [new QuestItemNeed { Name = "Bone Chips", Qty = 4 }],
                    Rewards = ["A few coppers"],
                },
            ],
        };
        return CompanionProjection.Build(new CompanionInputs
        {
            Character = "Dranak",
            AppVersion = "2.0.0",
            Offered = CompanionSurfaces.All,
            Stats = new StatsSnapshot { CurrentZone = "Lower Guk", YourKillCount = 3 },
            Settings = new AppSettings(),
            Timers = [new SpawnTimerState("legends", "Lower Guk", "Frenzied Ghoul", DateTime.Now, 600)],
            Quests = new CompanionQuestRequest { Catalog = catalog },
            QuestIndex = CompanionQuestIndex.Build(catalog),
        }, DateTime.Now);
    }
}
