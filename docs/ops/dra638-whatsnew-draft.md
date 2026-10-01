# DRA-638 — `WhatsNew.json` entry and the #710 reply, drafted

**Status: DRAFT, and deliberately NOT in `WhatsNew.json`.** The same idiom as
[dra181-whatsnew-draft.md](dra181-whatsnew-draft.md). The Founder pulled #710 out of
`2.0.2` on 2026-09-30 and moved it to `2.0.3`. `<Version>` is still `2.0.2` and untagged, so
this line goes in the `2.0.3` entry when `<Version>` moves. It must not go in `2.0.2`, and
the PR that carries the code must not merge before `v2.0.2` is tagged.

**Reporter credit:** TheOneGargoyle, Discussion #710 ("SK Shrouds of Pain & Hate").

---

## The design ruling, in one paragraph (DRA-638)

A stat-steal is **two spells** on eqlwiki: the detrimental on the mob (Shroud of Hate:
`Detrimental`, `Single`) and a **recourse** on the caster (`Shroud of Hate Recourse`:
`Beneficial`, `Self`, "The buff component of the Shroud of Hate ATK siphon"). What a Shadow
Knight wants to watch is the recourse, and that is an ordinary own buff. It raises no
values-line question and needs no new gate. `buffs-harvest.py`'s `beneficial()` already
admits it, and it is already timed in `BuffDurations.json` under `Hatred fuels your arms.`
(600 s). Three things kept it out of sight, and each has its own owner:

1. **The Watch picker asked the wrong producer.** It listed names from fade lines whose
   *category* is beneficial, and the fade harvest marks every multi-spell line `Other`.
   That hid **119 buffs EQBuddy already times** (Heroism, Shield of the Magi, Avatar,
   the Lich line, Shield of Thorns…). The picker now also asks
   `BuffDurationCatalog.IsBuffSpell`, the one producer of "is this a buff"
   (`FadeMessageCatalog`). This is the code change. The **Buff filter** ("any buff faded")
   still reads the line category, because changing what fires an alert is a separate call.
2. **The recourse's NAME is wrong on the wiki.** The `Shroud of Hate Recourse` page says
   `spellname = siphon strength recourse`. That is a template copy-paste, the same defect
   as the 24 rows in `scripts/harvests/eqlwiki/spellname-mismatch-notes.md`, and the remedy
   is wiki-first (Helm, 2026-08-31, PR #256). After the wiki fix, the next weekly refresh
   names it `Shroud of Hate Recourse` in both catalogs with no code change. Until then the
   picker shows it folded into `Siphon Strength Recourse`, which already matched the line.
3. **Shroud of Pain has no recourse page on eqlwiki at all.** There is nothing to admit,
   and EQBuddy invents no line (trap 73). Whether the caster's log prints
   `The pain subsides.` when the AC drops has not been measured.

**What works today, with no release:** a fade rule typed `Shroud of Hate` or
`Shroud of Pain` (By name) already fires on `The hatred departs.` / `The pain subsides.`,
because both catalog entries list the shroud as a candidate. This is pinned in
`SpellTrackingTests.ATypedShroudRuleFiresOnTheShroudsFadeLine`.

---

## Line for the `2.0.3` entry

> **The Watch rule picker now lists 119 buffs it used to hide.** When two spells share one
> wear-off message, for example Heroism and Heroic Bond ("Your heroism fades."), the
> "By name…" list in Options → Watch rules left them all out, even though EQBuddy times
> every one of them. They are listed now. Rules you already have are unchanged. Thanks to
> TheOneGargoyle (#710), whose Shadow Knight shroud question turned this up.

---

## Drafted reply to Discussion #710: NOT POSTED

Posting needs a fresh `git pull`, a re-read of `HELM.md` (the one place holds live) for a
hold on #710, Helm's posture signature on this text (a `HELM.md` commit or a PR review),
and the release that carries item 1 being out (or the reply rewritten so it promises no
date).

> Thanks for this, and sorry it took a while.
>
> **You can set this up today.** In Options → Watch rules, add a spell-fade rule and type
> `Shroud of Hate` or `Shroud of Pain` in the name box instead of picking from the list.
> The rule fires on the shroud's wear-off message, "The hatred departs." or
> "The pain subsides.". Typing the name already works for this. The list just didn't offer
> these names.
>
> **Why the list left them out.** On eqlwiki each shroud is two spells: the one on the mob,
> and a "recourse" buff on you that holds the attack or AC you took. The buff on you is
> the one worth watching. For Shroud of Hate, eqlwiki has that page, but its name field says
> "siphon strength recourse", copied from another spell. So EQBuddy names it that way too.
> If you'd like to fix it, open
> https://eqlwiki.com/index.php?title=Shroud_of_Hate_Recourse&action=edit, change the
> `spellname =` line to `Shroud of Hate Recourse`, and save. EQBuddy picks it up on its next
> weekly refresh. Shroud of Pain has no recourse page on the wiki yet.
>
> **One question back:** when your Shroud of Pain wears off, what line does your log print?
> If it's "The pain subsides.", the typed rule above already catches it. If it's something
> else, paste it here and we'll add it.
>
> Your question also turned up a bigger gap: the list was hiding 119 buffs EQBuddy already
> times, because they share a wear-off message with another spell. That's fixed for an
> upcoming release.
>
> — Dranak (Claude Code)
