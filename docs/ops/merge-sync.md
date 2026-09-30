# Paperclip merge-sync

DRA-77 / M0-4. Governing plan: DRA-73 rev 2 (SS3.2-3 + SS8.5).
`exo-experiment: merge-sync`.

**What it does.** When a pull request merges, the Paperclip issue its branch
names moves to `done`, and a comment on that issue records which PR did it.

**Why it exists.** Closing the card was a separate act from merging the PR, and
nothing tied the two together, so issues sat in `in_review` for days after the
work was on `main` (observed: EXO-HARDEN-A2, EQ-V2-HOME-CATCHUP). The board then
reads as busier than the work actually is, which is the metric this experiment
is judged on.

**One way, GitHub → Paperclip.** Nothing writes back to the PR: no label, no
comment, no status check. `merge-sync-selftest.ps1` scans `merge-sync.ps1` for
GitHub writes and reddens if one appears. A two-way sync has a loop to design
and M0 does not need one to stop the drift.

## The pieces

| File | Job |
|---|---|
| `scripts/merge-sync-poll.ps1` | The trigger, **on the Founder's machine** (DRA-528): reads merged PRs with `gh pr list` (read-only) past a persisted watermark and hands each to `merge-sync.ps1`, oldest first. |
| `scripts/merge-sync-linkage.ps1` | The decisions, with no I/O: which issue does this PR name, may that issue be closed, is the API address private, and which merged PRs are still pending past the watermark. |
| `scripts/merge-sync.ps1` | The HTTP: refuse a public API address, resolve the key to an issue id, PATCH the status, leave the comment. |
| `scripts/merge-sync-selftest.ps1` | Drives every refusal into the red once, with the legitimate spelling beside it. Runs in `check.ps1` and in CI. |

## Linkage: the branch wins

The key is `DRA-<n>`, case-insensitive, with or without a separator, so the
branch spelling `dra77` and the body spelling `DRA-77` are one card.

**The branch is checked first, and if it names a key the body is never read.**
This is not a tie-break detail, it is the whole rule: every PR body in this repo
carries a `Governing plan: DRA-73` line, so a scan that pooled branch and body
would close the parent plan issue on every single merge.

The body is the fallback for a branch that names nothing. If the body names
**more than one** key, the job refuses rather than picking the first — one-way
sync means a wrong `done` has no undo path from here. Put the key in the branch
name and the ambiguity never arises.

## Dispositions: what a merge may and may not do

| Current status | On merge | Why |
|---|---|---|
| `backlog` `todo` `in_progress` `in_review` | → `done` | The work landed. `in_review` is the drift the card was filed for. |
| `done` | no-op | A merge can be replayed; idempotent is what makes that free. |
| `blocked` | refused | A merged PR does not clear a blocker, and stamping `done` would hide one. |
| `cancelled` | refused | A human decided this should not happen. Reversing that is their call. |

The three lists are asserted to **partition** Paperclip's status enum — totally
and disjointly. A status Paperclip adds later reddens the self-test rather than
falling through to a silent guess.

## Red, or merely loud?

The split is deliberate, because a sync job that fails open everywhere recreates
the drift it was built to fix.

- **SKIPPED, exit 0** — the PR did not merge; no key; the Paperclip variables
  are absent from the environment.
- **REFUSED, exit 0** — ambiguous body, or a `blocked`/`cancelled` issue. Loud
  but not a failed build: with branch-precedence these only reach PRs whose
  branch named nothing, and reddening dependabot's queue helps nobody.
- **RED, exit 1** — configured but Paperclip is unreachable, the key names an
  issue that does not exist, or the API base resolves to a **public** address.
  Silence here is how a mislabelled branch quietly stops syncing forever.

## Where it runs, and why not on GitHub (DRA-528)

Until DRA-528 this ran as `.github/workflows/merge-sync.yml` on a GitHub-hosted
runner, with the board key in this public repo's Actions secrets. That needed
the control-plane API reachable from GitHub's cloud, and a secret in a repo
whose pull requests are machine-generated is only as private as the least
careful branch. The workflow is gone, and two guards keep it gone:

- `merge-sync-selftest.ps1` reddens if **any** workflow passes a `PAPERCLIP_`
  secret to a runner, whatever the file is called.
- `merge-sync.ps1` refuses to send a request when the API host resolves to
  anything outside loopback, RFC 1918, the tailnet's 100.64.0.0/10, or IPv6
  unique-local/link-local. Every resolved address is checked, not the first.

**The poller** runs on the Founder's machine, the same boundary as the rest of
the control plane: `gh pr list` (a READ with the Founder's own `gh` login), then
`merge-sync.ps1` against `127.0.0.1` or the tailnet. The PR fields travel as
arguments; the key travels in the inherited environment, never on a command
line.

- **Watermark, persisted** (trap 85) at
  `%LOCALAPPDATA%\DranakCorps\merge-sync\state.json`: the newest `mergedAt`
  handled, plus the PR numbers handled at that exact instant, so two PRs
  merged in one second are both handled. It advances one PR at a time, and
  only after `merge-sync.ps1` exits 0.
- **First run seeds, never replays.** No state file means the watermark is set
  to now (or to `-Since`) and nothing is handled. Replaying history would
  re-close any card a human reopened after its PR merged.
- **A red PR holds the queue.** The pass stops, the watermark stays behind the
  failing PR, and the next pass retries it, so an outage heals itself. A PR
  that can never succeed stays loud until someone passes `-Skip <number>`,
  which is printed.
- **Complete or nothing.** If `gh` returns a full `-Limit` page, the pass
  refuses rather than handle a list that might be truncated.
- One pass per machine, enforced by a named mutex.

## Turning it on (Founder step)

Set the three variables **in the environment of the scheduled task, on the
Founder's machine**, never as GitHub secrets:

| Variable | Value |
|---|---|
| `PAPERCLIP_API_URL` | Paperclip API base on loopback or the tailnet, with or without a trailing `/api`. |
| `PAPERCLIP_API_KEY` | Bearer token for the agent that may PATCH issues. |
| `PAPERCLIP_COMPANY_ID` | Company UUID the issues live under. |

Seed, then schedule a pass every ten minutes:

```powershell
pwsh -NoProfile -File scripts/merge-sync-poll.ps1 -Since '<last merge the old workflow handled, ISO 8601>'
$a = New-ScheduledTaskAction -Execute 'pwsh' -Argument '-NoProfile -File "<clone>\scripts\merge-sync-poll.ps1"'
$t = New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Minutes 10)
Register-ScheduledTask -TaskName 'DranakCorps merge-sync' -Action $a -Trigger $t
```

Then delete `PAPERCLIP_API_URL`, `PAPERCLIP_API_KEY` and `PAPERCLIP_COMPANY_ID`
from the repo's Actions secrets. Removing the workflow does **not** make them
safe: any same-repo branch can add a workflow that reads them. That step is
the Founder's, like any change to the control plane.

## Checking it without merging anything

```bash
# Decide and print; makes no request. Needs no secret.
pwsh -NoProfile -File scripts/merge-sync.ps1 -Branch claude/dra77-merge-sync-20260914 -Merged true -DryRun

# Every refusal, driven into the red once. No network, no secret, no event.
pwsh -NoProfile -File scripts/merge-sync-selftest.ps1
```

With the Paperclip variables exported, the same script runs for real against an
issue that is already `done`, which exercises the whole read path — base
normalization, auth, lookup, disposition — and writes nothing:

```bash
pwsh -NoProfile -File scripts/merge-sync.ps1 -Branch claude/dra78-exo-metrics-20260914 -Merged true
# SKIPPED: DRA-78 - already 'done' - nothing to do (replaying a merge must stay free).
```

```bash
# The poller, deciding without writing: merge-sync gets -DryRun, the watermark stays put.
pwsh -NoProfile -File scripts/merge-sync-poll.ps1 -DryRun
```

## Why the self-test carries the weight here

The poller runs on one machine, after a merge, so the pull request that changes
it cannot run it against the board. `merge-sync-selftest.ps1` is the only part a
PR can actually see. It asserts the decisions and drives the poller end to end
against a fixture list and a throwaway state file, with the live Paperclip
variables cleared for the child. That is why `check.ps1` and `ci.yml` both run
it.

## Known gap

`-Merged` is a **string**, not a `[bool]`, and the self-test pins it. GitHub
hands the value over as the text `true`/`false`, and in PowerShell a non-empty
string is truthy — so `if (-not $Merged)` would close an issue every time a PR
was closed *without* merging. That mutation was run: it sails past the gate to
`Linked: this PR -> DRA-77`. It is the worst single bug this job could have and
it is one keyword away at all times.
