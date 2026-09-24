# Telemetry in EQBuddy: off unless you turn it on

EQBuddy Evolved can send one small thing off your PC: a **heartbeat**. It is
**off on every install until you say yes.** This page is everything it sends,
when, how long it is kept, and how to delete it. If EQBuddy ever does something
this page does not say, that is a bug, and if it sends anything while telemetry
is off, that is a vulnerability: [report it as one](../SECURITY.md).

EQBuddy 1.x and the legacy builds never send anything, and never will.

## Being asked

The first time you open EQBuddy Evolved, it asks once: **"Help make EQBuddy
better? (optional)"**. The two buttons are the same size and neither is the
default. **Not now**, Esc and ✕ all say no.

- **No is final.** EQBuddy will not ask again, not on the next launch and not
  after an update.
- **Options → Behavior → Help improve EQBuddy** is the only way to turn it on
  later, or off again.
- EQBuddy works fully without it, exactly the same.

## What a heartbeat carries

Exactly three fields. Nothing else, ever:

| Field | What it is |
|---|---|
| **Install id** | A random number EQBuddy creates at the moment you turn this on. It is not your name, your computer or your account, and it is not worked out from any of them. |
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
- **On:** about 2 minutes after EQBuddy starts, then about every 5 minutes while
  it runs. Nothing when it closes.
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

The [README](../README.md#how-many-people-use-it) shows four numbers, read live
from the server's public
[`metrics.json`](https://eqbuddy-telemetry.eqbuddy-telemetry.workers.dev/metrics.json),
which carries the same definitions beside them:

- **Installs, last 30 days:** distinct installs that sent a heartbeat in the 30
  days up to the end of the last complete day (UTC).
- **Running now:** distinct installs that sent one in the last 10 minutes.
- **Most at once:** the most distinct installs in any single 10-minute window.
- **Version mix:** the share of the last 7 days' installs on each version.

Every one of them counts **installs that turned this on**, not people. Someone
on two PCs is two installs, someone who turns it off and on again gets a new id
and can count twice, and everyone who said "Not now" is not counted at all. They
are the smallest honest number, not the real one.

The README's **downloads** figure is a different thing: it is GitHub counting
installer fetches. A re-download, an update and a bot all count, so it says
nothing about how many people play. The installs number is the honest one.

---

*The engineering version of this page, with the wire format and the storage
schema, is [docs/v2/telemetry.md](v2/telemetry.md).*
