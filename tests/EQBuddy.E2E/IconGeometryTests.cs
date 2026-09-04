using System.Windows.Media;
using EQBuddy.UI.Shared;

namespace EQBuddy.E2E;

/// <summary>
/// Every icon path, through the REAL geometry parser — WPF's, which is the one the
/// shipping app hands these strings to at window-construction time. A typo there is an
/// exception in front of a player rather than a red build.
///
/// **Ported from <c>EQBuddy.Avalonia.Tests/IconGeometryTests</c> (E-2a), and it had to
/// come here.** <c>DesignSystemTests.EveryIconPathIsWellFormed</c> in
/// <c>EQBuddy.Tests</c> says so in as many words: it is "the cheap structural half",
/// and the real check "cannot live here — UI.Shared and its test project are deliberately
/// toolkit-free (ArchitectureTests), so there is no parser to call". This project is the
/// repo's one <c>net10.0-windows</c> test project, so it is where a WPF parser can be
/// called at all.
///
/// **It launches nothing.** It sits in the E2E project for the target framework and for
/// no other reason — see this project's README.
/// </summary>
public class IconGeometryTests
{
    public static TheoryData<string> IconNames()
    {
        var data = new TheoryData<string>();
        foreach (var name in IconPaths.Names) data.Add(name);
        return data;
    }

    [Theory]
    [MemberData(nameof(IconNames))]
    public void EveryIconParses(string name)
    {
        var bounds = Geometry.Parse(IconPaths.Path(name)).Bounds;
        Assert.True(bounds.Width > 0 && bounds.Height > 0, $"{name} parsed to nothing.");
    }

    /// <summary>Everything is drawn on one 24×24 grid, so a mixed set renders at mixed
    /// weights unless they agree. An icon that overflows the box gets clipped or scaled
    /// down beside its neighbours; one that occupies a corner of it renders as a speck.
    /// Both read as "the icons are broken" rather than as a bad path.</summary>
    [Theory]
    [MemberData(nameof(IconNames))]
    public void EveryIconFillsItsGridWithoutOverflowing(string name)
    {
        var bounds = Geometry.Parse(IconPaths.Path(name)).Bounds;
        const double box = IconPaths.ViewBox;

        Assert.True(bounds.X >= -0.5 && bounds.Y >= -0.5
            && bounds.Right <= box + 0.5 && bounds.Bottom <= box + 0.5,
            $"{name} draws outside the {box}×{box} grid: {bounds}");
        // Half the box in the larger dimension. Below that an icon is visibly lighter
        // than the set it sits in.
        Assert.True(Math.Max(bounds.Width, bounds.Height) >= box / 2,
            $"{name} only fills {bounds.Width:0.#}×{bounds.Height:0.#} of {box}×{box} — " +
            "it will render as a speck beside the others.");
    }
}
