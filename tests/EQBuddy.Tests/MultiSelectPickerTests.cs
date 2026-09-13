using System.Text.RegularExpressions;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **THE SIBLING OF THE CHIP RULE** (DRA-71 D2, Fable plan P1): the multi-select dropdown is
/// <c>EqMultiPicker</c> — never hand-build another one.
///
/// <para>CLAUDE.md has carried <c>EqChip</c>'s version of this sentence since gate 2b, and it
/// was written after sixteen hand-built pill strips had already drifted apart. Multi-select had
/// no primitive at all until this slice, so there was exactly one hand-built one to retire — a
/// <c>Popup</c> of <c>CheckBox</c>es typed into <c>QuestsView.xaml</c> — and the moment to make
/// the rule enforceable is the moment the second one does not exist yet.</para>
///
/// <para><b>BOTH HALVES, because half of this pairing is vacuous on its own (trap 34).</b>
/// <see cref="NoXamlHandBuildsAMultiSelectDropdown"/> forbids the wrong thing and cannot see a
/// surface that quietly grew its own picker in C#;
/// <see cref="EverySurfaceWithAMultiSelectGoesThroughThePrimitive"/> is the curated must-list
/// that can. A "no X may do Y" rule with no must-list beside it passes brilliantly on an empty
/// repo.</para>
///
/// <para><b>And the detector is proven to FIRE, in the commit that adds it (trap 78).</b> The
/// scanner is a pure function over markup, so <see cref="RetiredMarkup"/> is the real
/// <c>ClassPopup</c> block this slice deleted, kept as a committed negative. A guard aimed at
/// the wrong thing is at least green for a reason; a guard aimed at NOTHING — an empty pattern
/// list, a regex that never matches — is green for no reason at all, and only the second one is
/// invisible.</para>
/// </summary>
public class MultiSelectPickerTests
{
    /// <summary>The markup DRA-71 D2 deleted from <c>QuestsView.xaml</c>, verbatim. It is the
    /// shape the scanner exists to catch, and keeping it here is what proves the scanner can
    /// catch it — see the class summary.</summary>
    private const string RetiredMarkup = """
                <Popup x:Name="ClassPopup" PlacementTarget="{Binding ElementName=ClassBtn}"
                       Placement="Bottom" StaysOpen="False" AllowsTransparency="True">
                    <Border Background="{DynamicResource PopupBrush}"
                            CornerRadius="{DynamicResource CornerCard}"
                            BorderBrush="{DynamicResource BorderBrush}" BorderThickness="1"
                            Padding="{DynamicResource PadCard}">
                        <StackPanel x:Name="ClassChecks"/>
                    </Border>
                </Popup>
        """;

    /// <summary>
    /// **The curated must-list.** Every surface in the app that offers "tick as many as you
    /// like, behind one face" — the half a forbid-rule cannot see. A new one that skips the
    /// primitive is caught here, and a row naming a file that no longer has a picker fails
    /// too, so the list cannot rot into decoration.
    /// </summary>
    public static TheoryData<string, string> SurfacesWithAMultiSelect() => new()
    {
        {
            @"src\EQBuddy\QuestsView.xaml.cs",
            "the quest window's class lens — up to three active classes, per character (#184)"
        },
        {
            @"src\EQBuddy\HelperRoom.cs",
            "the Helper's goals, and the faction sub-picker under them (DRA-71 D2)"
        },
    };

    [Theory]
    [MemberData(nameof(SurfacesWithAMultiSelect))]
    public void EverySurfaceWithAMultiSelectGoesThroughThePrimitive(string relative, string what)
    {
        var path = Path.Combine(RepoRoot(), relative);
        Assert.True(File.Exists(path), $"{relative} is gone — this must-list row names {what}.");

        Assert.Contains("EqMultiPicker", File.ReadAllText(path));
    }

    /// <summary>No XAML declares a popup with check rows in it. That is the hand-built shape,
    /// and it is the only one the markup can express.</summary>
    [Theory]
    [MemberData(nameof(EveryShippedXaml))]
    public void NoXamlHandBuildsAMultiSelectDropdown(string relative)
    {
        var offenders = HandBuiltPickers(File.ReadAllText(Path.Combine(RepoRoot(), relative)));

        Assert.True(offenders.Count == 0,
            $"{relative} hand-builds a multi-select dropdown ({string.Join(", ", offenders)}). "
            + "The multi-select dropdown is EqMultiPicker — never hand-build another one.");
    }

    /// <summary>**The prove-fail.** The scanner really does redden on the markup this slice
    /// deleted. Without this the two assertions above are indistinguishable from a regex that
    /// matches nothing (trap 78).</summary>
    [Fact]
    public void TheScannerFindsTheMarkupThisSliceRetired() =>
        Assert.Equal(["ClassPopup"], HandBuiltPickers(RetiredMarkup));

    /// <summary>And it does not fire on a popup that is NOT a multi-select — the buff
    /// autocomplete and the timeline's hover tip are popups and must stay legal. Every
    /// equality assertion deserves one negative (trap 39).</summary>
    [Fact]
    public void TheScannerLeavesPopupsThatAreNotPickersAlone() =>
        Assert.Empty(HandBuiltPickers("""
            <Popup x:Name="MarkTip" Placement="Relative" StaysOpen="False">
                <Border><TextBlock x:Name="MarkTipText"/></Border>
            </Popup>
            """));

    /// <summary>And it leaves a COMMENT about a popup alone — the first run of this suite
    /// reddened on the note left where <c>ClassPopup</c> used to be, which would have taught
    /// the next person that the way past this guard is to stop writing the comment.</summary>
    [Fact]
    public void TheScannerDoesNotReadACommentAsMarkup() =>
        Assert.Empty(HandBuiltPickers("""
            <!-- The hand-built <Popup> of CheckBoxes that used to sit here is gone. -->
            <CheckBox x:Name="SomethingElse"/>
            """));

    /// <summary>Every <c>&lt;Popup&gt;</c> in the markup that contains a
    /// <c>&lt;CheckBox&gt;</c>, by name. Pure over TEXT so the same function reads the shipped
    /// files and the committed negative — a detector that can only be pointed at the repo is a
    /// detector nobody can prove fires.</summary>
    private static List<string> HandBuiltPickers(string xaml)
    {
        // COMMENTS ARE NOT MARKUP, and this caught itself on the first run: the note left
        // where ClassPopup used to be says the word "<Popup>", and a scanner reading it found a
        // block that ran to the end of the file and swept up two unrelated CheckBoxes. A guard
        // that reddens on a comment about the thing it forbids is a guard the next person
        // learns to edit around.
        xaml = Regex.Replace(xaml, "<!--.*?-->", "", RegexOptions.Singleline);

        var found = new List<string>();
        foreach (Match open in Regex.Matches(xaml, "<Popup\\b"))
        {
            var close = xaml.IndexOf("</Popup>", open.Index, StringComparison.Ordinal);
            var block = close < 0 ? xaml[open.Index..] : xaml[open.Index..close];
            if (!block.Contains("<CheckBox", StringComparison.Ordinal)
                && !Regex.IsMatch(block, "x:Name=\"\\w*Checks\"")) continue;
            var name = Regex.Match(block, "x:Name=\"(?<n>\\w+)\"");
            found.Add(name.Success ? name.Groups["n"].Value : $"at offset {open.Index}");
        }
        return found;
    }

    public static TheoryData<string> EveryShippedXaml()
    {
        var root = RepoRoot();
        var data = new TheoryData<string>();
        foreach (var file in Directory.EnumerateFiles(
                     Path.Combine(root, "src"), "*.xaml", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                continue;
            data.Add(Path.GetRelativePath(root, file));
        }
        return data;
    }

    private static string RepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "EQBuddy.slnx")))
            d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
