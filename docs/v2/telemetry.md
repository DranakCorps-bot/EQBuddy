# Evolved opt-in telemetry — the requirement page

> **NOTHING ON THIS PAGE HAS SHIPPED.** This is the requirement, written
> before the code so that the code is written to the page and not the
> other way round. NFR-PRIV-002 (`AdditionalRequirements.md` §18.3) asks
> for exactly this: *"Telemetry, if ever added, must be opt-in and
> separately documented."* Until TEL-PR3 lands, EQBuddy sends no telemetry
> of any kind. Until TEL-PR4 lands with the launch release, `README.md` and
> `SECURITY.md` keep saying so. The drafts in §8 are **UNSHIPPED** copy
> and must not be pasted anywhere public before then.

**Card:** DRA-360 (TEL-PR1), umbrella DRA-336, parent DRA-3.
**Plan of record:** [`docs/plans/DRA-336.md`](../plans/DRA-336.md), Helm-signed
2026-09-23 ([PR #856](https://github.com/DranakCorps-bot/EQBuddy/pull/856#issuecomment-5804558295)),
which amends the signed Fable TEL plan (PR #320, archived in
`docs/ops/claude-archive/channels/2026-Q3/FABLE.md` under "Evolved opt-in
telemetry"). **You do not need the archive to implement TEL-PR2 or TEL-PR3.**
Everything it decides is restated here. Where this page goes further than
the plan (the wire contract in §5, the database shape in §6, the edges in
§7), it says so, and the plan wins any disagreement.

**Scope:** EQBuddy Evolved (v2) only. v1 and the LEGACY tree never get
telemetry. [`LEGACY-V1.md`](../../LEGACY-V1.md)'s *"Nothing expires, phones
home, or switches itself off"* stays true forever.

**What already permits this, and what it must not break.** The charter
([`EQBuddy-v2-Project-Guide-Requirements.md`](EQBuddy-v2-Project-Guide-Requirements.md)
§2.2) says *"no telemetry by default"*, not "never". `AdditionalRequirements.md`
§18.3 carries the privacy rows this page answers: NFR-PRIV-002 (opt-in and
separately documented: this page), NFR-PRIV-001 (*"No session data may be
uploaded automatically"*) and NFR-PRIV-004 (no local paths or character names
off-machine). The last two hold **by construction**: TEL-002's three fields
cannot carry a session, a path or a name, and the key-set guard (§9) is what
keeps it that way. This is the **first feature in the product's history that
sends anything off the player's machine**, so every rule below is written as
if a reader with a network monitor will check it.

---

## §1 The requirement (TEL-001 … TEL-006)

TEL-002 … TEL-006 are the signed text, verbatim. TEL-001 is the signed text
with the one clause the Founder's 2026-09-22 AUTHORIZE replaced, marked in
place.

- **TEL-001 — Consent** *(as amended by DRA-336 §1)*. Telemetry is OFF on
  every install, forever, until the player turns it on. ~~No first-run
  prompt,~~ **The first app open prompts, once, asking the player to help
  improve EQBuddy; its default action (Esc, ✕, "Not now") is decline, and
  decline is final: no re-prompt on update, ever, and the settings toggle is
  the only way back in.** No nag, no dark pattern. The prompt and the toggle
  both carry the entire payload in their own copy ("here is everything it
  sends"). Turning it OFF stops sends **and destroys the local install id**.
  Re-enabling mints a fresh one, so opting out is also an identity reset.
  Trap 47 binds: **one policy module decides consent + cadence, and every
  send path goes through it.** Never two code paths deciding an off-machine
  question. The periodic timer's epoch is "time of opt-in", never
  `DateTime.MinValue`.
- **TEL-002 — Payload.** Exactly three fields: `installId` (random GUID
  minted at opt-in, never derived from hardware, user name, or paths),
  `appVersion`, `os` (coarse platform + version string). **The field list is
  a curated must-list with a guard** (trap 34's pair): a unit test asserts
  the serialized key set equals exactly this list, so adding a fourth field
  fails the build until this plan is amended and re-signed. No logs, no
  character or chat data, no file paths, no EQ account data, no hardware ids,
  no locale, no geo.
- **TEL-003 — Cadence, and what "concurrent" means.** One heartbeat shortly
  after launch (~2-minute dwell, so a crash-loop never spams), then every 5
  minutes while running. Nothing on exit. Server-side: **concurrent** =
  distinct ids in the last 10 minutes; **peak concurrent** = max over
  10-minute buckets; **unique users** = distinct ids trailing 30 days;
  **version mix** = share by `appVersion` among trailing-7-day uniques. These
  definitions are part of the requirement because they are what makes the
  public numbers honest. Publish them beside the numbers.
- **TEL-004 — Backend, retention, delete.** Our own backend, no third-party
  analytics. Raw heartbeats retained **90 days** then deleted by scheduled
  job; aggregates (counts only, no ids) kept indefinitely. **IPs are never
  persisted or logged.** Transport sees them, storage never does; say so in
  the docs. Delete path: an in-app "Delete my telemetry data" action posts the
  install id to a delete endpoint; the backend hard-deletes every raw row for
  that id. (Aggregates need no scrub. They contain no ids.)
- **TEL-005 — Public metrics.** README/repo-main shows: unique users,
  concurrent now, peak concurrent, version mix, fed live from the backend's
  public `metrics.json` (badges/fetch), not by a bot editing the README on a
  clock. GitHub download totals may appear **separately, labeled**: the
  shipped wording must say downloads count fetches, not people, and point at
  the uniques number as the honest one.
- **TEL-006 — Scope freeze.** Heartbeat only. No crash reporting, no
  feature-usage events, no session stats, no error strings, ever, under this
  plan. Any of those is a NEW plan with its own Helm last-look and its own
  David decision, and TEL-002's guard is what makes that structural rather
  than aspirational.

**What this plan does not authorize** (DRA-336 §5, and the Helm SIGN's
standing locks): on-by-default, in any build or channel. A payload field
beyond TEL-002's three. Crash or usage events. Play Console work. A paid
backend tier (a money decision for David, asked when it is real). Telemetry
in v1 / legacy.

## §2 The payload

The complete body of every heartbeat. There is no other request body.

```json
{
  "installId": "3f2b8c1e-9a47-4d2e-b0c6-5e81a7d4f920",
  "appVersion": "2.0.0",
  "os": "Windows 10.0.26200"
}
```

| Key | Where the client gets it | Server accepts |
|---|---|---|
| `installId` | `Guid.NewGuid()` at the moment of opt-in, stored in `AppSettings.TelemetryInstallId`. Lowercase `D` format (36 chars, hyphenated). | A lowercase hyphenated GUID and nothing else. |
| `appVersion` | The `<Version>` from `Directory.Build.props`, with no `+`build metadata. | 1–32 chars of `[0-9A-Za-z.\-]`. |
| `os` | Platform name + `Environment.OSVersion.Version` (`Major.Minor.Build`). Coarse: no edition, no revision, no locale. | 1–64 printable ASCII chars. |

**The key set is closed on BOTH ends.** The client's guard (TEL-PR3) asserts
the serialized keys are exactly these three. The server (TEL-PR2) rejects a
body with any other key, any missing key, or any value it would not accept
above, with `400`, and stores nothing. A body that is refused is not logged.

**What is on the wire besides the body:** HTTPS to one host (§5), and
`Content-Type: application/json`. The client adds no cookies, no auth
header, and no custom header. It sets no identifying `User-Agent`
(.NET's `HttpClient` sends none by default, so it should stay that way).
The source IP address is visible to the transport and is never stored (§6).

## §3 Cadence

| Event | What the client does |
|---|---|
| App launch, telemetry **off** | Nothing. No timer is armed and no socket is opened. |
| App launch, telemetry **on** | First heartbeat ~2 minutes after launch, then every 5 minutes. |
| Player opts in mid-session | The epoch is the moment of opt-in: first heartbeat ~2 minutes later, then every 5 minutes. |
| Player opts out | Pending timer cancelled; no further send in this process. The install id is cleared in the same settings write. |
| A send fails (network, 5xx, timeout) | Logged to `error.log` only. **The failed heartbeat is not retried, not queued, not caught up.** A heartbeat means "running now", so a late one is a wrong one. The signed plan's *"bounded backoff"* applies to the **next** tick instead: each consecutive failure doubles the interval (5 → 10 → 20 → 40 min), capped at **60 minutes**; the first success resets it to 5. So a dead endpoint costs one request an hour, not twelve. |
| `429` from the server | Same as a failure, backoff included. The 5-minute cadence is already below any sane limit, so a 429 means the clock is wrong somewhere, and slowing down is the right answer. |
| `400` from the server | Same as a failure, and the log line says `refused`, not `unreachable`. A 400 means the payload and this page disagree: a bug, never a network fault. |
| App exit | Nothing. No goodbye ping. |

Sends are fire-and-forget off the UI thread with a short timeout (10 s is
the default this page sets), and they never block the UI or delay exit. The
backoff state lives in memory only. A relaunch starts at the ordinary ~2-minute
dwell, which is already the crash-loop protection, so nothing new is persisted.

**Server-side definitions** (TEL-003, restated as the arithmetic TEL-PR2
implements). A bucket is a 10-minute UTC window aligned to `:00`, `:10`, …

| Public number | Definition |
|---|---|
| **Concurrent now** | Distinct `installId` in the last 10 minutes, as of the most recent rollup. |
| **Peak concurrent** | Max, over every closed 10-minute bucket since the backend went live, of the distinct ids in that bucket. Published with the bucket's start time. |
| **Unique users** | Distinct `installId` with any heartbeat in the trailing 30 days. |
| **Version mix** | Among distinct ids in the trailing 7 days, the share on each `appVersion` (an id's latest version in the window). Published with that 7-day denominator. |

Every id is an *install* that opted in, not a person. The published
definitions say that too. An opt-out-then-in mints a new id, so it can count
twice inside a window, and that is the price of the identity reset.

## §4 Retention

| Data | Kept | Deleted by |
|---|---|---|
| Raw heartbeat rows (`installId`, bucket, version, os) | **90 days** from the bucket | Scheduled purge job, daily |
| Raw rows for one id | Until the player deletes them, or 90 days | `POST /delete` (§5): hard delete, immediately |
| Bucket aggregates (bucket start, distinct-id count) | Indefinitely | Nothing. They contain no id. |
| Daily aggregates (uniques, version mix counts) | Indefinitely | Nothing. They contain no id. |
| Source IP address | **Never stored.** Not in a table, not in a log. | n/a |
| Request logs of any kind (platform or code) | **None enabled** | n/a. TEL-PR2 turns platform request logging off and says where in its README. |
| On the player's PC: `TelemetryInstallId` | While telemetry is on | Cleared by opt-out and by a successful delete |

## §5 Wire contract (for TEL-PR2 and TEL-PR3)

> **This section is this page's decision, not the signed plan's.** The plan
> fixes the payload, the retention, and the existence of a delete endpoint.
> The paths, status codes and response shapes below are the defaults
> TEL-PR2 builds against. TEL-PR2 may change them, but must amend this page
> in the same change, because TEL-PR3 builds against the page.

Backend: its own **public** repo, recommended `DranakCorps-bot/eqbuddy-telemetry`,
recommended stack Cloudflare Worker + D1, free tier. The contract is TEL-004,
not the vendor, and TEL-PR2 may substitute an equivalent if reality disagrees.
The host is not chosen yet. TEL-PR2 names it, and it is the one endpoint
literal the client carries (§9, the endpoint scanner).

**Why a separate PUBLIC repo** (signed plan): the payload claim becomes
checkable the same way the client's is, because a player can read exactly what
the endpoint stores. Out-of-tree also keeps the log-only app's source free of
any server code.

**The money door.** Free tier only. The moment the backend needs a paid plan,
that is a David decision (consequence item 4). TEL-PR2 stops and asks then; it
does not upgrade the plan to keep a number live.

**Two hypotheses, labelled, that TEL-PR2 and TEL-PR4 check before relying on
them** (both from the signed plan, neither measured):

1. The Cloudflare free tier covers heartbeats at any plausible player count.
   Believed from published limits, not load-tested. At 5-minute cadence one
   install is ~288 writes a day, so TEL-PR2 writes down the install count at
   which the free-tier write limit binds, from the vendor's own published
   numbers on the day, in its README.
2. shields.io endpoint badges cache acceptably for "concurrent now". If their
   cache is too coarse, the README shows the slow-moving numbers (trailing
   uniques, peak) and "concurrent now" lives on a linked page instead.
   TEL-PR4's call.

| Request | Body | Responses |
|---|---|---|
| `POST /heartbeat` | Exactly the §2 payload | `204` recorded. `400` malformed or wrong key set (nothing stored). `429` more than one heartbeat for this id in 60 s (nothing stored). |
| `POST /delete` | `{"installId": "<guid>"}`, exactly that one key | `204` every raw row for the id is gone (also when there were none: the call is idempotent and says nothing about whether the id existed). `400` malformed. |
| `GET /metrics.json` | n/a | `200`, public, cacheable for up to 10 minutes. Shape below. |

A heartbeat **upserts** one row per `(installId, bucketStart)`, so a
heartbeat every 5 minutes gives two writes against one row per bucket, and
the raw table's row count is bounded by ids × buckets.

`metrics.json`:

```json
{
  "schema": 1,
  "generatedAt": "2026-10-01T18:40:00Z",
  "concurrentNow": 12,
  "peakConcurrent": 31,
  "peakConcurrentBucket": "2026-09-28T02:10:00Z",
  "uniqueUsers30d": 140,
  "versionMix7d": {
    "denominator": 96,
    "versions": [
      { "appVersion": "2.0.1", "count": 80, "share": 0.833 },
      { "appVersion": "2.0.0", "count": 16, "share": 0.167 }
    ]
  },
  "definitions": {
    "concurrentNow": "Distinct opted-in installs that sent a heartbeat in the last 10 minutes.",
    "peakConcurrent": "The most distinct opted-in installs in any single 10-minute window.",
    "uniqueUsers30d": "Distinct opted-in installs in the last 30 days. An install, not a person; telemetry is off unless the player turns it on.",
    "versionMix7d": "Share of the last 7 days' distinct opted-in installs on each version."
  }
}
```

The `definitions` block is the TEL-003 rule *"publish them beside the
numbers"* made machine-readable, so a badge or page can print the sentence
it was given rather than write its own. **The landing site already has a
consumer waiting:** `site/metrics.json` carries `maxConcurrentUsers: null`,
and the hero tile paints "Telemetry not live yet" until this backend
publishes a figure (`LandingSourceClaimsTests.AFabricatedConcurrentIntegerIsRefused`).
Wiring `peakConcurrent` into that tile is TEL-PR4's, and it re-keys that
test in the same change.

## §6 Storage (for TEL-PR2)

The smallest shape that yields all four public numbers. There is no column
that could hold an IP address, a path or a name, which is the point.

```sql
-- raw, 90-day retention, the only table holding an id
CREATE TABLE heartbeat (
  install_id   TEXT NOT NULL,   -- the §2 GUID
  bucket_start TEXT NOT NULL,   -- ISO-8601 UTC, 10-minute aligned
  app_version  TEXT NOT NULL,
  os           TEXT NOT NULL,
  PRIMARY KEY (install_id, bucket_start)
);

-- aggregates, kept indefinitely, no ids
CREATE TABLE bucket_count (bucket_start TEXT PRIMARY KEY, distinct_ids INTEGER NOT NULL);
CREATE TABLE daily_rollup (day TEXT PRIMARY KEY, unique_30d INTEGER NOT NULL,
                           version_mix_7d TEXT NOT NULL);  -- JSON of the §5 versions array
```

Cron every 10 minutes: close the previous bucket into `bucket_count`,
recompute `metrics.json`. Daily: write `daily_rollup`, then purge
`heartbeat` rows whose `bucket_start` is older than 90 days. `/delete`
touches `heartbeat` only.

**Tests TEL-PR2 carries** (plan §3 done bar): delete removes every row for
the id and no other id's rows; the purge removes exactly the rows past 90
days; each of the four numbers is computed correctly against a fixture with
known answers, including an id that spans two buckets and an id that
changed version; `/heartbeat` refuses a fourth key, a missing key, and a
non-GUID id, and stores nothing on refusal; nothing in the repo's code or
config logs a request's IP. The repo is public, carries no secrets, and its
README says where platform logging is switched off.

## §7 Client (for TEL-PR3)

**Settings** (`Core/AppSettings.cs`). Three new keys, each with a
`DeadSettingTests` row. **No migration exists and none is allowed**: every key
defaults to "never happened", so there is nothing to migrate and trap 55
cannot arise. **No dark-launch flag either**: under E-1's local-only phase the
only installs that exist are the Founder's, so TEL-PR3 ships the client live
and the endpoint's only traffic is his.

| Key | Type | Default | Written by |
|---|---|---|---|
| `TelemetryEnabled` | `bool` | `false` | The prompt's accept, the settings toggle, and a successful delete (→ `false`) |
| `TelemetryInstallId` | `string?` | `null` | Opt-in (mint). Cleared by opt-out and by a successful delete |
| `TelemetryPromptShown` | `bool` | `false` | The prompt, **at the moment it is shown**, not when it is answered |

**One policy module, `UI.Shared/TelemetryHeartbeat.cs`**, pure and
framework-free with no network: consent state, the id lifecycle, the cadence
and epoch, whether the prompt should show, and the payload builder. One thin
sender does the HTTP, and it is the only code that holds the endpoint
literal. Every send path goes through the policy (trap 47).

**The first-open prompt:**

- Shows when `TelemetryPromptShown` is `false`. The flag is set when the
  prompt is SHOWN, so a prompt closed by killing the app counts as a
  decline. The default action is decline, and a crash must not become a nag.
- **Every existing profile sees it once too.** The first Evolved build that
  carries telemetry has never set the flag on any profile, so an upgrading
  player gets the prompt once, on that build, and never again. That is the
  "once per install" rule working, not a re-prompt on update.
- Decline writes `TelemetryPromptShown = true` and nothing else.
- Accept writes the flag, `TelemetryEnabled = true`, and a fresh
  `TelemetryInstallId`, in one `AppSettings.Save`. No new writer is needed
  (trap 13).
- It must not show under an automated launch. E2E and `shoot.ps1` run
  against isolated profiles (`IsolatedLaunchPolicy`), and a modal prompt
  there would hang the harness. TEL-PR3 decides how (seed the flag in the
  fixture, or gate on the policy) and dumps `telemetryPrompt=` so the E2E can
  assert it.
- The copy is TEL-A's, verbatim (§8.3).

**The settings surface** is whichever one exists when TEL-PR3 is kicked. Today
that is the **Behavior** block (`EQBuddy/SettingsBehaviorView.cs`), which both
`OptionsWindow` and the shell's `SettingsRoom` compose, so one view reaches both
hosts. TEL-PR3 takes the surface of the day; it does not wait on a settings
redesign. It must carry:

- A toggle whose copy carries the whole payload.
- A "last heartbeat" status line. It has a fixed shape, because a
  clock-driven string may not move a measured width (trap 12), and it shows
  "never sent" until the first success, so a player who opted in can see it
  working.
- A "Delete my telemetry data" action. It is **enabled only while telemetry
  is on**, because that is the only time an id exists to send. On `204` it
  sets `TelemetryEnabled = false` and clears the id. On failure nothing
  changes, and the status line says the delete did not reach the server, so
  the player can retry. It does not destroy the id until the server has
  confirmed.

> **An edge TEL-A's copy has to answer** (flagged for Bevel, DRA-359):
> opting OUT destroys the local id without sending anything, so after a
> plain opt-out the player's past rows can no longer be deleted on request.
> They age out within 90 days (§4). The honest copy says so beside the
> toggle, or points at Delete first. Sending a delete automatically on
> opt-out would be an off-machine send the player did not ask for, so it is
> not this page's default.

## §8 Draft copy — UNSHIPPED

> **UNSHIPPED. Do not paste any of this into a public file before TEL-PR4**,
> which moves it with the launch release David gates and Helm signs (a
> public promise under the project's name). Until then README.md and
> SECURITY.md stay exactly as they are, and they stay TRUE, because nothing
> sends.

### §8.1 README.md — the principle paragraph (line 44 today)

Replaces only the paragraph's first two sentences. The rest of the paragraph
stands.

> **Your own files, by principle. No telemetry unless you turn it on, always
> contribution.** EQBuddy never reads game memory and never measures other
> players. It knows only what the game writes for you on your own PC: the
> `/log` it tails, and the `/outputfile` dumps you ask the game for. It sends
> nothing about you or your play anywhere. The one exception is an opt-in
> heartbeat, **off until you say yes**, that carries exactly three things: a
> random install id, the app version, and your Windows version
> ([what it sends, and how to delete it](docs/Telemetry.md)).

**"Never phones home" is removed on purpose.** An opted-in heartbeat is a
phone home, and a principle sentence a reader can falsify with a network
monitor is worse than no sentence.

### §8.2 README.md — EQBuddy Mobile's privacy bullet (line 540 today)

The sentence is scoped to Mobile and stays true of Mobile, since the phone
pages send nothing. But its last clause, *"there is no server anywhere for
it to leave to"*, stops being true of EQBuddy once the heartbeat backend
exists. Draft:

> - **Your network only.** EQBuddy serves the pages straight to your device
>   over your LAN. No account, no cloud, no telemetry: nothing on the phone
>   pages ever leaves your network.

### §8.3 The first-open prompt — AWAITING BEVEL (TEL-A, DRA-359)

**Not written yet, on purpose.** The Founder named the order: Bevel writes
this copy before the client PR exists. Bevel's TEL-A copy lands in
`BEVEL.md` and is folded in here verbatim. The same goes for the settings
toggle, the status line and the delete action.

**Condition C-1 binds this PR's merge** (DRA-358 walk, Helm SIGN
2026-09-23): the consent copy in this section gets **one human read beyond
its author, by the Founder or Helm, RECORDED ON THIS PULL REQUEST** before it
merges. This PR does not merge with this section empty.

What the copy must do, from the signed requirement. These are constraints on
the copy, not the copy itself:

- Show the entire payload: the three fields, named in plain words.
- Give both buttons equal visual weight. Esc, ✕ and "Not now" decline.
- Link this page's player-facing twin (`docs/Telemetry.md`, TEL-PR4).
- Promise nothing the §4 retention and §7 delete edges do not deliver.
- No guilt, no "help us survive", no pre-ticked anything, no second ask.

### §8.4 SECURITY.md — the egress rule (line 20 today)

> EQBuddy's rule is **local-first, no telemetry unless you turn it on**: it
> never sends your data anywhere on its own. The complete list of hosts the
> app itself contacts:

Plus one new row in that table. Its host is TEL-PR2's to name:

> | `<telemetry host>` | Only if you turned telemetry on (it is off on every install until you do): ~2 minutes after launch, then every 5 minutes; and once when you press "Delete my telemetry data" | The three-field heartbeat: a random install id, the app version, your Windows version. Nothing else, ever. IPs are never stored. See [Telemetry](#telemetry-off-unless-you-turn-it-on). |

**The guard this sentence must keep passing:** `LandingSourceClaimsTests`
arm (d) (`SecurityBoundary`) requires SECURITY.md to match `zero telemetry|no
telemetry` AND `never sends your data|sends nothing about you`. The draft
keeps both. A rewording at TEL-PR4 that loses either one reddens the build,
and that is the guard doing its job, not an obstacle to edit around.

### §8.5 SECURITY.md — the "Zero telemetry" section (line 101 today)

Heading and body replaced. Nothing links to the `#zero-telemetry` anchor
today (grepped 2026-09-23), so the rename breaks no link.

> ## Telemetry: off unless you turn it on
>
> There is no analytics SDK, no crash reporter, no usage events, no
> "anonymous statistics". Errors go to a local file
> (`%AppData%\EQBuddy\error.log`), full stop.
>
> EQBuddy Evolved has **one** optional exception, and it is **off on every
> install until you say yes**. The first time you open it, EQBuddy asks once;
> "Not now" is final, and Options is the only way back in. If you turn it
> on, it sends a heartbeat of exactly three fields: a random install id
> minted when you opted in, the app version, and your Windows version. No
> logs, no character or chat data, no file paths, no account data, no
> hardware ids, no locale, no location. Turning it off destroys the install
> id. "Delete my telemetry data" erases every stored heartbeat for it. The
> backend is [public](https://github.com/DranakCorps-bot/eqbuddy-telemetry),
> keeps raw heartbeats for 90 days, and never stores your IP address.
> EQBuddy 1.x and the legacy builds never send anything.
>
> When knowledge moves between players it moves because a player chose to
> move it: share strings you paste to a friend, the ✦ Copy-for-wiki button
> that fills your clipboard, feedback drafts you post yourself. If you ever
> catch EQBuddy sending something this page doesn't list, **or sending the
> heartbeat while telemetry is off**, that is a vulnerability — report it as
> one.

The self-enforcing last sentence is the best thing on the page, and it
stays. It gains teeth rather than losing them: the heartbeat is listed, so
anything else is still a vulnerability, and so is the listed thing
happening without consent.

**LEGACY-V1.md:60 is not touched, at TEL-PR4 or ever.**

## §9 Guards (TEL-PR3), each prove-failed on a mutated tree, then green ×8

| Guard | Asserts | The mutation that must redden it |
|---|---|---|
| Payload key set | The serialized heartbeat's keys equal exactly `installId`, `appVersion`, `os` | Add a fourth property to the payload record |
| Endpoint scanner | The endpoint literal appears in exactly one source file, and no second `HttpClient` reaches it | Copy the literal into a second file |
| E2E OFF fact | The default profile, launched as the real exe, dumps `telemetry=off sends=0`, and after a declined prompt still does | Default `TelemetryEnabled` to `true` |
| Prompt fires once | The prompt shows on a profile with the flag unset, then never again: not on relaunch, not after decline, not after a version bump | Set the flag on answer instead of on show, or clear it on update |

Plus `DeadSettingTests` rows for the three keys, and the settings-surface
shot in both states. **Must-list rows this plan creates:** the TEL-002 key-set
list (new, curated, reasoned) and the `DeadSettingTests` rows. No existing
`GameCommandsTests` or `ImportReportReachesASurfaceTests` row is touched, and
a TEL-PR3 diff that touches one has left its lane. The fixture has no network, so the staged state is
OFF + "never sent", predicted before shooting (trap 23).

## §10 The sequence (DRA-336 §2)

| Step | Card | Blocked by | What |
|---|---|---|---|
| TEL-A | DRA-359 (Bevel) | — | Consent copy for the prompt, toggle, status line and delete, as text in `BEVEL.md` |
| **TEL-PR1** | **DRA-360** | Helm SIGN (landed 2026-09-23) | **This page.** Merges only with §8.3 filled from TEL-A and C-1's read recorded on the PR |
| TEL-PR2 | DRA-361 | Helm SIGN | The backend repo, per §5–§6 |
| TEL-PR3 | DRA-362 | TEL-PR1, TEL-PR2, TEL-A | The client, per §2–§3 and §7, with the §9 guards |
| TEL-PR4 | DRA-363 | TEL-PR3, the launch release | §8's drafts go live, plus `docs/Telemetry.md`, the README metrics block with a separately labelled downloads row, and `WhatsNew.json`. Helm signs the copy |

**TEL-PR4's tri-read** (signed plan §3 done bar): README, SECURITY.md and
`LEGACY-V1.md` are read together at the flip, so the global change does not
falsify a scoped sentence (README's Mobile bullet, §8.2) and the legacy
promise stays literally true. **Why TEL-PR4 waits for the release and the rest
does not:** public metrics before there is a public channel would be a
dashboard of one machine. The sequencing is the honesty.

**TEL is its own lane**, disjoint by construction from the shell and nav work:
a new `UI.Shared` policy file, a thin sender, settings rows, docs, and a repo
that is not this one. No `MainWindow`, no `ShellWindow`, no `*Room.cs` beyond
the settings surface of the day.

## §11 Decided on this page, without asking

Logged here rather than restated in `DECISIONS.md`. Each one could have gone
the other way, and each is reversible before TEL-PR3 lands.

- **A failed heartbeat is dropped, not retried or queued.** It could have
  been queued. A late heartbeat claims "running now" at a time it was not.
- **The signed plan's "bounded backoff" slows the NEXT tick** (doubling, cap
  60 min, reset on success, in memory only) rather than retrying the failed
  one. It could have been read as a retry schedule for the lost heartbeat,
  which would contradict the rule above. The plan wins any disagreement, and
  this reading honours both of its sentences.
- **The prompt flag is set on SHOW.** It could have been set on answer. A
  kill during the prompt would then re-prompt, and that is a nag by
  accident.
- **Delete is offered only while on, and destroys the id only on a
  confirmed `204`.** Offering it while off would need the id kept after
  opt-out, which TEL-001 forbids.
- **Opt-out does not auto-send a delete.** It could have. That is an
  off-machine send nobody asked for. Flagged to TEL-A instead (§7).
- **`/delete` answers `204` whether or not the id existed.** It could have
  answered `404`. An existence oracle for ids is a thing nobody needs.
- **One raw row per id per 10-minute bucket (upsert).** It could have been
  one row per heartbeat. The bucket is all TEL-003's arithmetic reads, and
  it keeps the table small.
- **"Never phones home" leaves the README principle line** (§8.1). It could
  have been kept as "never phones home without asking". A principle a
  network monitor can falsify is not one to keep.
