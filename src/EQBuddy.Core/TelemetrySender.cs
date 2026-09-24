using System.Net;
using System.Net.Http;
using System.Text;

namespace EQBuddy.Core;

/// <summary>
/// **The one piece of EQBuddy that sends anything off the player's machine** — the opt-in
/// heartbeat's thin HTTP half (<c>docs/v2/telemetry.md</c> §3, §5; DRA-362 TEL-PR3).
///
/// It decides NOTHING. Whether a request may go, and what is in it, is
/// <c>UI.Shared/TelemetryHeartbeat</c>'s answer (trap 47); this file only carries a body that
/// policy already built. It holds the endpoint literal and the only <see cref="HttpClient"/>
/// that can reach it, and <c>TelemetryEndpointScanTests</c> refuses a second file that names
/// either.
///
/// What goes on the wire besides the body (§2): HTTPS to one host and
/// <c>Content-Type: application/json</c>. No cookies, no auth header, no custom header, and no
/// <c>User-Agent</c> — .NET's <see cref="HttpClient"/> sends none unless told to, and this one
/// is never told to. Every failure is a VALUE, never an exception: a send that throws into the
/// UI would be telemetry breaking the app it describes.
/// </summary>
public static class TelemetrySender
{
    /// <summary>
    /// **The endpoint — empty until the backend is deployed.** <c>eqbuddy-telemetry</c>
    /// (DRA-361) is merged but not running anywhere, so there is no host to name yet, and a
    /// guessed one could be a host somebody else owns. Empty means every send answers
    /// <see cref="Outcome.NotConfigured"/> without opening a socket, and the first-open prompt
    /// is not shown (its one showing is not spent on a build that cannot send).
    /// TODO(DRA-369): set this to the deployed host, e.g. <c>https://host.example</c>, no
    /// trailing slash.
    /// </summary>
    public const string BaseUrl = "";

    public const string HeartbeatPath = "/heartbeat";
    public const string DeletePath = "/delete";

    /// <summary>Is there anywhere to send to?</summary>
    public static bool IsConfigured => BaseUrl.Length > 0;

    /// <summary>§3: a short timeout. A send never blocks the UI or delays exit.</summary>
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    /// <summary>How a request ended. <see cref="Refused"/> is a <c>400</c> — the payload and
    /// the requirement page disagree, a bug and never a network fault, so the log says so.
    /// </summary>
    public enum Outcome { Ok, Failed, Refused, NotConfigured }

    /// <summary>Everything but <see cref="Outcome.Ok"/> is a failure to the schedule.</summary>
    public readonly record struct Result(Outcome Outcome, string Detail)
    {
        public bool Ok => Outcome == Outcome.Ok;
    }

    public static Task<Result> PostHeartbeatAsync(string json) => PostAsync(HeartbeatPath, json);

    public static Task<Result> PostDeleteAsync(string json) => PostAsync(DeletePath, json);

    private static async Task<Result> PostAsync(string path, string json)
    {
        if (!IsConfigured) return new Result(Outcome.NotConfigured, "no endpoint is configured");
        try
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await Http.PostAsync(BaseUrl + path, content).ConfigureAwait(false);
            return response.StatusCode switch
            {
                HttpStatusCode.NoContent => new Result(Outcome.Ok, "204"),
                HttpStatusCode.BadRequest => new Result(Outcome.Refused, "400"),
                var code => new Result(Outcome.Failed, ((int)code).ToString()),
            };
        }
        catch (Exception ex)
        {
            // Unreachable, timed out, TLS — all one bucket to the schedule, and the type name
            // is enough for error.log: the message can carry a host name and nothing more is
            // needed to tell a timeout from a refused connection.
            return new Result(Outcome.Failed, ex.GetType().Name);
        }
    }
}
