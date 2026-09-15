using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// **The Helper's answers about guide references, for the GUIDE surface** (DRA-83, the DRA-70
/// plan's D5).
///
/// <para>It is <see cref="PhoneHelperSource"/>'s sibling: its own <see cref="HelperPass"/> — the
/// memo is per host (trap 45), because <see cref="QuestsWindow"/> and <see cref="QuestsRoom"/>
/// each build their own <see cref="QuestsView"/> and a cache with two owners is state. What is
/// shared is the PRODUCER: <c>Gather</c> stays the one place the Helper's inputs are assembled,
/// so a guide row cannot rank over a different set of stores than the Helper room does (trap 33).
/// </para>
///
/// <para><b>The second cache is what makes this affordable.</b>
/// <see cref="GuideAttachmentLines.Read"/> folds this character's whole history — far too much
/// for a repaint gate — so <see cref="GuideAttachmentMemo"/> rebuilds only when the evidence
/// signature moves, and that signature is what a caller folds into its own repaint key: the line
/// has to change when the evidence does (trap 72), and must not be recomputed when nothing has.
/// </para>
///
/// <para>A class rather than lines in the view, for the reason its sibling is one: a surface that
/// can live outside the window it is drawn in does.</para>
/// </summary>
internal sealed class GuideHelperSource
{
    private readonly HelperPass _pass;

    /// <summary>When to re-ask, decided in UI.Shared so this host and the phone's cannot answer
    /// it differently. The GOALS are deliberately not consulted: a goal chip is "what I am working
    /// toward this evening" and it narrows the Helper ROOM's short list, while a guide step's
    /// reference asks "the thing this step points at — what does my own play say about it".
    /// Answering that only while the matching chip happened to be ticked would make the line
    /// appear and disappear for a reason the guide surface never mentions.</summary>
    private readonly GuideAttachmentMemo _memo;

    public GuideHelperSource(MainWindow main)
    {
        _pass = new HelperPass(main);
        _memo = _pass.Attachments();
    }

    /// <summary>The worded answers the guide rows draw.</summary>
    public GuideAttachmentLines Lines => _memo.Lines;

    /// <summary>
    /// Take the reads, and rebuild the answers only if the evidence actually moved.
    /// </summary>
    /// <remarks>
    /// <b>On its own clock, because the CALLER's is one second.</b> The Guide surface repaints
    /// every tick (the shell room calls <c>Refresh</c> rather than <c>MaybeRefresh</c> on
    /// purpose — trap 56), and this sits inside its repaint KEY, so without a clock of its own
    /// every tick would pay a pool-signature join and a fold over every zone, mote and sale this
    /// character has. <see cref="HelperSources.CacheFor"/> is the interval because it is the one
    /// the reads behind it already keep: asking more often than the data can change is the
    /// steady-state allocation the perf audit's rule is about, and the answer a guide row draws
    /// is a fold of archived play rather than something the player just did.
    /// </remarks>
    /// <returns>The evidence signature, for the caller's repaint key. A caller that drops it
    /// draws the moment before for the rest of the session (trap 72) — which is exactly what the
    /// Quests tab did with its own checklist stores until it was measured.</returns>
    public string Refresh()
    {
        if (DateTime.Now - _asked < HelperSources.CacheFor) return _signature;
        _asked = DateTime.Now;
        _pass.Read();
        return _signature = _memo.Refresh();
    }

    private DateTime _asked = DateTime.MinValue;
    private string _signature = "";
}
