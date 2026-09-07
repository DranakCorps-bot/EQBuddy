using System.Windows;
using System.Windows.Controls;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Role = EQBuddy.UI.Shared.DesignTokens.TypeRole;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// **The consent question for the one-time EQBuddy 1.x profile import** — the first thing a
/// player sees the first time EQBuddy Evolved runs beside an EQBuddy 1.x install (Fable's
/// transition plan §2, TR-1).
///
/// Every sentence it draws comes from <see cref="ProfileImportReadout"/>, which is unit
/// tested; this file is layout and one boolean. That split is not tidiness — the words are
/// promises about a player's data ("copied, never moved"), and a promise spelled inline in
/// a window is a promise nothing can check. It is also what makes Bevel's follow-on one-pager
/// an edit to one file rather than to a dialog.
///
/// **Modal, at startup, before anything is loaded** — see
/// <see cref="ProfileImportStartup"/> for why the moment is not negotiable. It is a plain
/// window with the system's own chrome rather than the widget's frameless look: this one
/// asks a question a player has to be able to read, dismiss and understand before EQBuddy
/// has become anything to them, and a chromeless always-on-top box would be the least
/// trustworthy possible frame for "may I copy your profile".
/// </summary>
internal sealed class ProfileImportWindow : Window
{
    /// <summary>The answer. False for every path that is not the player ticking the box and
    /// pressing Continue — including closing the window, which is a decline and not a
    /// deferral (there is one close, like Setup's, for the same reason).</summary>
    public bool Accepted { get; private set; }

    private readonly CheckBox? _consent;

    public ProfileImportWindow(ProfileImportOffer offer)
    {
        Title = ProfileImportReadout.WindowTitle;
        Width = 520;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        SetResourceReference(BackgroundProperty, "BgBrush");

        var root = new StackPanel { Margin = new Thickness(18, 14, 18, 16) };
        Content = root;

        var running = offer.Block == ProfileImportBlock.LegacyIsRunning;

        var head = DesignSystem.Text(Role.TitleWindow,
            running ? ProfileImportReadout.LegacyRunningHeadline : ProfileImportReadout.Headline);
        head.Ink("AccentBrush");
        root.Children.Add(head);

        if (running)
        {
            // The one refusal a player can clear, so it is said rather than swallowed —
            // and NO marker is written for it (ProfileImportStartup), so the offer comes
            // back on the next launch. A silent no-op here would be a player who never
            // learns their 1.x profile could have come over at all.
            root.Children.Add(Line(ProfileImportReadout.LegacyRunning, Role.Body, Tok.SpaceS));
        }
        else
        {
            root.Children.Add(Line(ProfileImportReadout.Lead, Role.Body, Tok.SpaceS));

            _consent = new CheckBox
            {
                // Default-CHECKED: door D2's stated assumption (opt-in, default-checked),
                // which is one boolean if David flips it. Never silently pre-accepted —
                // the box is on screen, beside a sentence saying what leaving it unticked
                // does.
                IsChecked = true,
                Margin = new Thickness(0, Tok.SpaceM, 0, 0),
                Content = DesignSystem.Text(Role.Body, ProfileImportReadout.Consent)
                    .Ink("TextBrush"),
            };
            root.Children.Add(_consent);

            root.Children.Add(Line(ProfileImportReadout.CopyNeverMove, Role.Metadata, Tok.SpaceXs));
            root.Children.Add(Line(ProfileImportReadout.StartFresh, Role.Metadata, Tok.SpaceXs));

            root.Children.Add(Line(ProfileImportReadout.ManifestHeadline, Role.Body, Tok.SpaceM));
            root.Children.Add(Line(
                ProfileImportReadout.Volume(offer.FileCount, offer.ByteCount),
                Role.Metadata, Tok.SpaceXxs));
            foreach (var entry in offer.Entries)
                root.Children.Add(Line(ProfileImportReadout.ManifestRow(entry),
                    Role.Metadata, Tok.SpaceXxs));
        }

        var go = Theming.Button(ProfileImportReadout.Continue, isDefault: true);
        go.HorizontalAlignment = HorizontalAlignment.Left;
        go.Margin = new Thickness(0, Tok.SpaceL, 0, 0);
        go.Click += (_, _) =>
        {
            Accepted = _consent?.IsChecked == true;
            Close();
        };
        root.Children.Add(go);
    }

    private static TextBlock Line(string text, Role role, double top)
    {
        var block = DesignSystem.Text(role, text);
        block.TextWrapping = TextWrapping.Wrap;
        block.Ink(role == Role.Body ? "TextBrush" : "DimBrush");
        block.Margin = new Thickness(0, top, 0, 0);
        return block;
    }
}
