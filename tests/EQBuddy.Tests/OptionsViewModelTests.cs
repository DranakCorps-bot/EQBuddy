using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

public sealed class OptionsViewModelTests
{
    private static (OptionsViewModel Vm, AppSettings Settings, Counter Persists) Create(AppSettings? settings = null)
    {
        var s = settings ?? new AppSettings();
        var counter = new Counter();
        return (new OptionsViewModel(s, () => counter.Value++), s, counter);
    }

    private sealed class Counter { public int Value; }

    [Fact]
    public void RecentWindowIndexRoundTrips()
    {
        var (vm, s, persists) = Create();
        Assert.Equal(1, vm.RecentWindowIndex);          // default 15 min
        vm.RecentWindowIndex = 0;
        Assert.Equal(5, s.RecentWindowMinutes);
        vm.RecentWindowIndex = 2;
        Assert.Equal(30, s.RecentWindowMinutes);
        Assert.Equal(2, persists.Value);
    }

    [Fact]
    public void SoundSelectionHandlesLegacyNamesAndCustomPaths()
    {
        var (vm, s, _) = Create(new AppSettings { AlertSound = "Question" });
        Assert.Equal(Array.IndexOf(AlertSoundCatalog.Names, "Notify"), vm.SoundIndex);   // legacy maps
        Assert.Equal("", vm.SoundFileNote);

        vm.SetCustomSound(@"C:\sounds\gong.wav");
        Assert.Equal(AlertSoundCatalog.Names.Length, vm.SoundIndex);                     // custom slot
        Assert.Contains("gong.wav", vm.SoundFileNote);

        vm.SelectNamedSound(0);
        Assert.Equal(AlertSoundCatalog.Names[0], s.AlertSound);
        Assert.True(vm.IsCustomSoundIndex(AlertSoundCatalog.Names.Length));
    }

    [Fact]
    public void CardsNormalizeMoveAndToggle()
    {
        var settings = new AppSettings { SectionOrder = ["kills", "bogus"] };
        var (vm, s, _) = Create(settings);

        // Unknown keys dropped, missing keys appended in default order, kills stays first.
        Assert.Equal("kills", s.SectionOrder[0]);
        Assert.Equal(OverlaySections.Catalog.Length, s.SectionOrder.Count);
        Assert.DoesNotContain("bogus", s.SectionOrder);

        vm.MoveCard("kills", -1);                        // top can't move up
        Assert.Equal("kills", s.SectionOrder[0]);
        vm.MoveCard("kills", +1);
        Assert.Equal("kills", s.SectionOrder[1]);

        vm.ToggleCard("money");
        Assert.True(vm.Cards.Single(c => c.Key == "money").Hidden);
        vm.ToggleCard("money");
        Assert.False(vm.Cards.Single(c => c.Key == "money").Hidden);
    }

    [Fact]
    public void RulesAddAndRemovePersist()
    {
        var (vm, s, persists) = Create();
        var rule = vm.AddRule();
        Assert.Single(s.TrackedRules);
        vm.RemoveRule(rule);
        Assert.Empty(s.TrackedRules);
        Assert.Equal(2, persists.Value);
    }

    [Fact]
    public void SliderLabelsAndClamping()
    {
        var (vm, s, _) = Create();
        vm.UiScale = 9;                                  // clamps
        Assert.Equal(2.0, s.UiScale);
        Assert.Equal("200%", vm.ScaleLabel);
        vm.BackgroundOpacity = 0.0;
        Assert.Equal(0.15, s.BackgroundOpacity, 3);
    }
}
