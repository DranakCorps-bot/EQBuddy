using System.Globalization;
using System.Reflection;

namespace EQBuddy.UI.Shared;

/// <summary>
/// **"WHAT AM I RUNNING?", ANSWERED IN THE APP — ON A DEV BUILD ONLY** (DRA-705 §6, DRA-707 D3).
///
/// <para>The Founder's PC runs <c>main</c> on its own now (<c>scripts/auto-roll.ps1</c>), and
/// every build of it carries the same <c>2.0.x</c> as the release before it, so the version
/// alone cannot say which merge he is looking at. <c>scripts/install-local.ps1</c> — the
/// daily driver and auto-roll's one install path — builds with <c>EqDevBuild=true</c>, the
/// commit and the build time; <c>Directory.Build.props</c> turns those into assembly
/// metadata only under that property, and <c>release.ps1</c> never sets it. So a release
/// carries none of the three keys and <see cref="Marker"/> answers null: a player never sees
/// a dev marker. <c>DevBuildStampTests</c> holds the scripts to that; the E2E row
/// <c>ReleaseBuildMarkerTests.AReleaseConfigurationBuildShowsNoDevMarker</c> holds the shipped
/// configuration.</para>
///
/// <para>One producer, two readers (trap 4): Options draws <see cref="Current"/> as its own
/// line under the footer (the footer row is a horizontal StackPanel at a 390 MinWidth, so
/// appending to the version would clip it — trap 25), and Feedback's version line calls
/// <see cref="Append"/>. Neither builds the words, so they cannot disagree about which build
/// this is.</para>
/// </summary>
public static class DevBuildStamp
{
    public const string FlagKey = "EqDevBuild";
    public const string ShaKey = "EqDevSha";
    public const string BuiltAtKey = "EqDevBuiltAt";

    /// <summary>"dev abc1234 · Oct 1, 3:56 PM", or null for a build that is not a dev build.
    /// A dev build missing its commit or its time still says <c>dev</c> — a marker with a part
    /// missing is honest, a release-looking label on a dev build is not.</summary>
    public static string? Marker(IEnumerable<AssemblyMetadataAttribute> metadata, TimeZoneInfo zone)
    {
        bool dev = false;
        string? sha = null, builtAt = null;
        foreach (var a in metadata)
        {
            if (a.Key == FlagKey) dev = string.Equals(a.Value, "true", StringComparison.OrdinalIgnoreCase);
            else if (a.Key == ShaKey) sha = a.Value?.Trim();
            else if (a.Key == BuiltAtKey) builtAt = a.Value?.Trim();
        }
        if (!dev) return null;

        var text = "dev";
        if (!string.IsNullOrEmpty(sha)) text += " " + (sha.Length > 7 ? sha[..7] : sha);
        if (DateTimeOffset.TryParse(builtAt, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var at))
        {
            var local = TimeZoneInfo.ConvertTime(at, zone);
            text += " · " + local.ToString("MMM d, h:mm tt", CultureInfo.InvariantCulture);
        }
        return text;
    }

    /// <summary>This process's marker. Directory.Build.props stamps every assembly of one
    /// publish alike, so this assembly answers for the exe it ships in.</summary>
    public static string? Current { get; } =
        Marker(typeof(DevBuildStamp).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>(), TimeZoneInfo.Local);

    /// <summary><paramref name="label"/> with " · dev …" on a dev build; unchanged otherwise.</summary>
    public static string Append(string label, string? marker) =>
        marker is { Length: > 0 } m ? $"{label} · {m}" : label;

    public static string Append(string label) => Append(label, Current);
}
