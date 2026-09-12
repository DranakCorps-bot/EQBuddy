using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **The Unlocks tab's All · Races · Classes lens, and the rule that there is only one list
/// of it** (DRA-65 acceptance A5).
///
/// <para>The lens already existed behind a ComboBox; the Founder asked for "a filter between
/// race and class unlocks" anyway, which is a verdict on a collapsed dropdown rather than a
/// missing feature. Making it a chip strip is a one-line change with one real risk: a strip
/// built in a window is three <c>Add("All") / Add("Races") / Add("Classes")</c> calls away
/// from being a SECOND list of section names, and then the day a fourth section exists the
/// strip offers three and the filter knows four. That is the drift
/// <see cref="UnlockLayout"/> exists to stop, so it is asserted rather than intended.</para>
///
/// <para>This reads the WINDOW'S SOURCE, the way <c>GameCommandsTests</c> and
/// <c>WidgetMenuTests</c> read XAML: the WPF layer has no unit test project
/// (docs/TestPlan.md §5), and the claim "the combo is gone and the chips come from Core" is
/// about what that file says.</para>
/// </summary>
public class UnlockSectionLensTests
{
    private const string View = "src/EQBuddy/QuestsView.xaml.cs";
    private const string Xaml = "src/EQBuddy/QuestsView.xaml";

    /// <summary>Three sections and no fourth. Deity is deliberately absent — the game has not
    /// defined its requirements, so a fourth chip would filter to nothing.</summary>
    [Fact]
    public void TheLensIsAllRacesClasses()
    {
        Assert.Equal([UnlockLayout.SectionAll, UnlockLayout.RacesHeading, UnlockLayout.ClassesHeading],
            UnlockLayout.Sections);
    }

    /// <summary>Every section the strip can offer actually NARROWS something — an offered lens
    /// that filters to nothing is the inert control this tab already refuses for the state
    /// combo.</summary>
    [Fact]
    public void EverySectionOfferedIsOneTheTabCanDraw()
    {
        foreach (var lens in UnlockLayout.Sections)
        {
            var races = UnlockLayout.InSection(UnlockLayout.RacesHeading, lens);
            var classes = UnlockLayout.InSection(UnlockLayout.ClassesHeading, lens);
            Assert.True(races || classes,
                $"the '{lens}' chip draws neither section, so it is a lens onto nothing.");
        }
        // ...and the two narrowing chips really do narrow, or the strip is decoration.
        Assert.False(UnlockLayout.InSection(UnlockLayout.ClassesHeading, UnlockLayout.RacesHeading));
        Assert.False(UnlockLayout.InSection(UnlockLayout.RacesHeading, UnlockLayout.ClassesHeading));
        Assert.True(UnlockLayout.InSection(UnlockLayout.RacesHeading, UnlockLayout.SectionAll));
        Assert.True(UnlockLayout.InSection(UnlockLayout.ClassesHeading, UnlockLayout.SectionAll));
    }

    /// <summary>
    /// **The combo is gone, the strip is there, and the names come from Core.** All three
    /// halves matter: a retired control left in the XAML is a second way in that nothing
    /// paints (trap 29's shape), and a strip built from three literals is the second producer
    /// this file exists to refuse.
    /// </summary>
    [Fact]
    public void TheStripReplacedTheComboAndItsNamesStillComeFromCore()
    {
        var view = File.ReadAllText(Path.Combine(RepoRoot(), View));
        var xaml = File.ReadAllText(Path.Combine(RepoRoot(), Xaml));

        Assert.DoesNotContain("UnlockSectionCombo", xaml);
        Assert.DoesNotContain("UnlockSectionCombo", view);
        Assert.Contains("UnlockSectionStrip", xaml);
        Assert.Contains("UnlockLayout.Sections", view);

        // No hand-written copy of the list beside the strip that reads it. The chip builder
        // takes its labels from the loop variable, so a literal "Races" or "Classes" chip
        // could only be a second list.
        Assert.DoesNotContain("_unlockSections.Add(\"", view);
    }

    /// <summary>
    /// **The contract the window's guidance loop zips on**, pinned here because it is invisible
    /// at the call site: <see cref="UnlockLayout.Groups"/> emits exactly one row per
    /// <see cref="UnlockProgress.Actionable"/> entry, in order, which is how a surface pairs a
    /// drawn row with the criterion its guidance came from. The alternative — splitting the row
    /// id back apart — reads one fact out of a string that contains the separator it would
    /// split on (trap 4).
    /// </summary>
    [Fact]
    public void EachGroupHasOneRowPerActionableCriterionInOrder()
    {
        var achievements = AchievementsImport.Parse(File.ReadAllLines(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "fixtures", "achievements", "hateborne.txt")));
        var races = UnlockRequirements.Races(achievements);
        Assert.NotEmpty(races);

        var groups = UnlockLayout.Groups(races, null, UnlockLayout.RacesHeading);

        Assert.Equal(races.Count, groups.Count);
        for (var i = 0; i < races.Count; i++)
        {
            var actionable = races[i].Actionable;
            Assert.Equal(actionable.Count, groups[i].Rows.Count);
            for (var r = 0; r < actionable.Count; r++)
            {
                var expected = actionable[r].Subject.Length > 0
                    ? actionable[r].Subject : actionable[r].Text;
                Assert.Equal(expected, groups[i].Rows[r].Title);
            }
        }
    }

    private static string RepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "EQBuddy.slnx")))
            d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
