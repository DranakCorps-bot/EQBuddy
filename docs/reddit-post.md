# Reddit post draft for r/EQLegends

**Suggested title:**

> I made EQBuddy — a free, open-source session tracker widget for EQ Legends (kills, DPS, loot, money, XP — straight from your /log file)

**Body:**

---

My family and I have been playing EQ Legends together since launch, and I wanted a way to
see what we'd actually accomplished each session — so I built a little Windows widget and
figured others here might get some use out of it.

**What it is:** an always-on-top card that reads your character's log file live and
aggregates the session: creatures killed, DPS, loot, coin, XP, skill-ups, faction hits,
deaths, zones. Click any section to drill in — every creature killed with counts, every
item looted, damage per attack/spell/song with averages, who healed you, what you sold
and for how much.

A few things I'm proud of:

- **It only reads the log file.** No injection, no memory reading, no overlay hooks —
  it's the same text file `/log` has always written. EQBuddy even flips the ini setting
  so logging is always on, and it empties finished-session logs so they never grow.
- **Combat-aware DPS.** Damage ÷ time actually in combat. Your group fighting keeps the
  clock alive (melee aren't punished for repositioning, casters aren't punished for
  weaving), but downtime and med breaks never dilute the number.
- **Pets count.** It learns your pet's name from its "Attacking X Master." chatter and
  credits pet damage and kills to you — mage pet armies get full credit per summon.
- **Bard songs count** (they log as damage-over-time ticks).
- **Auto-follows whoever is playing.** No setup per character — it watches whichever log
  is growing, so it just works on a shared family PC.
- **Mini mode.** Star the stats you care about, hit minimize, and you get a tiny pill
  (kills / DPS / money / XP with estimated time-to-level) that sits over the game.
- **Estimated time to next level** at your session's XP pace.

**Download:** grab `EQBuddySetup.exe` from the releases page —
https://github.com/DranakCorps-bot/EQBuddy/releases/latest — no admin needed.
Source is all there too if you'd rather read or build it yourself.

**Honest caveats:**

- Windows only (WPF app). Windows SmartScreen will warn on first run because it's
  self-signed, not signed with a paid certificate — "More info → Run anyway," or build
  from source if you'd rather.
- The log parser knows every message format my family's characters have produced up to
  the low-20s (melee, casters, a mage/bard, pets, merchants). If you see a number that
  looks wrong or something that isn't tracked, paste the raw log line in the comments —
  adding a new pattern is usually a five-minute fix.
- To start a fresh count mid-session there's a reset button; a "session" otherwise ends
  after 60 minutes of no activity.

Screenshots in the comments/post: the compact card, the fully expanded breakdown, and
the minimized pill.

Happy hunting!

---

**Screenshot files to attach** (in `docs\screenshots\`):

| File | Caption suggestion |
|---|---|
| `widget-compact.png` | The default card — one line per category, click to expand |
| `widget-expanded.png` | Full drill-down: damage per skill with averages, kills per creature, loot, money incl. merchant sales, skill-ups |
| `widget-mini.png` | Mini mode — just the stats you starred, with time-to-level estimate |
