# EQBuddy — EverQuest Legends Session Tracker

An always-on-top Windows widget that reads your EverQuest Legends `/log` file live and
shows what's happened this play session: kills, DPS, loot, money, XP, skill-ups,
faction changes, deaths, and zones visited. Click any section to drill into details.

**Download:** grab `EQBuddySetup.exe` from the
[latest release](https://github.com/DranakCorps-bot/EQBuddy/releases/latest).

## For players (family install guide)

1. Run **EQBuddySetup.exe** and click through the installer (no admin needed).
2. Launch **EQBuddy** from the Start Menu or desktop shortcut.
3. Start EverQuest Legends and log into your character.
4. Play! The widget updates live. Click a section (Combat, Kills, Loot, …) to expand details.
   - EQBuddy turns the game's logging on permanently (it sets `Log=1` in `eqclient.ini`
     whenever the game isn't running), so you normally never need to type `/log`.
   - The dot in EQBuddy's corner turns **green** when it's receiving data. If it's red
     with a yellow banner during play, type `/log` in the game's chat as a one-time fix.

Mini dashboard:
- Click the **★ star** next to any section header to include that stat in the mini dashboard.
- Click **–** in the title bar to minimize: only your starred stats remain, in a tiny
  always-on-top pill (e.g. `💀 12  ⚔ 34 dps`). Great while actually fighting.
- Double-click the pill (or click ⤢) to expand back to the full view.

Updates (automatic, no internet service involved):
- EQBuddy checks the family's shared **EQBuddyDownload** OneDrive folder at startup and
  every 6 hours. When a newer version is there, a green banner appears — click it and
  EQBuddy installs the update silently and restarts itself.
- Right-click the widget → **Check for updates** to check on demand.
- On a family PC where the shared folder syncs to a different path, EQBuddy auto-finds a
  folder named `EQBuddyDownload` under that PC's OneDrive; if it can't, set `UpdateFolder`
  in `%AppData%\EQBuddy\settings.json`.

Log cleanup (automatic):
- Because logging is always on, EQBuddy empties any character log that has been quiet
  for 60+ minutes (a finished play session), so files never grow across sessions.
  Cleanup runs at EQBuddy startup and every 10 minutes — but never while the game is open.

Notes:
- The title bar shows which character EQBuddy is following. It always tracks whoever is
  actively playing (the log file that's currently growing) and switches automatically
  within a few seconds when you swap characters.
- The ↻ button clears the session and starts counting from now.
- The 📌 button toggles always-on-top. Drag anywhere on the widget to move it.
- A "session" is a contiguous stretch of play. After 60+ minutes of no log activity,
  the next activity starts a fresh session automatically.

## How DPS is measured

Session DPS = your damage ÷ time actually **in combat**, so downtime never dilutes it.

- **Your pet counts.** EQBuddy learns your pet's name from its "Attacking X Master."
  chatter and credits its melee and spell damage to you (shown as "Pet (Name)" in the
  damage breakdown). Pet kills count as your kills.
- The combat clock opens when *you* act — hit, miss, pet attack, or getting hit — and
  stays open while your group keeps fighting, so slow-swinging melee and casters between
  casts aren't penalized mid-fight.
- Others' fighting only keeps your clock running for ~20 s past your last action:
  tagging one mob doesn't charge you for the whole group fight, and idle time in a busy
  zone never counts. The clock closes after 10 quiet seconds.
- The Combat detail view shows total time-in-combat so you can see the denominator.

## What it tracks

| Section | Summary stat | Click-in details |
|---|---|---|
| Combat | Session DPS (+ live fight DPS) | Damage by attack/spell, crits, accuracy, biggest hit, time in combat, damage taken per mob, healing, fizzles/resists |
| Kills | Your kills (+ group kills) | Count per creature type, kills/hour, group-member kill counts |
| Loot | Items looted (+ items made) | Every item with counts, items created by merging |
| Money | Coin earned (p/g/s/c) | Drop count, biggest drop, money per hour |
| Progress | XP % gained | XP ticks, %/hour, level-ups with times, skill-ups per skill |
| Faction | Factions touched | Net standing change per faction |
| Travels & Deaths | Death count | Each death (what killed you, when), zones visited with times |

## For developers

- `src/EQBuddy` — WPF app (.NET 10, `net10.0-windows`). Build: `dotnet build -c Release`.
- `src/EQBuddy/Core/LogParser.cs` — one regex per log-line type; add new patterns here.
- `src/EQBuddy/Core/SessionStats.cs` — aggregation + DPS fight tracking + session rollover.
- `src/EQBuddy/Core/LogWatcher.cs` — file tailing (500 ms polls, offset-based, truncation-safe).
- `src/EQBuddy/Core/EqConfig.cs` — log hygiene: forces `Log=1` in eqclient.ini and truncates
  stale (60+ min quiet) logs; both are skipped while `eqgame.exe` is running.
- Publish: `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o dist/publish`
- Release: `scripts\release.ps1` — reads the version from the csproj, publishes, signs both
  exes (self-signed cert; create once with `scripts\new-cert.ps1`), compiles the installer
  with the matching version stamp, and copies to the OneDrive family folder. Pass
  `-Tag vX.Y.Z` to also publish a GitHub release. Bump `<Version>` in the csproj first —
  the in-app updater compares it against the version stamped into the shared setup exe.
- Settings live in `%AppData%\EQBuddy\settings.json`; errors in `%AppData%\EQBuddy\error.log`.
- Debug: set `EQBUDDY_EXPAND=1` to launch with all sections expanded plus a state dump
  in `%AppData%\EQBuddy\debug.txt`.

Log folder auto-detected at
`C:\Users\Public\Daybreak Game Company\Installed Games\EverQuest Legends\Logs`
(`eqlog_<Character>_<server>.txt`).
