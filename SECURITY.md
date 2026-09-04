# Security

EQBuddy runs next to your game and reads your log file. That's a position of
trust, so this page says exactly what the app does with it — every network
connection, every file it writes, and how updates are verified. If anything
here stops matching the code, that's a bug; report it like one.

## Supported versions

The latest release, only. EQBuddy checks for updates at startup and every
6 hours and offers the new version in a banner, so staying current is one
click — and on the family/OneDrive channel the installer syncs down within a
few hours of release. There are no maintained older branches; a security fix
ships as the next release.

## Every network destination, and why

EQBuddy's rule is **log-only, zero telemetry**: it never sends your data
anywhere on its own. The complete list of hosts the app itself contacts:

| Host | When | What |
|---|---|---|
| `eqlwiki.com` | You hover/click an item, open item info, or use the drops/quest views | Read-only MediaWiki API lookups of item and mob pages. Responses are cached locally for a week and labelled LIVE / CACHED / STALE. Your search term is the only thing in the request. |
| `api.github.com` | Startup, every 6 h, and right-click → "Check for updates" | A read-only request for the latest release's version number and asset list. Nothing about you or your session is attached. |
| `github.com` | Only when you click the update banner | Downloads `EQBuddySetup.exe` and its published `.sha256` from the release you were just shown. |

That's the whole list. The family/guild update channel is not a network
request at all: EQBuddy reads `EQBuddySetup.exe` from a locally synced
OneDrive folder (`EQBuddyDownload`) — OneDrive does the transport, EQBuddy
just checks the file that's already on disk.

Some buttons open your **browser** at a site — the app never fetches these
itself, it hands the URL to your default browser and steps away:

- eqlwiki.com pages (item info "open wiki page", wiki search, the ✦ wiki
  contribution edit links)
- GitHub: the repository, the releases page, and pre-filled Discussion drafts
  (Send feedback, Submit-to-EQBuddy zone shares, the quest ⚑ report). These
  open as **drafts in your browser for you to review and post under your own
  account** — the app posts nothing.
- eqmaps.info (the map window's "Get maps…" button, linking Brewall's map packs)
- eqlegendstools.com (the char-sheet link in Options)

## EQBuddy Mobile (LAN listener)

The "EQBuddy Mobile (Beta)" feature (Options → Behavior → EQBuddy Mobile) is the
one place EQBuddy can LISTEN on the network instead of only making requests, so
here is exactly what it does:

- **Off by default.** Until you flip the toggle, nothing listens on anything.
  The toggle's state persists; turning it off closes the listener immediately.
- **LAN only.** The server binds your machine's local-network addresses
  (skipping loopback-only and link-local) on one TCP port (default 47859,
  changeable). It is plain HTTP + WebSocket on your Wi-Fi — nothing is hosted
  on the internet, no cloud relay, no account. Traffic goes phone → PC and
  never leaves your network. (Consequence to know: LAN HTTP is unencrypted,
  so anyone on the same network could observe it — the data at stake is what
  your desktop cards already show you.)
- **Token-gated.** Enabling the feature mints a crypto-random 128-bit pairing
  token, carried in the QR code's URL *fragment* (the part after `#`, which
  browsers never send in requests). The page presents it on the WebSocket
  connect; a connect without the exact token is refused, and repeated failures
  are rate-limited per IP. Once the PC has accepted it, the device remembers
  the code in its own browser storage, so an "Add to Home Screen" launch (which
  starts at the bare address, without the `#`) reconnects — the code never
  leaves that device. "New code" mints a fresh token, disconnects every paired
  device, and makes every remembered one useless; a device whose remembered
  code is refused forgets it and asks to be paired again.
- **Unauthenticated surface = the explainer page only.** A browser hitting the
  address without the token gets a static page that says how to pair, plus the
  Home Screen manifest and its icon. None of it contains game data or the
  pairing code; data flows only over the token-checked WebSocket.
- **You choose what's offered.** The pairing window lists every screen the PC is
  willing to send — the zone map, spawn timers, mez chips, buffs, the combat
  breakdowns, session stats, loot and watches, XP/AA, and the Epic, Sky and
  Gear checklists. Untick anything you want to never leave the machine; a
  withheld screen is never even assembled, let alone sent. Each device then
  picks its own subset of what's offered — that choice is stored on the device,
  not by EQBuddy.
- **What's actually sent:** character name, current zone, app version, your
  desktop theme's colors, and whichever of the above screens are both offered
  and picked. It is the same information the desktop's own cards are showing
  you; nothing is sent that isn't on a screen you could already see.
- **Mobile sounds**, if you turn them on (Options → Behavior; off by default),
  add exactly two values to that: the switch's own state, and a count of alerts
  raised since EQBuddy started. The count is what tells the device to play a
  tone. It names nothing — not the rule, not the mob, not the zone — and it is
  not one of the screens above, because a device showing only the map is still
  a device that should be able to hear a camp come due.
- **What comes back from the device:** its screen picks, and ticks on the Epic,
  Sky and Gear checklists — the same tick a click on the PC makes. There is no
  other write: a device cannot change settings, run commands, or touch
  anything the desktop doesn't already offer as a checkbox.
- **Windows Firewall** will ask to allow EQBuddy the first time it listens;
  saying no (or missing the prompt) silently blocks phones — the pairing
  window says so and tells you where to fix it. EQBuddy never edits firewall
  rules or elevates to try.

## Zero telemetry

There is no analytics endpoint, no crash reporter, no usage ping, no
"anonymous statistics". Errors go to a local file
(`%AppData%\EQBuddy\error.log`), full stop. When knowledge moves between
players it moves because a player chose to move it: share strings you paste
to a friend, the ✦ Copy-for-wiki button that fills your clipboard, feedback
drafts you post yourself. If you ever catch EQBuddy sending something this
page doesn't list, that is a vulnerability — report it as one.

## What it writes locally

Everything lives under `%AppData%\EQBuddy\` (or wherever `EQBUDDY_APPDATA`
points): `settings.json`, `history.db` (your session history, SQLite),
`error.log`, and the per-character ledgers and archives (AA ledger, quest
ledger, spawn-point archives).

The log janitor is the one thing that touches files outside that folder,
and it asks first — the tutorial's opening page asks whether EQBuddy may
auto-empty finished-session logs, and nothing is touched until you answer:

- It sets `Log=1` in the game's `eqclient.ini`, only while the game is closed.
- With auto-empty **on**, a character log quiet for 60+ minutes (a finished
  session) is emptied — never deleted. With *"Keep a timestamped copy before
  emptying"* on, the content is first archived to `Logs\archive\` as its own
  file.
- With auto-empty **off**, EQBuddy never touches your log files.
- The janitor stands down completely while the game, GINA, or GamParse is
  running, so no other tool's read position is ever yanked out from under it.

## Update trust model

- Every release publishes `EQBuddySetup.exe.sha256` beside the installer. The
  auto-updater stages the installer to a temp file and verifies it against
  that hash before running it; a mismatch deletes the staged file and aborts.
- **Fail closed:** if a GitHub release has no published hash, the updater
  refuses to download at all and points you at the release page instead. The
  same verification runs on installers taken from the OneDrive/family folder.
- Installers and the app executable are signed with a **publicly trusted
  certificate** via Azure Artifact Signing, as `CN=FlossworksCross-Stitch`,
  chaining to a Microsoft public CA. Releasing cannot skip this: `scripts\release.ps1`
  resolves the signing toolchain before it builds anything and aborts unless every
  artifact comes back with a signature that verifies and carries a timestamp.
  SmartScreen reputation still accrues over time, so warnings fade rather than
  stop dead (see the README's security note).
- Signatures are **timestamped** (`timestamp.acs.microsoft.com`). Artifact Signing
  certificates are valid for three days by design; the countersigned timestamp is
  what keeps an installer you downloaded last month verifiable today.
- The updater is slated to additionally validate the publisher identity on staged
  installers — hash verification proves the file is the published one, signature
  validation will prove who published it.

## Reporting a vulnerability

Use GitHub's private vulnerability reporting: **Security → Report a
vulnerability** on this repository (it's enabled). You'll reach the
maintainer directly and privately, and you'll get credit in the fix's release
notes unless you'd rather not. For anything that isn't sensitive — a
suspicious warning, a hardening idea — a public
[Discussion](https://github.com/DranakCorps-bot/EQBuddy/discussions) is fine
too.
