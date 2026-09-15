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
internal sealed class PhoneHelperSource
{
    /// <summary>This host's own pass — the seven reads and the eight <c>Gather</c> arguments,
    /// which DRA-83 moved into <see cref="HelperPass"/> because the guide surface needs the
    /// identical ones. Still ONE INSTANCE PER HOST (trap 45); only the wiring is shared.</summary>
    private readonly HelperPass _pass;

    /// <summary>The guide-reference answers for THIS host (DRA-83). They live here rather than in
    /// a second phone-side source because the phone is ONE host: its Helper screen and its quest
    /// screens have to read one pass, or the two would answer from stores taken moments apart
    /// (trap 33) and each would keep its own five-second clock.</summary>
    private readonly GuideAttachmentMemo _attachments;

    public PhoneHelperSource(MainWindow main)
    {
        _pass = new HelperPass(main);
        _attachments = _pass.Attachments();
    }

    /// <summary>
    /// The Helper's answers about the subjects the guide steps point at, for the phone's quest
    /// screens (DRA-83).
    ///
    /// <para>Asked from the QUEST request rather than from <see cref="Build"/>, because the two
    /// screens are offered independently: a phone paired on quests with the Helper screen off
    /// still draws guided Sky rows, and on the desktop those rows carry this line. It takes the
    /// same throttled read and the same memo, so a quiet tick costs a string join.</para>
    /// </summary>
    public GuideAttachmentLines Attachments()
    {
        // ON ITS OWN CLOCK, for the reason `GuideHelperSource.Refresh` keeps one: the quest
        // request is built every tick a phone is paired on that screen, and the fold behind this
        // is archived play rather than anything the player just did. `HelperSources.CacheFor` is
        // the interval the reads under it already keep.
        if (DateTime.Now - _asked >= HelperSources.CacheFor)
        {
            _asked = DateTime.Now;
            _pass.Read();
            _attachments.Refresh();
        }
        return _attachments.Lines;
    }

    private DateTime _asked = DateTime.MinValue;

    /// <summary>One pass. Called only while the screen is offered AND a device is paired —
    /// <c>CompanionHost.Tick</c>'s lazy rule, which matters here more than for any other
    /// source: the reads behind it are a session query and three probes.</summary>
    public Companion.CompanionHelperRequest Build()
    {
        _pass.Read();
        var bundle = _pass.Gather();

        return new Companion.CompanionHelperRequest(
            bundle.Inputs, bundle.Goals, bundle.Professions,
            // The curated eight, asked of the one file that owns them rather than spelled as
            // a number here (DRA-71 D8's rule, one surface along).
            Tradeskills.Standings(bundle.Skills).Count);
    }
}
