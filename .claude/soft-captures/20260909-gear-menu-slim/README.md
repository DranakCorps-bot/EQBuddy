# gear-menu-slim (DRA-25) — the four shots that prove the cut landed

Taken 2026-09-08 evening CT against `claude/gear-menu-slim-implementation-20260909`,
after `dotnet build EQBuddy.slnx -c Release`. Committed rather than left on one
machine on purpose: the previous Soft shots for this lock live at
`C:\Users\david\source\EQBuddy\.claude\soft-captures\20260908-owner-qa\`, which is a
path nobody but that machine can open, so a reviewer had to take the description on
trust. These four have a recipe and are in the repo.

**The recipe, verbatim:**

```
pwsh -NoProfile -Command "& ./scripts/shoot.ps1 -Shot @('options-behavior','shell-progress-history','shell-quests','widget-expanded') -Out .claude/soft-captures/20260909-gear-menu-slim"
```

`-Theme` is left at its default (`Turquoise`, the standing owner lock). Note that
`-Shot` with several names needs `pwsh -Command` and a real array — under `-File`
the whole list arrives as ONE string and `shoot.ps1` throws `Unknown shot '...'`.

## What each one is evidence FOR, and what was predicted before it was opened

| Shot | The claim it settles | Read against the picture |
|---|---|---|
| `options-behavior.png` (420×499) | Click-through is the FIRST row of Options → Behavior; the four surviving `Data & imports` rows are a `Data` group; the three surviving `Help` rows are a footer under every tab | All three. Row 1 is "Click-through (game clicks pass through)". "Data" heading carries Wiki contribution pack… / Review an archived log… / Choose log folder… / Auto-detect log folder. Footer reads "EQBuddy v2.0.0 · Quick tutorial… · Check for updates" — the version line is the website door, which is why the WhatsNew entry names it that way rather than as "EQBuddy (website)" |
| `shell-progress-history.png` (946×633) | `Session history…` has a real door, and the sentence that used to name the cut menu row was rewritten in the SAME change (trap 20) | Both. The button says "Open the full History studio…"; the line above it now ends "— the button below opens it", not "right-click the EQBuddy widget" |
| `shell-quests.png` (946×633) | Part B: the rail says **Quest**, the window title still says **Guide**, and the two achievement rows landed on the Guide room | All three in one frame. Title bar "EQBuddy — Guide", rail row "Quest", and "Import achievements…" / "Copy /outputfile achievements" sit above the checklist |
| `widget-expanded.png` (338×994) | The title-bar doors the cut leans on still exist: the ✉ that replaces `Send feedback…` and the ✏ that replaces `Edit HUD` | Both present, beside the cog |

## What these shots deliberately do NOT show

**The expanded gear menu itself.** It is a `ContextMenu` popup, and `shoot.ps1`
captures a window by title (trap 24) — a popup has no title to match, so there is no
honest way to stage it here. The machine-checkable proof of the four rows is
`WidgetMenuTests` (reads `MainWindow.xaml` against `WidgetMenuPolicy`) and
`ShellHostTests.TheMinimizedAndExpandedWidgetMenusBothShowExactlyTheFourDoors`, which
launches the real app. The owner's own screen grab
(`20260908-owner-qa/expanded-gear-menu-screen.png`) is the human-eye counterpart and
is what this change was written against.
