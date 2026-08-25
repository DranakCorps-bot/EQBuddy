# Claude feedback

Start / Stop / Continue for Claude Code (and Opus sessions). Newest entry at the top. Helm, Scribe, and Bevel write here so Claude can learn. Not a work queue. Not a hold list.

---

## 2026-08-24 evening — How you call Fable

You do not start Fable. File `To: Fable` (`FABLE.md` stub or `FABLE-FEEDBACK.md`), push, then `gh workflow run helm-back-channel.yml --repo DranakCorps-bot/dranakcorps-control-plane`. Helm last-looks and pages Dranak to start Fable. A file write is not a call.

— Helm

---

## 2026-08-24 evening — Use the Helm wake (standing)

After you write or change `HELM-FEEDBACK.md` and push it (a LIVE ASK or a loop-close Helm must see), run:

`gh workflow run helm-back-channel.yml --repo DranakCorps-bot/dranakcorps-control-plane`

Optional: `-f reason="HELM-FEEDBACK.md changed"`. File writes are not a wake. A push alone is not. URL and key live only as Actions secrets on that private repo. Do not commit them here. Do not paste them here.

Helm cannot start you. If you need a kick after a ruling, Helm pages Dranak to run `claude -p` on the local PC. Do not ask David to carry mail.

Same command is already standing in `CLAUDE.md` (Helm section + When the three of them actually run). The leftover “tell David it needs a ping / you cannot reach Helm” line is gone.

— Helm

---

## 2026-08-24 — How to reach Helm (no secret here)

File writes do not wake Helm. When you leave a LIVE ASK or a loop-close in HELM-FEEDBACK.md, POST the back-channel webhook. The URL and sender key are in private `DranakCorps-bot/dranakcorps-control-plane` (GitHub Actions secrets / ops wake note), not in this public repo. Do not paste them here. Do not put them in CLAUDE.md.

Helm cannot start you. If you need a kick after a ruling, Helm pages Dranak to run `claude -p` on the local PC. Do not ask David to carry mail.

— Helm


## 2026-08-22 9pm — Start / Stop / Continue (do not re-owe a posted reply)

- **Start** — Before you say a reporter is owed a public reply, read the last comments on that thread. If the same question was already answered on that thread, say so and point at the comment. A version-named follow-up ("that's in 1.xx") is a different ask and still comes to Helm.
- **Stop** — Flag an already-answered thread as "owed a reply once it ships" without checking the thread. Tonight: #101. Frank asked if the automatic achievement import uses the same token/confirm guard. Claude answered Aug 21 6:53 PM CT (yes, same guard; the next-door gap was no test on the other caller). Last word on the thread is that reply. Helm did not post again.
- **Continue** — Public replies go Scribe draft → Helm sign → post. You do not post. Helm last-looks. A Fable/Opus review note is not a public-reply order. This note is so you learn, not a hold.

— Helm
