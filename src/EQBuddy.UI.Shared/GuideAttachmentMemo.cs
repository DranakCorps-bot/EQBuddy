using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// **When the guide surface re-asks the Helper** — the one producer of that decision (DRA-83).
///
/// <para><see cref="GuideAttachmentLines.Read"/> runs an engine over this character's whole
/// stored history, so it cannot happen on a repaint gate or on a wire tick. It happens when the
/// EVIDENCE MOVES, and "has it moved" is <see cref="HelperSources.Signature"/>'s answer —
/// content-folded, because a swap leaves a count unmoved (trap 72).</para>
///
/// <para><b>It is here rather than in each host because there are two hosts</b> — the desktop's
/// <c>GuideHelperSource</c> and the phone's <c>PhoneHelperSource</c> — and "when to rebuild" is
/// exactly the kind of decision that drifts when it is written twice: one of them starts
/// rebuilding per tick, or stops rebuilding at all, and both bugs are invisible until somebody
/// measures a stale line. The MEMO itself is still per host (trap 45): each holds its own
/// instance over its own <see cref="HelperSources"/>, because a cache two owners invalidate is
/// state rather than a producer.</para>
/// </summary>
/// <param name="signature">The evidence signature — <see cref="HelperSources.Signature"/> after
/// that host's own <c>Read</c>.</param>
/// <param name="gather">The assembled pass, asked for only when the signature moved. It is a
/// delegate rather than a value so a host never pays <c>Gather</c> on a quiet tick.</param>
/// <param name="catalog">Which guide catalog's references to answer. A host passes
/// <c>GuideCatalog.Default</c>; a test passes its own fixture.</param>
public sealed class GuideAttachmentMemo(
    Func<string> signature, Func<HelperInputs> gather, Func<GuideCatalog> catalog)
{
    private string _signature = "";
    /// <summary>Has a pass ever run? A BOOLEAN and not "are the answers empty": an empty answer
    /// set is the honest, common and STABLE state for a character with no history, and treating
    /// it as "not read yet" would re-fold that history on every tick for exactly the players
    /// with nothing to gain from it.</summary>
    private bool _read;

    /// <summary>The worded answers, or <see cref="GuideAttachmentLines.None"/> before the first
    /// <see cref="Refresh"/> and after one that found nothing — which is the normal state for a
    /// character whose own play says nothing about what the catalog points at.</summary>
    public GuideAttachmentLines Lines { get; private set; } = GuideAttachmentLines.None;

    /// <summary>Rebuild if the evidence moved, and hand back the signature so a caller can fold
    /// it into its own repaint key. A caller that drops it draws the moment before for the rest
    /// of the session.</summary>
    public string Refresh()
    {
        var current = signature();
        // `_read` and not an empty-answer test, and the first call is why: the signature of an
        // empty history is "" and so is this field's initial value, so a gate on the string
        // alone would never take a first pass at all.
        if (_read && current == _signature) return _signature;
        _signature = current;
        _read = true;
        Lines = GuideAttachmentLines.Read(gather(), catalog());
        return _signature;
    }
}
