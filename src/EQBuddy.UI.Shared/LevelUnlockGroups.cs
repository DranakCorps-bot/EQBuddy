using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>One class's share of a level's unlocks, as a surface should draw it.</summary>
/// <param name="ClassName">The class, or <see cref="LevelUnlockGroups.SharedGroup"/> for the
/// class-agnostic AA categories (General, Archetype, Special) that belong to no one class.</param>
/// <param name="Rows">Name and right-column value, already ordered and labelled. Empty when
/// this class gains nothing at the level — which is a row the surface KEEPS, not one it
/// drops (Bevel, Helm-signed 2026-08-23).</param>
public sealed record LevelUnlockGroup(
    string ClassName,
    IReadOnlyList<(string Name, string Value)> Rows)
{
    public bool IsEmpty => Rows.Count == 0;
}

/// <summary>
/// The next level's unlocks, split by class.
///
/// **Why this is in UI.Shared and not in a window — nor in Core.** David asked for the grouping (2026-08-23,
/// via Helm) and three surfaces draw this list — both desktops and EQBuddy Mobile. #210 is
/// what happens when one of them decides for itself, and the phone receives GROUPS rather
/// than rows precisely so it cannot group them differently (trap 9's cousin).
///
/// **The premise that makes it worth doing at all**, and which this codebase had wrong until
/// David said so: *"you seem to think EQ Legends just lets you have 1 class when in fact you
/// can be 3 at a time."* A character is up to three classes at once, so a level's unlocks are
/// naturally several lists, not one — and "which of my classes is this for" is the question
/// the flat list could not answer without reading every row's value column.
///
/// (`ClassInference` still collapses three classes to one or to `""`; that is filed as a V3
/// in `FABLE.md`. This module takes whatever class list it is given and is correct for one,
/// three or none.)
///
/// It sits beside <see cref="LevelUnlockText"/> rather than in Core because the row VALUES
/// are its words — Core holds the facts, UI.Shared holds how they read. Both widgets and the
/// companion reference this assembly, so all three still get one answer.
/// </summary>
public static class LevelUnlockGroups
{
    /// <summary>The heading for AA rows that belong to no single class — General, Archetype
    /// and Special. They are shown to everyone because the wiki's tables never say which
    /// classes an Archetype row covers, and guessing would be inventing game truth
    /// (<see cref="LevelUnlocks"/>). Kept in their own group rather than repeated under every
    /// class, which would triple them on a three-class character.</summary>
    public const string SharedGroup = "Any class";

    /// <summary>Split one level's unlocks into per-class groups.
    ///
    /// Order is the caller's class order — the classes as the player's own list gives them,
    /// so the group that opens first is the one they think of first — with
    /// <see cref="SharedGroup"/> last, because a class-agnostic AA is the least specific
    /// answer to "what do I get".
    ///
    /// **A spell two classes share appears under BOTH** (Bevel, Helm-signed). It is one
    /// unlock and two answers to "what does this class get", and hiding it from the second
    /// class to avoid repeating it would be answering a different question.
    ///
    /// **A class that gains nothing keeps an EMPTY group.** Dropping it silently is
    /// indistinguishable from that class not existing, and for a Warrior — who has no spell
    /// table at any level — "nothing new" is the honest and complete answer rather than an
    /// absence. The surface renders the words; this decides that the row is there.</summary>
    public static IReadOnlyList<LevelUnlockGroup> ByClass(
        LevelUnlockSet set, IReadOnlyList<string> classes)
    {
        var groups = new List<LevelUnlockGroup>();
        foreach (var cls in classes)
        {
            var rows = new List<(string, string)>();
            rows.AddRange(set.Aas
                .Where(a => a.Class is { Length: > 0 } c
                    && c.Equals(cls, StringComparison.OrdinalIgnoreCase))
                .Select(a => (a.Name, LevelUnlockText.RowValue(a))));
            rows.AddRange(set.Spells
                .Where(s => s.Classes.Contains(cls, StringComparer.OrdinalIgnoreCase))
                .Select(s => (s.Name, LevelUnlockText.SpellRowValue(s))));
            groups.Add(new LevelUnlockGroup(cls, rows));
        }

        var shared = set.Aas
            .Where(a => a.Class is not { Length: > 0 })
            .Select(a => (a.Name, LevelUnlockText.RowValue(a)))
            .ToList();
        if (shared.Count > 0)
            groups.Add(new LevelUnlockGroup(SharedGroup, shared));

        return groups;
    }

    /// <summary>Does the split earn its chrome?
    ///
    /// **One group is a heading with nothing to choose between** — Bevel, Helm-signed
    /// 2026-08-23: *"One inferred class = names under the heading, no lone expander."* The
    /// surface asks this rather than counting, so the rule lives in one place and both
    /// desktops and the phone cannot disagree about when a fold appears.</summary>
    public static bool WorthGrouping(IReadOnlyList<LevelUnlockGroup> groups) => groups.Count > 1;

    /// <summary>
    /// Which group starts open. Bevel, Helm-signed: *"first inferred class open, the rest
    /// collapsed"* — read as the first class with something to SHOW, which is the same
    /// group in every ordinary case and a different one in the case that matters.
    ///
    /// **Found from a written prediction, before the screenshot** (trap 23). A Warrior
    /// whose next milestone is an Archetype AA produces exactly two groups: Warrior, empty,
    /// and <see cref="SharedGroup"/> holding the one row. Open-by-index would open the
    /// empty one, so the fold a player just expanded would read "Warrior — nothing new at
    /// 15" above a collapsed heading, and the single row the whole preview exists to show
    /// would be two clicks away. That is not what "first class open" was protecting.
    ///
    /// -1 when every group is empty — a state the surface should not draw a preview for at
    /// all, and the caller has the level to say so.
    /// </summary>
    public static int DefaultOpenIndex(IReadOnlyList<LevelUnlockGroup> groups)
    {
        for (var i = 0; i < groups.Count; i++)
            if (!groups[i].IsEmpty) return i;
        return -1;
    }

    /// <summary>What a group with nothing in it says. Its own method because it is the line
    /// a Warrior sees at almost every level, and a class that gains nothing must read as
    /// answered rather than broken.</summary>
    public static string NothingNew(int level) => $"Nothing new at {level}";
}
