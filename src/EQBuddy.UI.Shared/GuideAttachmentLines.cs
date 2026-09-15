using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// **Every guide reference the Helper can answer today, worded once** (DRA-83, the DRA-70
/// plan's D5).
///
/// <para><b>It exists to make the engines run ONCE for a whole screen.</b> The Guide room draws
/// up to a capful of groups and a Sky tab alone carries 95 <c>GearUpgrade</c> references; asking
/// <see cref="Recommendations.Attached"/> per ROW would re-fold this character's session history
/// once per row, on a surface that repaints. So the whole catalog's references go in together —
/// <c>Attached</c> runs each engine once per call however many subjects ask for it — and what
/// comes back is a lookup keyed on the reference itself.</para>
///
/// <para><b>It decides no words.</b> <see cref="HelperPresentation.Attached"/> words a row and
/// <c>Recommendations</c> owns every number behind it; this class holds a dictionary and joins
/// duplicates. The reason it is in UI.Shared rather than Core is exactly that: the strings are
/// UI.Shared's by the standing KEEP (words in <c>HelperPresentation</c>, numbers in Core), and
/// the desktop, the shell and the phone must not each assemble their own.</para>
///
/// <para><b>Each host holds its own instance</b> — the same rule <see cref="HelperSources"/>
/// carries (trap 45): this is a memo, and a memo handed between hosts is state with two owners.
/// Rebuild it when <see cref="HelperSources.Signature"/> moves; a caller that folds it into its
/// repaint key gets a line that changes when the evidence does (trap 72).</para>
/// </summary>
public sealed class GuideAttachmentLines
{
    /// <summary>The Helper has nothing to say about anything — the state every surface starts
    /// in, and the state a surface with no <see cref="HelperInputs"/> stays in. Explicitly a
    /// value rather than a null, so a caller never branches on "do I have a lookup".</summary>
    public static readonly GuideAttachmentLines None = new(new Dictionary<string, string>());

    private readonly Dictionary<string, string> _byReference;

    private GuideAttachmentLines(Dictionary<string, string> byReference) =>
        _byReference = byReference;

    /// <summary>How many distinct references got an answer. Reported for a dump fact and a
    /// test: "the store says so" and "the screen says so" are different claims (trap 56), and
    /// this is the first half.</summary>
    public int Answered => _byReference.Count;

    /// <summary>
    /// Ask the Helper about every reference in a catalog, once.
    /// </summary>
    /// <param name="inputs">The pass <see cref="HelperSources.Gather"/> assembled. Null is a
    /// real state — a host that has not read its stores yet — and answers <see cref="None"/>
    /// rather than ranking over nothing.</param>
    /// <param name="catalog">The merged guide catalog the surface is drawing from. Both halves
    /// are scanned: a harvested guide carries no attachment today, and the day the transformer
    /// learns to place one it must not need a second reader.</param>
    public static GuideAttachmentLines Read(HelperInputs? inputs, GuideCatalog? catalog)
    {
        if (inputs is null || catalog is null) return None;

        var references = new List<GuideAttachment>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var guide in catalog.Guides)
        {
            foreach (var stage in guide.Stages)
            {
                Collect(stage.Attachments);
                foreach (var objective in stage.Objectives) Collect(objective.Attachments);
            }
        }
        if (references.Count == 0) return None;

        var lines = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var answer in Recommendations.Attached(inputs, references))
        {
            var line = HelperPresentation.Attached(answer);
            if (line.Length > 0) lines[Key(answer.Attachment)] = line;
        }
        return lines.Count == 0 ? None : new GuideAttachmentLines(lines);

        // DISTINCT references, because the answer is a property of `{ Kind, Key }` and nothing
        // else: 95 Sky guides pointing at one zone are one question. The engines memo per GOAL
        // inside a single Attached call, so this is not what saves the folds — it is what keeps
        // the answer list from carrying 95 copies of one sentence for the dictionary to
        // overwrite.
        void Collect(IReadOnlyList<GuideAttachment> attachments)
        {
            foreach (var attachment in attachments)
                if (attachment.Key.Length > 0 && seen.Add(Key(attachment)))
                    references.Add(attachment);
        }
    }

    /// <summary>
    /// The worded line for a step's references, or "" when the Helper answered none of them.
    ///
    /// <para>Several references on one step are joined the way the guide's own facts are — one
    /// per line — and in the order the curated file placed them, because that order is an
    /// author's decision about what matters first and this class has no better one.</para>
    /// </summary>
    public string For(IReadOnlyList<GuideAttachment>? attachments)
    {
        if (_byReference.Count == 0 || attachments is not { Count: > 0 }) return "";
        var lines = new List<string>();
        foreach (var attachment in attachments)
            if (_byReference.TryGetValue(Key(attachment), out var line) && !lines.Contains(line))
                lines.Add(line);
        return string.Join("\n", lines);
    }

    /// <summary>The reference's identity: the KIND and the KEY, which is the whole schema. A
    /// key alone would fold an item and a zone that share a name into one answer, and EQ has
    /// plenty of both.</summary>
    private static string Key(GuideAttachment attachment) =>
        attachment.Kind + "|" + attachment.Key;
}
