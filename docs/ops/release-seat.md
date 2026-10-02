# The release seat (DRA-675)

The Founder, on DRA-675, 2026-10-01: *"make sure future releases aren't bound by me
needing to execute command level scripts. These are not founder level activities in
our ExO."* Plan: [docs/plans/DRA-675.md](../plans/DRA-675.md).

**The GO stays the Founder's.** It is consequence item 2 in CLAUDE.md and the one
hard gate. What moved is the **execution**. The Sr Executor seat runs the release on a
card, and the Founder is never asked to type the script. A seat assignment is not a go,
and neither is a Reviewer PASS. **Signing is unchanged**: `release.ps1` still throws
unless every artifact comes back `Valid` and timestamped.

Runbook row: [pc-change-runbook.md](pc-change-runbook.md), **Release** (Sr Executor
only; Jr and Cursor Executor are banned from signing).

## The procedure (a release card, Sr Executor)

1. **Read the card.** You need all three:
   - a Founder go that names the **version** and the **commit of the latest Reviewer
     PASS** (it may be conditional, e.g. "ship v2.0.3 when the review of `<sha>`
     passes");
   - `origin/main` equals that commit;
   - Fable's release review recorded on the card, or a Founder override that says
     the review has not happened.

   If any of the three is missing, stop: comment, and hand the card to Planner.
   **Never infer a go.** A go on an older card is never cited against a later tag.
2. `git pull`, then re-read `HANDOFF.md` for holds. A release hold binds.
   **Then the PR gate (DRA-723):** run
   `pwsh -NoProfile -File scripts/pr-sweep.ps1 -Release vX.Y.Z` and post its whole output
   on the release card. Do not go on until it prints `GATE: PASS`, which means two things.
   First, every open PR is either merged or excluded from this version with a reason: a PR
   comment `RELEASE-EXCLUDE vX.Y.Z: <reason>`, or `-Exclude '<n>=<reason>'`. Second, no PR
   is an EXCEPTION, meaning a hold naming a release that is already tagged, with no
   `HOLD LIFTED` or `STILL HELD: <reason>` comment dated after that tag. On `GATE: FAIL`,
   stop, comment, and hand the card to Planner. The seat does not write an exclusion
   reason itself; the PR's owner or Planner does. PR #992 is why: it was signed off and
   held "until v2.0.2 is tagged", and it missed v2.0.2 and v2.0.3.
3. **Signing login:** nothing to do by hand. `release.ps1` resolves who signs before
   the build and prints `Signing identity: service principal <appId>` (see
   [Signing login](#signing-login-dra-679) below). If it prints a `WARN: signing
   certificate expires` line, the release goes ahead and you file a Planner rotation
   card. If it throws `neither signing login is available`, stop. File the Founder card
   "Founder: run `az login` (one line)", and set the release card `blocked` on it. The
   seat never runs `az login` itself.
4. Note the run's start time (UTC). Then run
   `pwsh -NoProfile -File scripts/release.ps1 -Tag vX.Y.Z` (from `pwsh`, trap 27).
5. Run `pwsh -NoProfile -File scripts/release-verify.ps1 -Tag vX.Y.Z -Commit <reviewed-sha> -Since <run-start>`
   and paste every row on the card.
6. Post only the reporter replies already **written on the card** and marked as routine
   signed thread replies. Re-read `HANDOFF.md` first, and sign them
   `— Dranak (Claude Code)`. The seat composes nothing: no announcement and no new
   reply (consequence item 3).
7. Close the card with the evidence.

**HARD STOP.** The seat never retries a failed `release.ps1`, blind or otherwise. Run
`release-verify.ps1` to see what DID happen (tag? release? OneDrive?), paste the rows,
and hand the card to Planner. A published release cannot be reverted, because family
widgets offer it within 6 hours.

## Signing login (DRA-679)

Plan and threat model: [docs/plans/DRA-679.md](../plans/DRA-679.md). The Founder
ruled on DRA-677 (option B): *"automatic signing login, please."*

**Who signs, in order:**

1. **The release signing login.** This is a service principal, *EQBuddy Release
   Signer*. It has one role, `Artifact Signing Certificate Profile Signer`, scoped to
   the one certificate profile. Its credential is a **non-exportable, TPM-held**
   certificate in `Cert:\CurrentUser\My` on the Founder's PC. `scripts/signing.ps1`
   reaches it through Az PowerShell (`Az.Accounts`, pinned, restored into `tools\`).
   `artifact-signing-identity.json` (gitignored) holds three identifiers and nothing
   that signs.
2. **The Founder's `az login` session.** This is the fallback, and still a signed path.
3. **Neither:** `Initialize-EqSigning` throws before the build. Nothing ships unsigned.

Each sign excludes every other Azure credential, so the identity that was printed is
the only one that can answer.

**The certificate lasts 12 months.** Inside 30 days of expiry the release warns and
still signs. Once it has expired, the SP is skipped and the `az` fallback is tried.

| Need | Command (run under the Founder's `az` session; the SP cannot manage itself) |
|---|---|
| Make it (once) | `pwsh -NoProfile -File scripts/signing-identity.ps1 -Create` |
| Re-assert the one role row + cert state | `pwsh -NoProfile -File scripts/signing-identity.ps1 -Check` |
| Rotate (yearly) | `pwsh -NoProfile -File scripts/signing-identity.ps1 -Rotate`: new cert appended, proven, then the identity file rewritten, then the old one removed. See the rotation notes below |
| **Revoke (leak or lost PC)** | `pwsh -NoProfile -File scripts/signing-identity.ps1 -Revoke [-RemoveKey]`: role assignment deleted, then the app and its SP. Portal: Entra ID → App registrations → *EQBuddy Release Signer* → Delete |

**What `-Rotate` proves, and what it does not** (DRA-697). Plan §6 step 4 says "first
sign with it". The script proves less than that: the new certificate gets a **token**
for the SP (`Connect-AzAccount`). The role assignment belongs to the SP, not the key,
so a token is the part a rotation can break. The first real **signature** with the new
key is the next release's, and its `Signing identity: service principal` line plus
`release-verify.ps1`'s signature row are where that is seen. If the token proof fails,
nothing names the new key: the identity file, the old key and its credential are left
as they were. Only the new credential and the new local key are left behind, and the
error names both.

**The old credential is removed only on an exact single match.** The script matches
the old thumbprint against `customKeyIdentifier` as hex or as base64 of its bytes,
because the form `az` returns has not been measured. If it finds anything other than
one match, it removes nothing, keeps the old local key, prints every credential's
`keyId` and `customKeyIdentifier`, and exits 2. Record those values on the rotation
card: they are the measurement DRA-697 left open.

The rest of the revocation runbook is plan §7: certificate revocation for files signed
in a leak window, and the residual token life.

**Who can use the key:** any process running as the Founder's Windows user, which
includes every agent seat on this PC. **Who can copy it:** nobody. The boundary is that
it cannot leave the PC, not that only this seat can use it.

## What `release-verify.ps1` asserts

Each assertion prints as its own row. Exit 0 only when every row is `[ OK ]`:

| Row | Passes when |
|---|---|
| `tag` | `git ls-remote origin` has the tag, peeled to `-Commit` |
| `release` | `gh release view` finds it, it is not a draft, and it carries all four assets |
| `onedrive` | the installer, its `.sha256`, and the zip are in `OneDrive\EQBuddyDownload`, written at or after `-Since`. These are the three files `release.ps1` copies there; the zip's `.sha256` is published on GitHub only |
| `sha256` | per artifact: OneDrive = GitHub's asset digest = `dist\` = every published `.sha256` sidecar |
| `signature` | the installer (OneDrive and `dist\`), `dist\publish\EQBuddy.exe` and the `EQBuddy.exe` inside the OneDrive zip are each Authenticode `Valid`, timestamped, and signed by `CN=FlossworksCross-Stitch` |

**A row that says "could not ask origin" or "could not ask GitHub" is never a reason to re-run
`release.ps1`.** It means the remote did not answer, not that the tag or release is missing
(DRA-699). Re-run `release-verify.ps1`; only "is not on origin" or "no GitHub release" is an
answer about the release.

Without `-Since`, the freshness bound falls back to the tagged commit's date, and the
row says so. If origin could not be asked for the tag, there is no fallback: the OneDrive
rows say the bound is unknown and why. Without `-Commit`, the tag row is red. `-Dist` points at the `dist\`
folder of the checkout that ran the release.

`-SelfTest` is offline and runs in `check.ps1` and CI. A named mutant drives every row
red. The hash and signature readers also run against real files: an unsigned temp file,
and PowerShell's own signed `pwsh.dll`.

## The permission layer: measured, not guessed (plan §2)

Measured 2026-10-01 from the Sr Executor seat, in run `41501996` on DRA-678.

- **The probe:** `pwsh -NoProfile -File scripts/release.ps1 -EvolvedLocal -Tag x` ran
  with no prompt and no refusal. It reached `release.ps1:29`'s throw
  (`-EvolvedLocal refuses -Tag`) and exited 1. Nothing was built, tagged or published.
  **Per Challenger condition 1, this clears nothing on its own.** It shows the seat
  reached a throw. It does not show the live `-Tag vX.Y.Z` string being accepted.
- **The adapter:** `claude_local`, `engine: acp`. Paperclip runs
  `claude-agent-acp`, which starts `claude.exe` with
  `--setting-sources=project,local --permission-mode default --permission-prompt-tool stdio`.
  So **the user-wide `~/.claude/settings.json` is not read by this seat at all**, and
  every permission prompt goes over stdio to Paperclip's ACP client.
- **Who answers the prompt:** Paperclip's acpx client. The seat's `adapterConfig`
  sets no `permissionMode`, so `normalizePermissionMode` (adapter-utils
  `acpx-engine/execute.js`) falls to `DEFAULT_ACP_ENGINE_PERMISSION_MODE = "approve-all"`.
  In that mode the client picks the allow option without reading the command
  (`acpx` `live-checkpoint-*.js`: `if (mode === "approve-all") return selectedOrFirst(options, allowOption)`).
  There is no auto-mode classifier on this seat, so the v2.0.2 refusal came from the
  Founder's interactive session and not from this path.
- **Which settings files exist for it**, and why none is the Sr-only home the plan
  asked for:

  | File | Read by the Sr seat? | Who else reads it | Persists? |
  |---|---|---|---|
  | `~/.claude/settings.json` (user) | **No** (`--setting-sources=project,local`) | every interactive session | yes |
  | `<repo>/.claude/settings.json` (project, committed) | yes | every seat, every clone, the Founder's sessions | yes. **Too wide** |
  | the Paperclip clone's main checkout `.claude\settings.local.json` | no (the seat's cwd is its worktree) | Planner, Reviewer, Sr (its `additionalDirectories` name all three) | yes. Too wide, and not read |
  | the card worktree's `.claude\settings.local.json` | **yes** | the Sr seat only | **per card**. Paperclip's `writePaperclipClaudeSettings` re-writes it at every run start and *merges* in existing `allow` rows, but each release card gets a fresh `sr-exec/DRA-<n>` worktree that starts without one |
  | `C:\Users\david\source\EQBuddy\.claude\settings.local.json` (where the Founder pasted `Bash(pwsh -NoProfile -File scripts/release.ps1:*)` for v2.0.2) | no | the Founder's and Dranak's interactive sessions in that checkout | yes. It covers **every** argument of the script |

### The allow rule, as text (NOT written into any settings file)

The seat needs no rule today. Paperclip's `approve-all` answers every prompt. The rule
below is for the day the seat's `permissionMode` is narrowed (`approve-reads` or
`deny-all`). It is exactly one script with one argument shape, plus the read-only
verifier:

```json
"Bash(pwsh -NoProfile -File scripts/release.ps1 -Tag v*)",
"Bash(pwsh -NoProfile -File scripts/release-verify.ps1:*)"
```

**Target file:** `.claude\settings.local.json` in the **release card's own worktree**
(`C:\Users\david\.paperclip\instances\default\worktrees\sr-executor\sr-exec\DRA-<n>\`).
That is the only file the measurement found that the Sr seat reads and no other seat
does. The cost is that it is **per release card**, not standing. A standing, Sr-only
home would be a Paperclip seat-config allow-list, and none was found in the adapter.
That question goes to Planner. The rule is **never** widened into the project file or
the user file to make a run green (plan §7, kill criterion).

**What this means for plan §4 (the Founder paste card):** the paste buys nothing while
the seat runs `approve-all`, and under any narrower mode it would be a per-release
keystroke, which is the thing this card removes. Planner decides whether §4 is filed.
Under Challenger condition 1, the first live publish (D2b) still has to *show* that the
live string was accepted on the seat, in its own run.
