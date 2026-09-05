namespace EQBuddy;

/// <summary>
/// **THE ONE PLACE THE WIDGET'S IDENTITY PAIR IS TURNED ROUND, because a tuple conversion
/// is POSITIONAL and the two pairs in this codebase are spelled in OPPOSITE orders.**
///
/// <c>MainWindow.Identity</c> is <c>(Character, Server)</c>; <c>SessionArchiver.Identity</c>
/// — and therefore everything in <c>UI.Shared</c> that takes one, from
/// <see cref="UI.Shared.HomeReadout.Identity"/> to <see cref="UI.Shared.ShellRoomEmpty"/> —
/// is <c>(Server, Character)</c>. C# checks the element NAMES on that conversion with
/// nobody: assigning one to the other compiles, runs, and hands every reader the two
/// strings the wrong way round.
///
/// **It already did exactly that for one build**, in <c>HomeRoom</c>. The symptom was not
/// an exception: Home named the SERVER as the character and its readiness block globbed
/// <c>test_*-Inventory.txt</c>, reporting three dumps as never run while one sat on disk —
/// a room that renders perfectly and is entirely wrong, which no diff, build or screenshot
/// can see. Home and Live each carried their own hand-written destructure afterwards, which
/// is two chances to get it wrong; E-3 S1 added four more rooms that need the same pair, and
/// six copies of a conversion that has already been wrong once is trap 4's shape — one fact
/// with several sources, differing at exactly the point nobody looks.
///
/// So the destructure happens HERE and nowhere else. The rooms' own <c>Who()</c> methods
/// forward to it and keep their comments, because the comments are the reason anybody would
/// think twice before "simplifying" one of them back into an assignment.
/// </summary>
internal static class ShellRoomIdentity
{
    /// <summary>Who the widget is following, in the order every <c>UI.Shared</c> reader
    /// takes it. A DESTRUCTURE and not an assignment — see the type's summary for what the
    /// assignment costs and how it presents.</summary>
    public static (string Server, string Character) Of(MainWindow main)
    {
        var (character, server) = main.Identity;
        return (Server: server, Character: character);
    }
}
