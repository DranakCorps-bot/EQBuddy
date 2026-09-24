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
| **Concurrent now** | Distinct `installId` whose latest heartbeat was received (`last_seen_ms`, §6) in the rolling 10 minutes up to the moment the snapshot is written. Recomputed live every 10 minutes. |
| **Peak concurrent** | Max, over every closed 10-minute bucket since the backend went live, of the distinct ids in that bucket. Published with the bucket's start time. Recomputed live every 10 minutes. |
| **Unique users** | Distinct `installId` with any heartbeat in the 30 days up to the end of the last complete UTC day. **Refreshed DAILY from `daily_rollup`, not by a live scan** (see below). |
| **Version mix** | Among distinct ids in the 7 days up to the end of the last complete UTC day, the share on each `appVersion` (an id's latest version in the window). Published with that 7-day denominator. **Refreshed DAILY from `daily_rollup`, not by a live scan.** |

**Why the two trailing numbers are daily** (amended from TEL-PR2, DRA-361):
a live 30-day distinct count reads every raw row in 30 days, and running it on
every 10-minute cron (144 times a day) spends D1's free rows-read allowance on
a number that barely moves. The keys and shape in §5 are unchanged; only the
two `definitions` sentences say *"up to the end of the last complete UTC day"*.
Both read `0` until the backend's first UTC day completes.

Every id is an *install* that opted in, not a person. The published
definitions say that too. An opt-out-then-in mints a new id, so it can count
twice inside a window, and that is the price of the identity reset.

## §4 Retention

| Data | Kept | Deleted by |
|---|---|---|
| Raw heartbeat rows (`installId`, bucket, version, os, `last_seen_ms`) | **90 days** from the bucket | Scheduled purge, on every 10-minute cron run |
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
| `POST /heartbeat` | Exactly the §2 payload | `204` recorded. `400` malformed or wrong key set (nothing stored). `429` a second heartbeat for this id less than 60 s after the last one (nothing stored). |
| `POST /delete` | `{"installId": "<guid>"}`, exactly that one key | `204` every raw row for the id is gone (also when there were none: the call is idempotent and says nothing about whether the id existed). `400` malformed. |
| `GET` / `HEAD /metrics.json` | n/a | `200`, public (CORS-open), cacheable for up to 10 minutes (`max-age=600`). Shape below. |
| Any other method on a known path | n/a | `405` with an `Allow` header. |
| Any other path | n/a | `404`. |

**The rate-limit boundary, exactly** (amended from TEL-PR2, DRA-361): the
server compares the new beat's receive time with the id's `last_seen_ms`. A
beat **60 000 ms** after the last one is **accepted**; a beat at **59 999 ms**
is **refused** with `429`. TEL-PR2 tests both edges. The client's 5-minute
cadence sits far above it, so only a wrong clock or a duplicate sender meets it.

Request bodies over **1 KiB** are refused unread; the §2 payload is well under
that.

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
    "uniqueUsers30d": "Distinct opted-in installs in the 30 days up to the end of the last complete UTC day. An install, not a person; telemetry is off unless the player turns it on.",
    "versionMix7d": "Share of the distinct opted-in installs in the 7 days up to the end of the last complete UTC day on each version (each install counted once, on its latest version)."
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
  last_seen_ms INTEGER NOT NULL, -- server receive time of the latest beat in this
                                 -- bucket (epoch ms): the 60 s rate limit and the
                                 -- rolling "concurrent now" window read it
  PRIMARY KEY (install_id, bucket_start)
);
CREATE INDEX heartbeat_bucket ON heartbeat (bucket_start);
-- deliberately no index on last_seen_ms: it changes on every beat, and D1
-- counts each index entry touched as a row written

-- aggregates, kept indefinitely, no ids
CREATE TABLE bucket_count (bucket_start TEXT PRIMARY KEY, distinct_ids INTEGER NOT NULL);
CREATE TABLE daily_rollup (day TEXT PRIMARY KEY,     -- YYYY-MM-DD UTC, as of the day's end
                           unique_30d INTEGER NOT NULL,
                           version_mix_7d TEXT NOT NULL);  -- JSON of the §5 versionMix7d object
-- the published metrics.json, one row, no ids
CREATE TABLE metrics_snapshot (id INTEGER PRIMARY KEY CHECK (id = 1),
                               generated_at TEXT NOT NULL, body TEXT NOT NULL);
```

`last_seen_ms` is the **fifth `heartbeat` column** (amended from TEL-PR2,
DRA-361). It holds a time, not an identity: the bucket alone is too coarse for
a 60-second limit or a rolling 10-minute window. It is purged with its row.
TEL-PR2's schema-pin test names every column of every table, so a sixth
column fails its build until this page is amended again.

Cron every 10 minutes: close the previous bucket into `bucket_count`, write
`daily_rollup` for any UTC day that has completed since the last run
(catching up missed days), purge `heartbeat` rows whose `bucket_start` is
older than 90 days, then rewrite `metrics_snapshot`: `concurrentNow` and
`peakConcurrent` live, `uniqueUsers30d` and `versionMix7d` copied from the
latest `daily_rollup`. `/delete` touches `heartbeat` only.

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
  *"On — no heartbeats sent yet"* until the first success, so a player who
  opted in can see it working. The closed set of strings is §8.3 §D, as
  corrected by §8.3.1 rows 8–12. The line reserves the width of the longest
  string so that ticking cannot resize its row.
- A "Delete my telemetry data" action. It is **enabled only while telemetry
  is on**, because that is the only time an id exists to send. On `204` it
  sets `TelemetryEnabled = false` and clears the id. On failure nothing
  changes, and the status line says the delete did not reach the server, so
  the player can retry. It does not destroy the id until the server has
  confirmed.

> **An edge TEL-A's copy had to answer, and does** (DRA-359): opting OUT
> destroys the local id without sending anything, so after a plain opt-out the
> player's past rows can no longer be deleted on request. They age out within
> 90 days (§4). Sending a delete automatically on opt-out would be an
> off-machine send the player did not ask for, so it is not this page's
> default. Bevel's §B *"What turning it OFF does NOT do"* says this beside the
> toggle and points at Delete first. The two OFF-state sentences near it that
> contradicted it are amended by C-1 / Helm (§8.3.1 rows 2–3).

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

### §8.3 The consent copy — TEL-A (DRA-359), folded verbatim

**Source:** Bevel's TEL-A delivery, `BEVEL.md` entry *"2026-09-23 — DELIVERED:
consent copy for Evolved opt-in telemetry (DRA-359 TEL-A)"*, commit `6611cb61`
on branch `dra-359-tel-a-consent-copy`. Everything between the two rules below is
Bevel's text **word for word**, with two kinds of edit: its `###` headings are
demoted to `####` to sit under this section, and the sentences C-1 ruled FALSE,
incomplete or missing are **amended by C-1 / Helm**. Every amended sentence
carries the marker *(C-1 / Helm, row N)*, naming its §8.3.1 row, so no word
Helm wrote reads as Bevel's. Bevel's note on the word "heartbeat" versus
"telemetry", its layout judgment call and its delivery note are kept because
the C-1 reader is the one they are addressed to.

**Condition C-1 binds this PR's merge** (DRA-358 walk, Helm SIGN
2026-09-23): the consent copy in this section gets **one human read beyond
its author, by the Founder or Helm, RECORDED ON THIS PULL REQUEST** before it
merges. **C-1 READ RECORDED by Helm, 2026-09-24**
([#858 comment](https://github.com/DranakCorps-bot/EQBuddy/pull/858#issuecomment-5808133793)),
at head `51864fa8`. Its rulings are §8.3.1's Status column, and the amendments
below carry its binding text.

What the copy must do, from the signed requirement. These were the constraints
TEL-A was written to; §8.3.1 checks the copy against them:

- Show the entire payload: the three fields, named in plain words.
- Give both buttons equal visual weight. Esc, ✕ and "Not now" decline.
- Link this page's player-facing twin (`docs/Telemetry.md`, TEL-PR4).
- Promise nothing the §4 retention and §7 delete edges do not deliver.
- No guilt, no "help us survive", no pre-ticked anything, no second ask.

---

#### A. The first-open prompt — shown once, decline final, Esc is decline

Shown ONCE per install, on the first open of the first telemetry build, when the player has never seen it. Default action (Esc, ✕, focus-out) is decline. The two buttons are equal weight: no visual default, no larger one, no accent color, no pre-focus. Decline is final with no re-prompt; the Options toggle (§B) is the only way back.

**Title (H2):**
`Help make EQBuddy better? (optional)`

**Body (single paragraph, no subheaders):**
> When this is on, EQBuddy sends a small "heartbeat" about how it is being used, roughly every 5 minutes while the app runs, and once more if you press Delete my telemetry data. *(C-1 / Helm, row 4)* That is the only time it sends anything.
>
> Exactly three fields are in each heartbeat — nothing else, ever:
>
> 1. **Install id** — a random number we create when you turn this on. It is not your name, computer, or account, and we cannot work backwards from it to you.
> 2. **App version** — the build number of EQBuddy you are running.
> 3. **Operating system** — your OS and its version, in the form the system reports it.
>
> That is the entire list. We keep each heartbeat for 90 days and then delete it. Aggregate counts of distinct installs (not your id, not your name) are what we use to size the backend. Your id is kept on your machine and in the heartbeats we store. It is how Delete finds your rows, and it is not linked to your name, computer or account. *(C-1 / Helm, row 1)*
>
> If you turn this off later, we delete the id from your machine and stop sending. Your past heartbeats stay until they age out.
>
> This is optional. EQBuddy works fully without it, exactly as it works today.

**The three-field list, as a table or three rows — copy for each row, verbatim:**

| Field | Copy (verbatim) |
|---|---|
| `installId` | *A random number we create when you turn this on. It is not your name, your computer, or your account. We cannot work backwards from it to you.* |
| `appVersion` | *The build number of EQBuddy you are running.* |
| `os` | *Your operating system and its version, in the form the system reports it.* |

**Footnote (small, dim, below the list, above the buttons):**
> This prompt appears once. If you decline, nothing changes on your machine and we won't ask again. You can turn this on any time from **Options → Help improve EQBuddy**.

**Link line (one line, above the buttons)** *(C-1 / Helm, row 6)*:
> Everything about it, and how to delete it: [link]

The target is `docs/Telemetry.md` once TEL-PR4 ships it; until then it may be this page, `docs/v2/telemetry.md`.

**Buttons, equal weight, left to right:**
- `Not now` *(C-1 / Helm, row 5)*
- `Send these heartbeats`

The left button is decline, the right button is accept, and they are visually identical in size, border weight, fill, and focus ring. Neither is the default. Esc, ✕, and clicking outside the dialog all decline.

**What this prompt must NOT imply (the "not-promise" list, for the requirement page's §S7 or wherever the copy is folded):**
- It must not imply the app improves, is better, or behaves differently when you say yes. The closing sentence carries that: *This is optional. EQBuddy works fully without it, exactly as it works today.*
- It must not imply any other data leaves the machine. Only the three fields, named.
- It must not imply the prompt itself will reappear, nag, or re-appear after an update.
- It must not imply the app is collecting "activity" or "usage" beyond the three named fields.
- It must not imply the data goes to a third party. Only the team's own backend.
- It must not imply the install id is persistent, permanent, or a user id.

---

#### B. The Options toggle + "everything it sends" + off-behavior

This is the Options row. The heading, the toggle, and the explanatory text below it are three separate surfaces.

**Row label (the toggle's name, on the left of the switch):**
`Help improve EQBuddy`  (switch: OFF by default)

**The "here is everything it sends" block — copy below the toggle, shown always (on or off), verbatim:**

**When OFF (toggle not moved, or turned OFF):**

> **Off — nothing is being sent. Heartbeats sent earlier age out within 90 days.** *(C-1 / Helm, row 2)*
>
> Turning this ON will start sending small "heartbeats" about how the app is being used. Each heartbeat carries exactly three fields — nothing else, ever:
>
> 1. **Install id:** a random number we create when you turn this on. It is not your name, your computer, or your account.
> 2. **App version:** the build number of the EQBuddy you are running.
> 3. **Operating system:** your OS and its version, in the form the system reports it.
>
> That is the entire list. We keep each heartbeat 90 days and then delete it. Turning this OFF at any time stops the sends AND deletes that install id from your machine — it is gone. If you turn it back on later, we create a new one, so we cannot connect it to the old one.

**When ON (toggle flipped to ON):**

> **On.** Each heartbeat (about every 5 minutes while the app runs; nothing on exit) carries exactly three fields — nothing else, ever:
>
> 1. **Install id:** `3a71c04b…` (your random number, created when you turned this on; delete it with the toggle or the button below).
> 2. **App version:** the build number of the EQBuddy you are running.
> 3. **Operating system:** your OS and its version, in the form the system reports it.
>
> That is the entire list. We keep each heartbeat 90 days and then delete it. Turning this OFF stops the sends AND destroys the install id on your machine.

**(Both states, below the list, verbatim — the off-consequence line the toggle must carry):**

> **What turning this OFF does:** it stops all sending, and it deletes the install id from your machine. You cannot re-enable the old id — turning it back ON creates a new one.
>
> **What turning it OFF does NOT do** (say so, don't let it be a surprise): your past heartbeats already sent on this machine stay on our backend until they age out of their 90-day window. We do not auto-delete them when you flip this OFF, because we no longer have the id to match them against — the id is what we use to find your rows, and it is already gone. If you want them gone right now, use **Delete my telemetry data** below.

**The "Delete my telemetry data" button, as a secondary action, verbatim:**

`Delete my telemetry data…`

When OFF: the button is still shown but dimmed, with the tooltip: *While this is off there is no install id to delete with. Any earlier heartbeats age out within 90 days.* *(C-1 / Helm, row 3)* (And not tappable, or tappable with a no-op.)

When ON: the button is active, and pressing it opens the §C dialog.

---

#### C. The delete affordance — "Delete my telemetry data"

The dialog title and body are below. The button labels are below the body. The dialog is a single action: confirm or cancel. Esc and click-outside cancel, not confirm.

**Dialog title:**
`Delete my telemetry data?`

**Dialog body, verbatim:**
> This deletes the heartbeats EQBuddy has sent from this computer, on our backend. What gets deleted:
>
> - Every raw heartbeat we have kept for your install id, including any within the past 90 days. (Heartbeats older than that are already gone — we auto-delete them at 90 days.)
> - Your install id from your machine.
>
> What does NOT get deleted (it is not yours to delete, and it is not about you): the aggregate counts (how many distinct installs turned this on, how many are active now, the version mix). Those numbers do not contain your id, and they are what the public page shows.
>
> After this, your old install id is gone. If you turn the toggle ON again, we create a new one and your fresh heartbeats are not connectable to the old ones.

**Buttons, equal weight, left to right:**
- `Cancel`
- `Delete. Do it now.`

The left button is Cancel, the right is Delete. Neither is the default. Esc and click-outside cancel.

**After successful delete (status line, in the place where §D's line sits):**

`Deleted. Your id is gone. No more heartbeats from this computer.`

Then the toggle reverts to OFF and the §B OFF-state copy returns.

**What this copy must NOT imply:**
- It must not claim it deletes more than "your install id's heartbeats + the id itself."
- It must not imply it rewrites or "scrubs" the public metrics (it cannot — aggregates contain no ids).
- It must not imply it deletes data on other players' machines.
- It must not imply it deletes the app, the install, or the app version.

---

#### D. The "last heartbeat" status line — fixed-shape, per trap 12

This line is the player's proof that they opted in and it is working. Fixed shape: the same words, in the same order, every time; only the relative time moves. It never invents a free-form sentence, and it never adds a second line.

**Shape (verbatim):**
`Last heartbeat: <RELATIVE TIME>`

where `<RELATIVE TIME>` is one of these fixed strings, chosen by the client based on when the last successful heartbeat was sent:
- `just now` (less than 1 minute)
- `N min ago` (1–59 minutes) *(C-1 / Helm, row 9)*
- `N hr ago` (1–23 hours) — e.g. `2 hr ago`, `14 hr ago`
- `yesterday` (24–47 hours) *(C-1 / Helm, row 9)*
- `N days ago` (2 days and more, whole days, floor) — e.g. `3 days ago`, `11 days ago` *(C-1 / Helm, row 9)*

**The three states the line can be in, and the exact copy for each:**

**State 1 — telemetry ON, at least one heartbeat has succeeded:**
> `On — last heartbeat: 4 min ago`
> (the `On` prefix is part of the fixed shape; the player reads "on, and working")

**State 2 — telemetry ON, no heartbeat has succeeded yet (e.g. first open after opting in, before the ~2 min launch-dwell fires):**
> `On — no heartbeats sent yet`

**State 3 — telemetry ON, the last attempt failed (send error, server down, etc.):**
> `On — last send failed, will try again` *(C-1 / Helm, row 12)*

**State 3b — telemetry ON, the last Delete did not reach the server** *(C-1 / Helm, row 8)*:
> `On — delete did not reach the server, try again`

**State 4 — telemetry OFF (or never opted in):**
The line does not render at all. (The §B OFF-state block above carries the "nothing sent" information, so a status line here would be a second source of truth for the same fact — trap 12's "two lines saying the same thing" is the shape to avoid.)

**Shape rules (for the client tests):**
- Always `On — ` or the state-2/3 strings above, then the last-heartbeat portion. No extra words, no punctuation shift, no "successfully" word, no sentence ending in a period.
- `<RELATIVE TIME>` is a closed set — the client MUST emit one of the five strings above *(C-1 / Helm, row 9)* and no other. No `5 minutes ago`, no `a moment ago`, no `5 m`, no localized plural.
- The line is ONE line; it does not wrap, does not truncate with an ellipsis, and does not gain a tooltip. If the room it lives in is too narrow to show the whole line at the smallest width the app supports, the room is too narrow — fix the room, not the line.
- The line never says the app is "improving," "learning," "helping," or "better." It says the last heartbeat happened, when.
- The line is present in both the WPF and Avalonia Options surfaces, in the same place in both, in the same font size as their sibling rows.

---

#### E. Promise check — every factual claim in the copy, mapped

| Copy line (verbatim) | Signed requirement |
|---|---|
| "roughly every 5 minutes while the app runs, and once more if you press Delete my telemetry data. That is the only time it sends anything." *(C-1 / Helm, row 4)* | TEL-003: "One heartbeat shortly after launch (~2-minute dwell…) then every 5 minutes while running. Nothing on exit." ✓ |
| "Exactly three fields are in each heartbeat — nothing else, ever" (×4 surfaces) | TEL-002: "Exactly three fields: installId, appVersion, os. The field list is a curated must-list with a guard." ✓ |
| "Install id — a random number we create when you turn this on. It is not your name, computer, or account, and we cannot work backwards from it to you." | TEL-002: "installId (random GUID minted at opt-in — never derived from hardware, user name, or paths)." ✓ |
| "App version — the build number of EQBuddy you are running." | TEL-002: "appVersion." ✓ |
| "Operating system — your OS and its version, in the form the system reports it." | TEL-002: "os (coarse platform + version string)." ✓ |
| "We keep each heartbeat for 90 days and then delete it." | TEL-004: "Raw heartbeats retained 90 days then deleted by scheduled job." ✓ |
| "Aggregate counts of distinct installs (not your id, not your name) are what we use to size the backend." | TEL-004: "aggregates (counts only, no ids) kept indefinitely." + TEL-005: "unique users, concurrent now, peak concurrent, version mix." ✓ |
| "Turning this OFF stops the sends AND destroys the local install id." | TEL-001: "Turning it OFF stops sends AND destroys the local install id." ✓ |
| "If you turn it back on later, we create a new one, so we cannot connect it to the old one." | TEL-001: "re-enabling mints a fresh one, so opting out is also an identity reset." ✓ |
| "Your past heartbeats already sent on this machine stay on our backend until they age out of their 90-day window." | TEL-004: raw heartbeats retained 90 days. ✓ + Dranak's 2026-09-23 comment on DRA-360's draft: "after a plain opt-out the past heartbeats can no longer be deleted on request; they age out within 90 days." The copy says so, verbatim. ✓ |
| "If you want them gone right now, use Delete my telemetry data below." | TEL-004: "an in-app 'Delete my telemetry data' action posts the install id to a delete endpoint; the backend hard-deletes every raw row for that id." ✓ (the button exists; the copy names it) |
| "We do not see it" (about the install id) | TEL-004: "IPs are never persisted or logged — transport sees them, storage never does." + TEL-002: the id is "never derived from hardware, user name, or paths." The id is minted client-side and sent off-machine; the server does not use it to identify the user. ✓ (the copy's claim is about the *user*, not the *id* — the id is a token, not an identity) |
| "It is not your name, your computer, or your account." | TEL-002: "never derived from hardware, user name, or paths." ✓ |
| "Your id is stored on your machine only; we do not see it." — **superseded by C-1 / Helm, row 1**: the §A body now reads *"Your id is kept on your machine and in the heartbeats we store. It is how Delete finds your rows, and it is not linked to your name, computer or account."* This row and the "We do not see it" row above are Bevel's check of the sentence as first written. | TEL-002: id is "random GUID minted at opt-in" on the client. The only copy of the id on the server is in the raw-heartbeat rows. The copy does not claim the server does not have a copy of the id — it claims the id does not identify the user. (If the Founder wants the stronger claim — "we do not keep your id on the server" — that is FALSE per the signed plan: the id is in every row. The honest claim is the one above.) |

**The "never" list, restated for the requirement page or README-rewrite drafts:**
The copy never implies: a third-party analytics service, a persistent permanent user identity, any other data category, a re-prompt or nag, or a conditional promise about app quality. It says what it sends, when, for how long, and how to turn it off and delete it. That is the whole thing.

---

**What I did NOT write (so it is a line, not a buried sentence):** no wording for the `SECURITY.md` "Zero telemetry" rewrite or the `README.md:43` rewrite — those are TEL-PR4's drafts and the signed plan says TEL-PR4 composes them at release time, not now. If Dranak wants me to draft a DRAFT replacement for those two public-promise sentences now, say so and I will — but I did not, because the signed plan's ordering says they are composition at release, not copy now, and I do not want to invent a surface the plan did not ask for.

**One judgment call I made that the Founder or Helm should be given room to push back on:** in §A I wrote the body as a single paragraph with a nested 3-item list inside it, rather than three separate "cards" or three separate dialog panes. The signed plan (TEL-001, §1) says "the prompt shows the entire payload (three fields), equal visual weight on both buttons, and links the requirement page"; it does not specify the shape of the surface (paragraph vs. card rows). I chose one paragraph + a 3-item list because that is what "here is everything it sends" reads as to a player, and because three separate cards would visually *over-weight* the payload in a way that reads as an ask rather than a disclosure. If the Founder prefers three separate rows (which is what the §A table above shows), the copy is already written to be either shape; the words are the same. The shape call is a layout question and I left it to the client PR to pick.

— Bevel, 2026-09-23 7:32 PM CT

---

#### §8.3.1 Where the copy and this page disagreed — as C-1 ruled

Found by reading every sentence of the copy above against §2–§7 (DRA-360,
2026-09-24), then **ruled by Helm in the C-1 read**
([#858 comment](https://github.com/DranakCorps-bot/EQBuddy/pull/858#issuecomment-5808133793),
2026-09-24). Every row is **Helm-RULED** and binds TEL-PR3 and any later
amendment of §8.3. Rows 1–6, 8, 9 and 12 are amended in the copy above, each
marked *(C-1 / Helm, row N)*; rows 7, 10, 11, 13 and 14 are rulings TEL-PR3
carries out, with Bevel's words left as written.

| # | Copy as TEL-A wrote it (surface) | This page | Status | Ruling (C-1 / Helm) |
|---|---|---|---|---|
| 1 | *"Your id is stored on your machine only; we do not see it."* (§A body) | The id **is** the heartbeat (§2) and sits in every raw row for 90 days (§6); `/delete` works because the server has it (§5). Bevel's own §E last row says the same. | **Helm-RULED** — FALSE, AMEND | Replaced with: *"Your id is kept on your machine and in the heartbeats we store. It is how Delete finds your rows, and it is not linked to your name, computer or account."* |
| 2 | *"Off — nothing is being sent, and nothing has been sent from this computer."* (§B, OFF) | After an opt-out, beats **were** sent and stay up to 90 days (§4). The same block's next paragraph says so. True only for a profile that never opted in. | **Helm-RULED** — FALSE after opt-out, AMEND (single text) | One OFF heading for both cases: *"Off — nothing is being sent. Heartbeats sent earlier age out within 90 days."* It is true when none were sent. |
| 3 | Dimmed Delete tooltip: *"There is nothing to delete — this computer never sent anything."* (§B, OFF) | Same fact as row 2. After an opt-out there may be rows, but no id to name them (§7, §11). | **Helm-RULED** — FALSE after opt-out, AMEND (single tooltip) | *"While this is off there is no install id to delete with. Any earlier heartbeats age out within 90 days."* |
| 4 | *"…roughly every 5 minutes while the app runs. That is the only time it sends anything."* (§A body) | Also sends once on **Delete** (`POST /delete`, §5; §8.4's egress row says so). The first beat is ~2 minutes after launch (§3), which "roughly" covers. | **Helm-RULED** — Incomplete, AMEND | The cadence sentence gains *"…and once more if you press Delete my telemetry data."* |
| 5 | Decline button `No, thanks.` (§A) | TEL-001, as amended and signed, names the decline as *"Not now"*, and §8.5's SECURITY.md draft quotes it (*"Not now" is final*). | **Helm-RULED** — ADOPT TEL-001 label | The button reads **`Not now`**. TEL-001 is not changed to match `No, thanks.` |
| 6 | No link on the prompt (§A) | TEL-001 and this section's constraint: the prompt links the player-facing twin, `docs/Telemetry.md` (TEL-PR4). | **Helm-RULED** — ADOPT | One line above the buttons: *"Everything about it, and how to delete it: [link]"*. Until TEL-PR4 ships `docs/Telemetry.md`, TEL-PR3 may link `docs/v2/telemetry.md`. |
| 7 | *"from **Options → Help improve EQBuddy**"* (§A footnote) | The toggle lives in the **Behavior** block (`SettingsBehaviorView`, §7), which both hosts compose. | **Helm-RULED** — ADOPT path fill | The footnote names the real path of the day (*Options → Behavior → Help improve EQBuddy*, or whatever TEL-PR3 ships). The toggle's name stays *Help improve EQBuddy*. |
| 8 | Delete failure has no copy (§C) | §7: on a failed delete nothing changes, and **the status line says the delete did not reach the server** so the player can retry. | **Helm-RULED** — ADOPT | A fifth §D string in the fixed shape: `On — delete did not reach the server, try again`. |
| 9 | *"the client MUST emit one of the six strings above"*; the ranges are `<1 min`, `1–58 min`, `1–23 hr`, `24–48 hr`, `3 days and more` (§D) | The list has **five** forms, with holes at 59 minutes and between 48 and 72 hours. A client test cannot pin a closed set that has gaps. | **Helm-RULED** — ADOPT gap-free set | `just now` (<1 min) · `N min ago` (1–59) · `N hr ago` (1–23) · `yesterday` (24–47 hr) · `N days ago` (≥2 days, floor). "Six" is corrected to "five". |
| 10 | Shape `Last heartbeat: <RELATIVE TIME>` against the State 1 example `On — last heartbeat: 4 min ago` (§D) | One fixed shape is needed (trap 12). | **Helm-RULED** — ADOPT State 1–3 shape | The State 1–3 strings are the shape, lowercase `last` after `On — `. TEL-PR3's test pins those strings exactly. |
| 11 | *"present in both the WPF and Avalonia Options surfaces"* (§D rules) | The Avalonia lane was deleted on 2026-09-04 (E-2c). There is one WPF view that reaches both hosts (§7). | **Helm-RULED** — ADOPT stale fix | Read as *"the one Behavior view"*. No second Avalonia Options surface is built. |
| 12 | *"On — last send failed, retrying"* (§D State 3) | §3: a failed beat is **dropped**, not retried. The next tick backs off to as long as 60 minutes. | **Helm-RULED** — ADOPT wording | `On — last send failed, will try again`. It is true at any backoff, and it does not imply the lost beat is re-sent. |
| 13 | *"On — no heartbeats sent yet"* (§D State 2) | §7 said *"never sent"*. | **Helm-RULED** — ACK | Bevel's words win. §7's bullet points here. |
| 14 | The ON block shows `3a71c04b…`, the id's prefix (§B) | No rule against it. The id is not secret from its owner, and showing it helps a player check the public repo's claim. | **Helm-RULED** — ADOPT | TEL-PR3 shows the first 8 hex characters of `TelemetryInstallId`. The fixture has no id, so the ON shot needs one seeded (trap 23). |

**What rows 2 and 3 cost TEL-PR3, and how C-1 settled it.** To tell "never
opted in" from "opted out" the client would need one more remembered fact,
because `TelemetryPromptShown` is `true` after both a declined prompt and an
opt-out. The choice was a fourth key (`TelemetryEverSent`) or one OFF text true
in both cases. **Helm REJECTED `TelemetryEverSent`**: there is no fourth
settings key, and §7's three keys and their `DeadSettingTests` rows stand.
**Helm ADOPTED the single OFF text** (rows 2 and 3 above), true for a player
who never opted in and for one who opted out.

**Bevel's layout judgment call (§A) — Helm-RULED, ADOPT:** one paragraph with
a nested three-item list. Three separate cards are not required.

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
| TEL-A | DRA-359 (Bevel) | — | Consent copy for the prompt, toggle, status line and delete, as text in `BEVEL.md`. **Delivered** 2026-09-23 (`6611cb61`) and folded into §8.3 |
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
- **TEL-PR2's amendments are folded in as it built them** (DRA-361, via
  eqbuddy-telemetry PR #1): the fifth column `last_seen_ms`, the two trailing
  numbers refreshed daily from the rollup, and the exact 60 000 / 59 999 ms
  rate-limit edge. §5 invited TEL-PR2 to change the wire defaults and required
  this page to follow; none of the three adds data about the player, and the
  keys a reader sees are unchanged.
- **TEL-A's copy is folded verbatim and its conflicts are TABLED, not
  fixed** (§8.3.1). The fold could have corrected the copy in place. That
  would have put words under Bevel's name that Bevel did not write, and hidden
  from C-1's reader exactly what the read exists to catch. The rows marked
  FALSE bind TEL-PR3 regardless, because the page wins. **C-1 then ruled
  every row** (Helm, 2026-09-24), and the amendments it made are marked as
  Helm's, not Bevel's.
- **"Never phones home" leaves the README principle line** (§8.1). It could
  have been kept as "never phones home without asking". A principle a
  network monitor can falsify is not one to keep.
