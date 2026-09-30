namespace EQBuddy.UI.Shared;

/// <summary>
/// What the file picker offers, in one place (#197, wizen).
///
/// The picker filtered on <c>*.wav;*.mp3</c>. Playback does not: it hands the file to the
/// operating system's own media stack, which plays a good deal more than that — wizen
/// found it by typing <c>*</c> into the picker and choosing a <c>.ogg</c>, which worked.
/// So the picker was advertising a restriction the player does not have, and the honest
/// fix is to widen the picker rather than to narrow the promise.
///
/// Nothing validates an extension anywhere else, deliberately: a format this list has
/// never heard of still plays if the OS can play it, exactly as <c>.ogg</c> already did.
/// This is a list of what to OFFER, not a list of what is allowed.
/// </summary>
public static class AlertSoundFormats
{
    /// <summary>Offered in the picker. Everything the Windows media stack decodes out of
    /// the box.</summary>
    public static readonly IReadOnlyList<string> Extensions =
    [
        "wav", "mp3", "ogg", "opus", "m4a", "aac", "wma", "flac", "aiff", "aif", "mid", "midi",
    ];

    /// <summary>The glob patterns <see cref="WpfFilter"/> joins.</summary>
    public static string[] Patterns =>
        [.. Extensions.Select(e => "*." + e)];

    /// <summary>WPF's <c>OpenFileDialog.Filter</c>. "All files" stays second, because the
    /// list above is what we know about rather than what the OS can do.</summary>
    public static string WpfFilter =>
        "Sound files|" + string.Join(";", Patterns) + "|All files (*.*)|*.*";
}

/// <summary>Where the clip that is about to play actually came from.</summary>
public enum AlertSoundSource
{
    /// <summary>Nothing to play, and nothing wrong — the choice was "Off".</summary>
    Silent,
    /// <summary>One of the seven built-ins, resolved to this platform's own file.</summary>
    BuiltIn,
    /// <summary>The player's own sound file, in whatever format the OS plays.</summary>
    Custom,
    /// <summary>The chosen sound could not be found, so a built-in stands in for it.
    /// <see cref="AlertSoundPlan.MissingFile"/> says what was asked for.</summary>
    Substitute,
    /// <summary>Nothing playable at all — not even the stand-in exists.</summary>
    Unplayable,
}

/// <summary>
/// One play decided: which file, at what volume, and whether the player asked for
/// something that isn't there. Every source except <see cref="AlertSoundSource.Silent"/>
/// and <see cref="AlertSoundSource.Unplayable"/> carries <see cref="Volume"/>, which is
/// the whole point — see <see cref="AlertSoundPlanner"/>.
/// </summary>
/// <param name="Source">Which of the routes below produced <paramref name="FilePath"/>.</param>
/// <param name="FilePath">The file to hand the player, or "" when there is nothing to play.</param>
/// <param name="Volume">The player's alert volume, clamped to 0..1.</param>
/// <param name="MissingFile">The sound that was asked for and could not be found, or ""
/// when nothing is wrong. Non-empty means the UI owes the player a message: a sound the
/// player picked has gone away, and silently substituting is the kind of no-op this
/// project treats as a bug.</param>
public sealed record AlertSoundPlan(
    AlertSoundSource Source, string FilePath, double Volume, string MissingFile)
{
    /// <summary>True when this play will honour the Options volume slider. It is false
    /// only when nothing plays — there is deliberately no route that makes a noise the
    /// slider cannot reach (#153).</summary>
    public bool CarriesVolume => FilePath.Length > 0;

    /// <summary>The player picked a file that isn't there any more. Say so.</summary>
    public bool ShouldReportMissingFile => MissingFile.Length > 0;
}

/// <summary>
/// Decides what a single alert-sound play does, with no audio device and no window in
/// sight — the seam that lets #153 be a unit test instead of a field report.
///
/// The bug: a custom .wav that could not be found fell through to the OS notification
/// sound (WPF <c>SystemSounds.Asterisk</c>, an Avalonia <c>Console.Beep</c>). Those are
/// the one route out of this method that the Options volume slider cannot reach, and it
/// was reachable ONLY for custom paths — the seven built-ins live in
/// <c>C:\Windows\Media</c> (or the desktop sound theme) and are always there. So the
/// slider "worked for built-ins and did nothing for custom files": the custom sound was
/// never playing at all, the system ding was, at the system's own volume
/// (#153, adndmike).
///
/// The rule this class encodes: every audible outcome comes back as a file plus the
/// volume, so there is nothing left to play behind the slider's back. When the chosen
/// file is gone, a built-in stands in — at the slider's volume — and
/// <see cref="AlertSoundPlan.MissingFile"/> tells the caller to say so out loud.
/// </summary>
public static class AlertSoundPlanner
{
    /// <summary>The stand-in when the chosen sound is missing: the palette's own default,
    /// so a substitution sounds like EQBuddy rather than like Windows.</summary>
    public const string SubstituteName = "Ding";

    /// <summary>
    /// Work out what one play should do.
    /// </summary>
    /// <param name="choiceOrPath">A built-in name, a legacy SystemSounds name, "Off", or
    /// the full path of the player's own file.</param>
    /// <param name="volume">AppSettings.AlertVolume, unclamped.</param>
    /// <param name="locateBuiltIn">Maps a built-in NAME to this platform's file path
    /// ("" when the platform has no clip for it — a Linux sound theme need not carry
    /// every freedesktop event).</param>
    /// <param name="exists">File-existence probe; production passes File.Exists.</param>
    public static AlertSoundPlan Plan(
        string? choiceOrPath,
        double volume,
        Func<string, string> locateBuiltIn,
        Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(locateBuiltIn);
        ArgumentNullException.ThrowIfNull(exists);

        // NaN would survive Math.Clamp and reach the player as NaN; a settings.json that
        // has been hand-edited is exactly where that comes from.
        var level = double.IsNaN(volume) ? 1.0 : Math.Clamp(volume, 0.0, 1.0);
        var raw = choiceOrPath ?? "";

        // "Off" is a real answer, not a failure — nobody is owed a message for it.
        if (string.Equals(raw.Trim(), AlertSoundCatalog.OffChoice, StringComparison.OrdinalIgnoreCase))
            return new AlertSoundPlan(AlertSoundSource.Silent, "", level, "");

        var choice = AlertSoundCatalog.Normalize(raw);

        if (!AlertSoundCatalog.IsCustom(choice))
        {
            var builtIn = locateBuiltIn(choice) ?? "";
            if (builtIn.Length > 0 && exists(builtIn))
                return new AlertSoundPlan(AlertSoundSource.BuiltIn, builtIn, level, "");
            // A gap in the platform's sound theme is ours to paper over, not the
            // player's to be told about — they picked a sound we offered.
            return Substitute(level, missing: "", locateBuiltIn, exists);
        }

        if (choice.Length > 0 && exists(choice))
            return new AlertSoundPlan(AlertSoundSource.Custom, choice, level, "");

        return Substitute(level, missing: choice, locateBuiltIn, exists);
    }

    private static AlertSoundPlan Substitute(
        double level, string missing, Func<string, string> locateBuiltIn, Func<string, bool> exists)
    {
        var stand = locateBuiltIn(SubstituteName) ?? "";
        return stand.Length > 0 && exists(stand)
            ? new AlertSoundPlan(AlertSoundSource.Substitute, stand, level, missing)
            : new AlertSoundPlan(AlertSoundSource.Unplayable, "", level, missing);
    }

    /// <summary>The one-line message a UI shows when a picked sound has gone missing.
    /// Names the file, because "your alert sound is missing" without the path leaves the
    /// player hunting through Options for which one.</summary>
    public static string MissingFileMessage(string missingFile) =>
        $"Alert sound file is missing — playing {SubstituteName} instead. " +
        $"Pick another in Options: {missingFile}";
}
