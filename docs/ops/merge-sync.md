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
| `.github/workflows/merge-sync.yml` | The trigger: `pull_request: [closed]` filtered on `merged == true`, plus a `workflow_dispatch` replay for a merge this missed. |
| `scripts/merge-sync-linkage.ps1` | The two decisions, with no I/O: which issue does this PR name, and may that issue be closed. |
| `scripts/merge-sync.ps1` | The HTTP: resolve the key to an issue id, PATCH the status, leave the comment. |
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

- **SKIPPED, exit 0** — the PR did not merge; no key; the secrets are absent.
  A repo that has not been given a key yet is not a broken repo.
- **REFUSED, exit 0** — ambiguous body, or a `blocked`/`cancelled` issue. Loud
  but not a failed build: with branch-precedence these only reach PRs whose
  branch named nothing, and reddening dependabot's queue helps nobody.
- **RED, exit 1** — configured but Paperclip is unreachable, or the key names an
  issue that does not exist. Silence here is how a mislabelled branch quietly
  stops syncing forever.

## Turning it on

The job is **inert until three Actions secrets exist** on the repo
(*Settings → Secrets and variables → Actions*), the same pattern as
`helm-back-channel.yml` on `dranakcorps-control-plane`:

| Secret | Value |
|---|---|
| `PAPERCLIP_API_URL` | Paperclip API base. Accepted with or without a trailing `/api`. |
| `PAPERCLIP_API_KEY` | Bearer token for the agent that may PATCH issues. |
| `PAPERCLIP_COMPANY_ID` | Company UUID the issues live under. |

Until they are set, every run prints a `SKIPPED: not configured` line **naming
the missing secrets** and exits 0. Nothing else about the repo changes.

> **The base URL must be one a GitHub-hosted runner can reach.** A tailnet or
> loopback address works from a developer box and fails from a runner. That the
> Helm back-channel already posts successfully from `ubuntu-latest` is the
> evidence a reachable ingress exists; use the same one.

Setting a production secret is not something this lane does on its own — it is
the Founder/ops step that turns the job on.

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

## Why the self-test carries the weight here

This workflow's own trigger fires **only after a merge to the default branch**,
so the pull request that changes it cannot run it — the gate would first execute
on the commit that already landed. `merge-sync-selftest.ps1` is the only part a
PR can actually see, which is why it asserts the decisions rather than the
plumbing, and why `check.ps1` and `ci.yml` both run it.

## Known gap

`-Merged` is a **string**, not a `[bool]`, and the self-test pins it. GitHub
hands the value over as the text `true`/`false`, and in PowerShell a non-empty
string is truthy — so `if (-not $Merged)` would close an issue every time a PR
was closed *without* merging. That mutation was run: it sails past the gate to
`Linked: this PR -> DRA-77`. It is the worst single bug this job could have and
it is one keyword away at all times.
