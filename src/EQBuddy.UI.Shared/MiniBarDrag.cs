namespace EQBuddy.UI.Shared;

/// <summary>What a press on a mini-bar chip has turned out to be.</summary>
public enum MiniBarGesture
{
    /// <summary>Still inside the system's drag threshold — it is nobody's gesture yet, and
    /// it is still allowed to become the chip's CLICK.</summary>
    Pending,

    /// <summary>Past the threshold, and horizontally — the chip is being carried.</summary>
    Reorder,

    /// <summary>Past the threshold, and vertically. **This means nothing, on purpose**: the
    /// bar is one row, so up-and-down on a chip is neither a reorder nor a widget move.
    /// Inventing a meaning for it is not this lock's to spend — but it must still cancel the
    /// click, or a player who wiped the pointer down off the bar would pin a peek they never
    /// asked for.</summary>
    Dead,
}

/// <summary>What a completed carry turned out to MEAN — which settings it writes (SIGNED
/// #422 §3/§6).</summary>
public enum MiniBarDrop
{
    /// <summary>Nothing to write. The chip is where it started, or the carry was a pet chip
    /// that never left the insertion gap it was already in.</summary>
    None,

    /// <summary>An ordinary reorder among the cells — <c>MiniBarOrder</c> only.</summary>
    Reorder,

    /// <summary>The pet chip was carried out of the cells and into the always-on row's
    /// insertion gap: <c>HudGlancePet</c> becomes true and <c>MiniBarOrder</c> is left
    /// ALONE, which is what makes the eject land the chip back where the player had it.
    /// </summary>
    Insert,

    /// <summary>The inserted pet slot was carried back down among the cells:
    /// <c>HudGlancePet</c> becomes false, and if it landed beside a different neighbour the
    /// order is written in the SAME moment — one gesture, one write.</summary>
    Eject,
}

/// <summary>
/// THE MINI BAR'S REORDER, AS ARITHMETIC — which gesture a press became, where a carried
/// chip lands, and what the order reads afterwards.
///
/// **It is here rather than in <c>HudBarView</c> because the window cannot be unit-tested**
/// (docs/TestPlan.md §5), and every one of these three is a sum. The standing move for a
/// window bug is to lift the sum out; the standing move for a window FEATURE is not to put
/// one in. What stays in the view is genuinely the view's: hit-testing, capture, and the
/// ink that says a drag is happening.
///
/// **Nothing here knows about pixels-versus-DIPs** (trap 1). The caller measures its chips
/// through the framework's own transforms (<c>TransformToAncestor</c>, the <c>AnchorOf</c>
/// precedent) and hands in numbers that are already in one space; these functions only ever
/// compare them with each other.
/// </summary>
public static class MiniBarDrag
{
    /// <summary>
    /// Which gesture a press has become, given how far the pointer has travelled.
    ///
    /// **The thresholds are the SYSTEM's**, passed in rather than invented here: a player
    /// who set a different drag distance in Windows means it for every drag on their desk,
    /// and a chip that needed twice the travel of everything else would read as a chip that
    /// will not move. The caller passes <c>SystemParameters.MinimumHorizontalDragDistance</c>
    /// and its vertical twin; the unit tests pass their own so the rule is stated in numbers.
    ///
    /// The dominant axis decides, so a diagonal drag is a reorder rather than dead — a chip
    /// carried along a one-row bar is rarely level, and refusing a gesture that is plainly
    /// horizontal because it also drifted would read as a stuck chip.
    /// </summary>
    public static MiniBarGesture Classify(double dx, double dy, double horizontal, double vertical)
    {
        var ax = Math.Abs(dx);
        var ay = Math.Abs(dy);
        if (ax >= horizontal && ax >= ay) return MiniBarGesture.Reorder;
        if (ay >= vertical && ay > ax) return MiniBarGesture.Dead;
        return MiniBarGesture.Pending;
    }

    /// <summary>
    /// WHERE A CARRIED CHIP LANDS: the index it takes in the order once it is dropped.
    ///
    /// <paramref name="midpoints"/> is every drawn chip's horizontal centre, in draw order
    /// and in whatever one space the caller measured them in;
    /// <paramref name="from"/> is the carried chip's own index; <paramref name="x"/> is the
    /// pointer.
    ///
    /// The rule is "how many OTHER chips are now to my left", which is the whole of it: it
    /// needs no special case for either end (a pointer left of everything counts nothing and
    /// answers 0; right of everything counts them all and answers the last slot), and it
    /// cannot answer an index the list does not have. Midpoints rather than edges because a
    /// chip has to be carried PAST its neighbour to displace it — swapping the moment the
    /// two overlap makes a bar that flickers between two orders while the pointer sits
    /// still.
    /// </summary>
    public static int DropIndex(IReadOnlyList<double> midpoints, int from, double x)
    {
        if (midpoints.Count == 0) return 0;
        var to = 0;
        for (var i = 0; i < midpoints.Count; i++)
            if (i != from && midpoints[i] < x) to++;
        return Math.Clamp(to, 0, midpoints.Count - 1);
    }

    /// <summary>The landing slot that means "the always-on row's insertion gap", one step
    /// further left than any cell drag could reach before SIGNED #422. Negative because it is
    /// not an index into the drawn chips at all — it is the one place a carried chip can land
    /// that is not among them.</summary>
    public const int GlanceSlot = -1;

    /// <summary>
    /// WHERE A CARRIED **PET** CHIP LANDS — <see cref="DropIndex"/> plus the one slot beyond
    /// index 0 (SIGNED #422 §6; Bevel's arm condition, Helm-signed 2026-09-08).
    ///
    /// <paramref name="gapEndsAt"/> is the x at which the gap stops and the cells begin, in
    /// the same space as <paramref name="midpoints"/> and <paramref name="x"/>. **An edge and
    /// not a midpoint**, because this boundary is not "which side of a neighbour am I on" —
    /// it is "have I left the cells at all", and the caller measures the two readings of it
    /// off real chips:
    /// <list type="bullet">
    /// <item>Carrying pet UP from the cells, it is the DPS chip's own right edge — Bevel's
    /// arm condition, so the mark appears for that one slot rather than smearing across the
    /// whole always-on row.</item>
    /// <item>Carrying an INSERTED pet back DOWN, it is where the cells start, which is the
    /// mirror of the same sentence: the chip is in the gap until the pointer reaches
    /// them.</item>
    /// </list>
    ///
    /// **NaN is the honest "there is no gap"** — the row has not laid out yet, or the chip it
    /// is measured off could not answer — and it falls through to <see cref="DropIndex"/> by
    /// construction, since every comparison with NaN is false. A pet chip carried on a bar
    /// that cannot say where its own slots are behaves exactly like any other chip.
    /// </summary>
    public static int PetDropIndex(
        IReadOnlyList<double> midpoints, int from, double x, double gapEndsAt) =>
        x < gapEndsAt ? GlanceSlot : DropIndex(midpoints, from, x);

    /// <summary>
    /// WHAT A COMPLETED CARRY MEANS, given the chip and where it was let go.
    ///
    /// **The pet chip's index cannot answer this on its own, which is the whole reason this
    /// is a function.** An inserted pet slot is drawn FIRST, so it is index 0 of the carried
    /// list — the same number that means "the leftmost cell" for every other chip. `to ==
    /// from` is therefore a no-op for a cell chip and an EJECT for the inserted one, and a
    /// caller comparing indices alone would silently make the one gesture this release exists
    /// for do nothing.
    ///
    /// <paramref name="carriedIsPet"/> gates every pet answer: no other chip may take the
    /// gap, so a landing there from anything else is <see cref="MiniBarDrop.None"/> rather
    /// than a quietly-clamped reorder.
    /// </summary>
    public static MiniBarDrop DropKind(bool carriedIsPet, bool petInserted, int from, int to)
    {
        if (from < 0) return MiniBarDrop.None;
        if (carriedIsPet && petInserted)
            // Anywhere among the cells is an eject, INCLUDING slot 0 — see the note above.
            return to >= 0 ? MiniBarDrop.Eject : MiniBarDrop.None;
        if (to == GlanceSlot)
            return carriedIsPet ? MiniBarDrop.Insert : MiniBarDrop.None;
        return to < 0 || to == from ? MiniBarDrop.None : MiniBarDrop.Reorder;
    }

    /// <summary>
    /// The FULL order after a chip is carried from one DRAWN slot to another — remove and
    /// re-insert, never a swap.
    ///
    /// A swap is what a NUDGE does (<c>HudChipRow.Nudge</c>, one step at a time), and it is
    /// the wrong verb for a drag: carrying the last chip to the front with a swap would put
    /// the front one at the back, which is not what the player did with their hand.
    ///
    /// **The two lists are the whole reason this is not a two-line list operation.** What
    /// the player can see and grab is <paramref name="drawn"/> — the starred stats only —
    /// while what gets written is <paramref name="order"/>, every key there is. Re-indexing
    /// the drawn list and saving THAT would silently strip every un-starred key from the
    /// setting, and <c>ResolveOrder</c> would then append them canonically the next time one
    /// was starred: a stat you had placed once would come back somewhere else, with nothing
    /// naming the loss (trap 20's shape, arriving through an order rather than a fold).
    /// So the move is expressed against the chip it landed beside: the carried key is
    /// re-inserted immediately before that neighbour when it moved left, and immediately
    /// after it when it moved right, which leaves every un-starred key exactly where the
    /// player last left it.
    ///
    /// Out-of-range and no-op moves return a COPY of the order unchanged, so the caller has
    /// one path rather than a guard at every call site.
    /// </summary>
    public static List<string> Move(
        IReadOnlyList<string> order, IReadOnlyList<string> drawn, int from, int to)
    {
        if (from < 0 || from >= drawn.Count || drawn.Count == 0) return [.. order];
        to = Math.Clamp(to, 0, drawn.Count - 1);
        if (to == from) return [.. order];
        var key = drawn[from];
        var neighbour = drawn[to];
        var moved = new List<string>(order);
        var at = moved.IndexOf(key);
        if (at < 0) return [.. order];
        moved.RemoveAt(at);
        var beside = moved.IndexOf(neighbour);
        if (beside < 0) return [.. order];
        moved.Insert(to < from ? beside : beside + 1, key);
        return moved;
    }
}
