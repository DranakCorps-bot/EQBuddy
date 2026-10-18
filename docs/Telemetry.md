# Telemetry in EQBuddy: on by default, off with one switch

EQBuddy Evolved can send one small thing off your PC: a **heartbeat**. From
**v2.0.6 it is on by default**, and one switch turns it off. Until v2.0.6 it was
off on every install until the player said yes. This page is everything it
sends, when, how long it is kept, how to turn it off, and how to delete it. If EQBuddy ever does something
this page does not say, that is a bug, and if it sends anything while telemetry
is off, that is a vulnerability: [report it as one](../SECURITY.md).

EQBuddy 1.x and the legacy builds never send anything, and never will.

## The one-time notice

Before any heartbeat is sent, EQBuddy shows a notice once: **"EQBuddy sends a
small check-in"**, naming the three things a heartbeat carries. Its **Learn
more** link opens this page and answers nothing. It has two buttons of the
same size: **OK** and **Turn off**.

- **OK keeps it on, and so does closing the notice** (Esc, ✕). The notice says
  so before you close it. **Turn off is the only way to stop it there.**
- **Nothing is sent before the notice has been on screen.** If EQBuddy is
  stopped or crashes while the notice is up, nothing is saved and nothing is
  sent, and the notice shows again next time.
- It shows once. EQBuddy will not show it again, not on the next launch and
  not after an update.
- **Options → Behavior → Help improve EQBuddy** turns it off at any time, or
  back on.
- EQBuddy works fully without it, exactly the same.

**If you said "Not now", or turned it off, on an earlier build, it stays off.**
EQBuddy does not show you the notice and does not ask again; Options is the
way back in if you ever want it. Only an install that never answered the old
question sees the new notice.

## What a heartbeat carries

Exactly three fields. Nothing else, ever, and v2.0.6 did not add any:

| Field | What it is |
|---|---|
| **Install id** | A random number EQBuddy creates at the moment this is turned on (for a new install, when the notice closes). It is not your name, your computer or your account, and it is not worked out from any of them. |
| **App version** | The build number of the EQBuddy you are running, for example `2.0.0`. |
| **Operating system** | "Windows" and its version number, for example `Windows 10.0.26200`. No edition, no language, no location. |

A heartbeat carries no logs, no character names, no chat, no file paths, no
account, no hardware ids and no session numbers. There is no crash reporter and
there are no usage events. Here is a whole heartbeat:

```json
{
  "installId": "3f2b8c1e-9a47-4d2e-b0c6-5e81a7d4f920",
  "appVersion": "2.0.0",
  "os": "Windows 10.0.26200"
}
```

The app and the server both refuse any other shape, so a fourth field cannot
slip in quietly: a test fails the build that tries.

## When it sends

- **Off:** never. No timer is started and no connection is opened.
- **On:** once when EQBuddy starts (or right after you turn it on), then about
  every 5 minutes while it runs. Nothing when it closes.
- **When you press "Delete my telemetry data":** once, to delete (below).
- **If a heartbeat fails** (no network, server down), it is dropped, not saved
  up and sent later. EQBuddy waits longer before the next one, up to an hour
  between tries, and goes back to every 5 minutes once one gets through.

Options shows **"On — last heartbeat: 4 min ago"** (or that none has been sent
yet, or that the last one failed) so you can see it working.

Heartbeats go to one address, `eqbuddy-telemetry.eqbuddy-telemetry.workers.dev`,
over HTTPS, and it is on
[SECURITY.md's complete list of hosts](../SECURITY.md#every-network-destination-and-why).

## What is kept, and for how long

| What | Kept |
|---|---|
| Each heartbeat | **90 days**, then deleted automatically |
| Your IP address | **Never stored.** Your connection has to come from somewhere, so the network sees it on the way in; nothing writes it down. No request logs are kept. |
| Counts: how many installs were on, and on which version | Indefinitely. They are numbers with no install id in them, so they are not about you. |

The server is its own public repository,
[`DranakCorps-bot/eqbuddy-telemetry`](https://github.com/DranakCorps-bot/eqbuddy-telemetry),
so you can read exactly what it stores.

## Turning it off

Options → Behavior → **Help improve EQBuddy**, off. EQBuddy stops sending
straight away and **deletes the install id from your PC.** If you turn it on
again later, it makes a new one, so the new heartbeats cannot be connected to
the old ones.

Turning it off does **not** delete heartbeats already sent. Once the id is gone
from your PC there is nothing left to find them by, so they age out within 90
days. If you want them gone now, use Delete instead (below): it deletes them
and turns telemetry off in one step.

## Deleting your data

Options → Behavior → **Delete my telemetry data…** (only while it is on). It
sends your install id one last time, and the server deletes **every heartbeat
it has for that id**, straight away. Your PC deletes the id too, and the toggle
goes back to off.

It cannot remove you from the counts, because the counts never had you in them:
they are totals with no id attached.

## The public numbers

The [README](../README.md#how-many-people-use-it) shows five numbers, read live
from the server's public
[`metrics.json`](https://eqbuddy-telemetry.eqbuddy-telemetry.workers.dev/metrics.json),
which carries the same definitions beside them:

- **Downloads, last 30 days:** not telemetry at all. It is GitHub's own count
  of fetches of the EQBuddy Evolved installer and portable zip, counting from
  2.0 (September 28, 2026), which the server reads from GitHub once an hour.
  The `.sha256` checksum files are left out. A re-download, an update and a bot
  all count, so it is downloads, not people.
- **Running now:** distinct installs that sent one in the last 10 minutes.
- **Most at once:** the most distinct installs in any single 10-minute window.
- **Version mix:** the share of the last 7 days' installs on each version.
- **Total hours played (estimated):** hours EQBuddy Evolved was running on
  installs that turned this on, at 10-minute resolution (each install seen in a
  10-minute window counts as 10 minutes), since the server's first stored day,
  September 24, 2026, rounded to the hour. It is the time the app was open, not
  quite the time the game was played, and it is an estimate.

The installs figure the README used to show (distinct installs that sent a
heartbeat in the 30 days up to the end of the last complete UTC day) is still
published as `uniqueUsers30d` in `metrics.json` and on the report page.

The same counts, with charts, are on the server's public
[report page](https://eqbuddy-telemetry.eqbuddy-telemetry.workers.dev/report).

The [EQBuddy site](https://dranakcorps-bot.github.io/EQBuddy/) shows five
figures from the same server, updated about once an hour:

- **Total installs:** a running count that adds one the first time an install
  sends a heartbeat. It keeps no install id, so an install that comes back after
  90 days silent, or turns this off and on again, counts again.
- **Hours used:** estimated hours of use at 10-minute resolution (each install
  seen in a 10-minute window counts as 10 minutes), all time, rounded to the
  hour.
- **Peak daily users:** the most distinct installs in any single day (UTC),
  today included.
- **Peak weekly active:** the most distinct installs in any 7-day window,
  today included.
- **Peak concurrent:** the most distinct installs in any single 10-minute
  window (the README's "most at once").

None of them is ever about one install: they are totals, and nothing public
carries an install id.

Every one of them except downloads counts **installs with telemetry on**, not people. Someone
on two PCs is two installs, someone who turns it off and on again gets a new id
and can count twice, and everyone who turned it off (or said "Not now" on an
earlier build) is not counted at all. They are a lower bound: the smallest
honest number, not the real one.

### The v2.0.6 cutover

**Telemetry is on by default from v2.0.6. Earlier figures counted opted-in
installs only.** So running now, most at once, hours, and the site's peak
figures rise after that release because more installs are counted, not
because more people play. Read any chart across that release with that in
mind. The version mix shows it in the data: installs on 2.0.6 and later are
counted unless they turned it off, and earlier versions only if they opted in.
Nobody has measured how much of the rise is installs that never saw the old
question rather than people who said "Not now", so the size of the jump is not
a measurement of either.

The README's two **downloads** figures (the last 30 days since 2.0, and the
all-time total since 1.x) are a different thing: they are GitHub counting
fetches. A re-download, an update and a bot all count, so neither says how many
people play. The installs number, `uniqueUsers30d`, is the closest honest one.

---

*The engineering version of this page, with the wire format and the storage
schema, is [docs/v2/telemetry.md](v2/telemetry.md).*
