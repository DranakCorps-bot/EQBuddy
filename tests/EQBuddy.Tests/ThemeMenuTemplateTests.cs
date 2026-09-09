using System.Xml.Linq;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>#110: the themed MenuItem template was flat (no PART_Popup), so the gear
/// menu's nested "Data &amp; imports" / "Help" shelves rendered but could never open.
/// These pin the XAML shape: submenu headers must get a template that actually
/// contains a popup with an items host. XML-level checks — WPF can't load on the
/// Linux CI leg, but the template's structure is still enforceable as markup.</summary>
public class ThemeMenuTemplateTests
{
    private static readonly XNamespace P = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Theme() =>
        XDocument.Load(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "src", "EQBuddy", "Theme.xaml"));

    [Fact]
    public void SubmenuHeaderTemplateHasPopupWithItemsHost()
    {
        var popup = Theme().Descendants(P + "Popup")
            .SingleOrDefault(p => (string?)p.Attribute(X + "Name") == "PART_Popup");
        Assert.NotNull(popup);

        var template = popup!.Ancestors(P + "ControlTemplate").First();
        Assert.Equal("MenuItem", (string?)template.Attribute("TargetType"));
        Assert.Contains(template.Descendants(P + "Popup").First().Descendants(),
            el => (string?)el.Attribute("IsItemsHost") == "True");
    }

    [Fact]
    public void MenuItemStyleRoutesSubmenuHeadersToThatTemplate()
    {
        var style = Theme().Descendants(P + "Style")
            .Single(s => (string?)s.Attribute("TargetType") == "MenuItem");
        var roleTrigger = style.Descendants(P + "Trigger")
            .SingleOrDefault(t => (string?)t.Attribute("Property") == "Role"
                               && (string?)t.Attribute("Value") == "SubmenuHeader");
        Assert.NotNull(roleTrigger);
        Assert.Contains(roleTrigger!.Elements(P + "Setter"),
            s => (string?)s.Attribute("Property") == "Template");
    }

    // GearMenuStillHasTheNestedShelves RETIRED (gear-menu-slim, DRA-25, 2026-09-08). It
    // pinned "Data & imports" and "Help" as nested MenuItem shelves inside the widget's
    // context menu — the surface #110's bug actually shipped on. The owner's lock cut both
    // submenus off that menu entirely (their rows moved to Options → Behavior, the Guide
    // room and an Options footer; see MainWindow.xaml's own comment above the ContextMenu),
    // and a grep across every XAML file in src/EQBuddy confirms MainWindow.xaml is the only
    // one that ever used <MenuItem> at all — so there is now no live nested-submenu surface
    // in the shipped app to point this test at. The two tests above it are untouched and
    // still pin the underlying mechanism (the Theme.xaml template and style, not any one
    // menu's content), so #110 stays guarded: the day a nested MenuItem submenu returns
    // anywhere, it inherits a template that already opens.
}
