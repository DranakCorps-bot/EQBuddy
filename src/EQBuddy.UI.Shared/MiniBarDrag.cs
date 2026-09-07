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
