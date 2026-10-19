# Telemetry in EQBuddy: on by default, off with one switch

EQBuddy Evolved can send one small thing off your PC: a **heartbeat**. From
**v2.0.7 it is on for every install**, including installs that said "Not now" or
turned it off before, and one switch turns it off. It had been on by default
since v2.0.6, and before that it was off until the player said yes. It is there so
David can see how many people use EQBuddy and how, to plan support and
resources. This page is everything it
sends, when, how long it is kept, how to turn it off, and how to delete it. If EQBuddy ever does something
this page does not say, that is a bug, and if it sends anything while telemetry
is off, that is a vulnerability: [report it as one](../SECURITY.md).

EQBuddy 1.x and the legacy builds never send anything, and never will.

## The note after the update

There is no prompt and nothing to answer. The first time you open v2.0.7, a
short note on the widget says **"EQBuddy now sends a small check-in"**, names
the three things a heartbeat carries, and has a button that opens Options
where it is turned off.

- **v2.0.7 turns telemetry on for every install.** That includes you if you
  said "Not now" to the old question, turned it off in Options, or deleted
  your telemetry data on an earlier version: it is on again, with a new random
  install id, and the note says so.
- **Where it was off before, nothing is sent straight away.** The first
  heartbeat waits about five minutes after EQBuddy opens, and the note shows
  each time you open EQBuddy until that first heartbeat has gone. Turn it off
  in that time and nothing is sent at all.
- **Options → Behavior → Help improve EQBuddy** is the one way off, at any
  time. **Turning it off from v2.0.7 on is final:** EQBuddy never turns it back
  on. You can turn it on again yourself in the same place.
- EQBuddy works fully without it, exactly the same.

## What a heartbeat carries

Exactly three fields. Nothing else, ever, and neither v2.0.6 nor v2.0.7 added any:

| Field | What it is |
|---|---|
| **Install id** | A random number EQBuddy creates at the moment this is turned on (on the v2.0.7 update, if it was off before). It is not your name, your computer or your account, and it is not worked out from any of them. |
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
  every 5 minutes while it runs. Nothing when it closes. The one exception is
  the first heartbeat after v2.0.7 turned an off install on: that one waits
  about five minutes (above).
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
and can count twice, and everyone who turned it off is not counted at all. They are a lower bound: the smallest
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

### The v2.0.7 cutover

**Telemetry is on for every install from v2.0.7**, including installs that
said "Not now" or turned it off on an earlier version. So from then every
install is counted unless it was turned off, and the same figures rise again
after that release because more installs are counted, not because more people
play. The version mix shows it too: installs on 2.0.7 and later are counted
unless they were turned off on 2.0.7 or later.

The README's two **downloads** figures (the last 30 days since 2.0, and the
all-time total since 1.x) are a different thing: they are GitHub counting
fetches. A re-download, an update and a bot all count, so neither says how many
people play. The installs number, `uniqueUsers30d`, is the closest honest one.

---

*The engineering version of this page, with the wire format and the storage
schema, is [docs/v2/telemetry.md](v2/telemetry.md).*
