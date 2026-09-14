using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// **The Helper's per-tick bundle, for EQBuddy Mobile** (DRA-71 D9).
///
/// <para>It holds the phone's own <see cref="HelperSources"/> memo and joins it to the
/// widget's per-tick stores. Both halves of that sentence are the point:</para>
///
/// <para>The MEMO is per host (trap 45). <see cref="HelperSources"/> is a cache with a
/// five-second clock in it, and a cache two owners can invalidate is state rather than a
/// producer — the same split <see cref="LevelHistoryMemo"/> already keeps between the
/// Experience card and the phone.</para>
///
/// <para>The BUNDLE is shared. <c>Gather</c> is the one place the Helper's inputs are
/// assembled, so the object this hands to the projection is the object the shell room ranks
/// with, and "the phone showed something else" is a question about the inputs rather than
/// about which engine somebody called (trap 33). A second copy of the assembly here — seven
/// reads, three folds, four stores — is the #210 shape exactly, and it would drift the first
/// time one surface learned a store.</para>
///
/// <para>It is a class rather than a lambda in the widget's <c>CompanionSources</c>
/// initializer for the reason the ratchet exists: MainWindow is the hotspot, and a surface
/// that can live outside it does (the <c>CompanionMapSource</c> / <c>CompanionQuestSource</c>
/// idiom, one lane along).</para>
/// </summary>
internal sealed class PhoneHelperSource(MainWindow main)
{
    private readonly HelperSources _sources = new(new HelperSources.Reads(
        StoredMobRows: main.StoredMobRows,
        StoredSessions: main.StoredSessions,
        StoredThroughput: main.StoredThroughput,
        StoredSales: main.StoredSales,
        LatestInventory: () => main.LatestInventory(),
        StatsFor: main.WikiItems.StatsFor,
        ActiveSessionRowId: () => main.ActiveSessionRowId));

    /// <summary>One pass. Called only while the screen is offered AND a device is paired —
    /// <c>CompanionHost.Tick</c>'s lazy rule, which matters here more than for any other
    /// source: the reads behind it are a session query and three probes.</summary>
    public Companion.CompanionHelperRequest Build()
    {
        var snap = main.CurrentSnapshot();
        // The widget's pair is (Character, Server) and every UI.Shared reader takes
        // (Server, Character) — named access rather than a positional destructure, which is
        // the mistake ShellRoomIdentity exists to have made once and never again.
        _sources.Read(snap, main.Identity.Character, main.Identity.Server);

        var bundle = _sources.Gather(
            main.Settings, main.QuestCharacterKey, main.Unlocks, main.ResolvedLevel,
            main.QuestCatalog, ItemCatalog.Default,
            main.QuestLedger?.ClassesFor(main.QuestCharacterKey) ?? [],
            snap.InferredClass ?? "",
            main.QuestLedger?.SkillsFor(main.QuestCharacterKey) ?? []);

        return new Companion.CompanionHelperRequest(
            bundle.Inputs, bundle.Goals, bundle.Professions,
            // The curated eight, asked of the one file that owns them rather than spelled as
            // a number here (DRA-71 D8's rule, one surface along).
            Tradeskills.Standings(bundle.Skills).Count);
    }
}
