# The EQBuddy Evolved launch trailer — recipe

A ~65 s, 1080p30 YouTube trailer. **Every picture of EQBuddy in it is a capture of the
real build**, staged from a real character's log (David's Dranak, used with his permission,
2026-09-28). This folder only frames, times and scores those captures. That is the
illustration lock (CLAUDE.md) applied to video: a picture of our UI is a capture with a
recipe, or it does not ship.

## What is real, and where it comes from

| In the trailer | Source |
|---|---|
| The in-game HUD (bar, peeks, watch alert, spawn row) | `record-tray-gifs.ps1 -Gif trailer-hud -SourceLog … -CutAt …`: the built EQBuddy.exe on a throwaway profile. The log up to the cut is his history; the 32 s after it are appended **live at the pace he played them**, so every tick on screen is the shipped parser reading a line he played. Recorded full-screen over the key colour `#2A002A`. |
| Helper (hunt, gear), Guide (Plane of Sky), World → Drops | `shoot.ps1 -Shot trailer-* -SourceLog … -CutAt …` (PrintWindow stills). The rooms read his rebuilt sessions and his own `/outputfile` dumps. |
| The phone | `phone.ps1`: the real app serving EQBuddy Mobile from the same staged log, and the **shipped** page loaded by headless Edge at an iPhone viewport. Nothing is stubbed. |
| Game footage behind the HUD | A screen recording of his own play (2026-09-10), cropped to the 3D view and blurred to atmosphere (`gblur` σ 26), so no UI or name in it is legible. |
| Music | `score.py`: synthesized from arithmetic, no samples, so no Content ID claim. 96 BPM; its bar map is the edit's. |

Staging a real log is `scripts/real-log-staging.ps1`, shared by all three capture scripts:
the source is only **read**, copied into the throwaway profile with every stamp shifted by
one constant so the history ends "now", and his dumps are copied beside it.

## Run it

```bash
dotnet build EQBuddy.slnx -c Release
```

```bash
uv venv scripts/trailer/.venv --python 3.12 && uv pip install --python scripts/trailer/.venv/Scripts/python.exe numpy scipy pillow playwright
```

Then, with `LOG` the game log and `B` an empty build folder:

```bash
pwsh -NoProfile -File scripts/record-tray-gifs.ps1 -Gif trailer-hud -Out "$B/rec" -SourceLog "$LOG" -CutAt '2026-09-25 14:55:14'
```

```bash
pwsh -NoProfile -Command "& ./scripts/shoot.ps1 -Theme BlueGrey -Out '$B/rooms' -SourceLog '$LOG' -CutAt '2026-09-25 15:03:33' -Shot trailer-helper-hunt,trailer-helper-gear,trailer-quests-sky,trailer-world-drops"
```

```bash
pwsh -NoProfile -File scripts/trailer/phone.ps1 -SourceLog "$LOG" -CutAt '2026-09-25 15:03:33' -Out "$B" -Python scripts/trailer/.venv/Scripts/python.exe
```

```bash
ffmpeg -i "$B/rec/trailer-hud.mkv" -t 20 -vf "colorkey=0x2A002A:0.03:0.02,format=rgba" "$B/hud/%05d.png"
```

```bash
ffmpeg -ss 2 -t 17 -i "<gameplay recording>" -vf "fps=30,crop=996:560:450:40,scale=1920:1080,gblur=sigma=26,eq=brightness=0.02:saturation=1.2" -q:v 3 "$B/game/%05d.jpg"
```

```bash
scripts/trailer/.venv/Scripts/python.exe scripts/trailer/score.py "$B/score.wav"
```

```bash
scripts/trailer/.venv/Scripts/python.exe scripts/trailer/plan.py "$B"
```

```bash
scripts/trailer/.venv/Scripts/python.exe scripts/trailer/render.py "$B" EQBuddy-Evolved-Trailer.mp4
```

`render.py --stills 12.5,28.5` writes review PNGs at those times instead of a video.

## Things that cost a take

- **The replay walk.** On launch EQBuddy re-reads the whole log; a 62 MB log is ingested
  12.7 s after launch (`ingestDone=1`, measured). Only the minimized bar is gated on that
  (`ReplayPaintGate`); the expanded panel still walks old sessions (filed separately), so
  the trailer captures keep the widget minimized and settle 45 s.
- **A lingering buff chip** ("Spirit of Wolf line 0:00 est") read as broken for a whole
  take: the Buff chip family is muted the way a player mutes it, not by editing the log.
- **Watch chip and toast are one switch** (`WatchFireLedger` needs `AlertBanner`); the toast
  is parked mid-screen with `AlertLeft`/`AlertTop`, where a player drags it.
- **Pointer rests** must avoid the chip rows: a tooltip over the spawn row lasted 15 s.
- The cut times are Dranak's; a different log needs its own (`-CutAt` just before a fight
  with kills and loot in the next ~30 s).
