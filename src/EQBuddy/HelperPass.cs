using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// **The widget's side of one Helper pass** — which of its stores the Helper reads, and which
/// of its per-tick facts <c>Gather</c> is given.
///
/// <para>It exists because there are now TWO widget-side hosts of the Helper — the phone
/// (<see cref="PhoneHelperSource"/>, DRA-71 D9) and the guide surface
/// (<see cref="GuideHelperSource"/>, DRA-83) — and both need the same seven reads and the same
/// eight <c>Gather</c> arguments. Written twice, that is the #210 shape the Helper's own modules
/// keep warning about: two callers assembling one bundle, drifting the first time one of them
/// learns a store. <see cref="HelperSources"/> is where the READS live; this is where "which
/// widget fields feed them" lives, and it is one place for the same reason.</para>
///
/// <para><b>One instance per HOST, never a shared one</b> (trap 45). <see cref="HelperSources"/>
/// is a cache with a five-second clock in it; a cache two owners invalidate is state rather than
/// a producer, so each host constructs its own pass and the PRODUCER is what they share.</para>
/// </summary>
internal sealed class HelperPass
{
    private readonly MainWindow _main;
    private readonly HelperSources _sources;

    public HelperPass(MainWindow main)
    {
        _main = main;
        _sources = new HelperSources(new HelperSources.Reads(
            StoredMobRows: main.StoredMobRows,
            StoredSessions: main.StoredSessions,
            StoredThroughput: main.StoredThroughput,
            StoredSales: main.StoredSales,
            LatestInventory: () => main.LatestInventory(),
            StatsFor: main.WikiItems.StatsFor,
            ActiveSessionRowId: () => main.ActiveSessionRowId));
    }

    /// <summary>Take the reads, behind <see cref="HelperSources"/>'s own throttle.
    /// The widget's pair is (Character, Server) and every UI.Shared reader takes
    /// (Server, Character) — named access rather than a positional destructure, which is the
    /// mistake <c>ShellRoomIdentity</c> exists to have made once and never again.</summary>
    public void Read() => _sources.Read(
        _main.CurrentSnapshot(), _main.Identity.Character, _main.Identity.Server);

    /// <summary>What these stores fold to, for a caller's repaint or wire fingerprint.</summary>
    public string Signature() => _sources.Signature();

    /// <summary>Assemble the pass. <see cref="Read"/> first — <c>Gather</c> does no I/O of its
    /// own and joins the throttled reads to the per-tick stores.</summary>
    public HelperSources.Bundle Gather() => _sources.Gather(
        _main.Settings, _main.QuestCharacterKey, _main.Unlocks, _main.ResolvedLevel,
        _main.QuestCatalog, ItemCatalog.Default,
        _main.QuestLedger?.ClassesFor(_main.QuestCharacterKey) ?? [],
        _main.CurrentSnapshot().InferredClass ?? "",
        _main.QuestLedger?.SkillsFor(_main.QuestCharacterKey) ?? []);

    /// <summary>A guide-reference memo over THIS pass (DRA-83) — the answers, and the decision
    /// about when to rebuild them, both from the shared modules.</summary>
    public GuideAttachmentMemo Attachments() =>
        new(Signature, () => Gather().Inputs, () => GuideCatalog.Default);
}
