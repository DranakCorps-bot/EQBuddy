namespace EQBuddy.UI.Shared;

/// <summary>How long until a cue fires, written for a glance at a mini bar.</summary>
public static class Countdown
{
    /// <summary>m:ss while a minute or more remains, bare seconds after that — a respawn
    /// timer wants "8:12" and the tail of a cast cue wants "3s". Rounds up, so a cue never
    /// reads "0s" while it's still pending, and never goes negative. The round-up must not
    /// print "60s" either: a live clock crosses the minute at 59.98s-ish every cycle, and
    /// ceiling that to 60 read as the countdown stalling right at the boundary.</summary>
    public static string Format(TimeSpan left)
    {
        if (left < TimeSpan.Zero) left = TimeSpan.Zero;
        if (left.TotalMinutes >= 1) return $"{(int)left.TotalMinutes}:{left.Seconds:D2}";
        var secs = Math.Max(1, (int)Math.Ceiling(left.TotalSeconds));
        return secs == 60 ? "1:00" : $"{secs}s";
    }
}
