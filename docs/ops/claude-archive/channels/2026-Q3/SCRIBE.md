# Scribe inbox - archived entries (2026-Q3)

**Immutable. Nothing here is live, and nothing here is a work queue.**

Rotated out of `SCRIBE.md` on 2026-09-20 by DRA-229 (DRA-144 F4b), under DRA-26
plan rev 3 section 5. Entries moved **verbatim**: no rewording, no re-dating, no
line-break changes.

**The cut is a triage, not a date cut.** `SCRIBE.md` is an inbox whose own contract
is "take an item, then delete it", so entries were partitioned by their `Priority`
field rather than by age. Archived here: `someday`, `taken`, `done`, and the
terminal dispositions (`FIXED-shipped`, `BUILT`, `CLOSED`, `ANSWERED`, `ROUTED`,
`MOVED`). **No open ask was archived at any age** - every entry whose Priority is
`must-fix`, `approved`, `authorized`, `authorized-next`, `open` or `waiting` is
still in the live file, including `waiting` entries dated back to 2026-08-19.

An archived entry never revives a hold and never commissions work. If you are
looking for something to do, the live inbox is `SCRIBE.md`.

**Do not append here.** Append to the active file at the repo root.

---
### SK "watch buff list" — Shroud of Hate + Shroud of Pain missing from the buff list (add-request)

- **Priority:** `someday` (real ask, catalog addition — not authorized; Scribe intake only). Not must-fix (nothing breaks; a shadowknight simply isn't tracking these two spells).

- **Place (hypothesis, wiki-grounded):** the buff-durations / watch-buff catalog lane — `src/EQBuddy.Core/Data/BuffDurations.json` (ships "Shroud of Undeath", "Shroud of Death", "Shroud of the Spirits", but NOT "Shroud of Hate" nor "Shroud of Pain"), fed by the eqlwiki buffs harvest in `scripts/harvests/eqlwiki/` (`buffs-harvest.py`, `buffs-report.md`, plus the wiki wikitext already cached in-tree). Surface touched: the buff card / watch list (`BuffsCardView`, `BuffTracker`, `docs/WatchListGuide.md`). **Not** a parse break (the log lines will already land like any other spell), **not** a debuff-tracking item (the two *spells* as cast on a target are `Detrimental`; the requester wants the buff side tracked — "Shroud of Hate Recourse" exists on eqlwiki as the buff component; "Shroud of Pain Recourse" 404s — the benefit is inherent in the 10-minute spell). **Do not fold into** #690 (Banestrike achievements), #679 (chest-mote capture), #435 (merge-flags), #165 (bag-flags), or the Reddit items above.

- **Wiki check (wiki-first, this run 2026-09-19):** [eqlwiki.com/Shroud_of_Pain](https://eqlwiki.com/Shroud_of_Pain) — Shadow Knight level 50, "Covers your target in a mass of darkness that steals their armor class and gives it to you for 10 min", duration 10 minutes. [eqlwiki.com/Shroud_of_Hate](https://eqlwiki.com/Shroud_of_Hate) — Shadow Knight level 35, ATB/ATK siphon, "Recourse: Shroud of Hate Recourse", duration 10 minutes. [eqlwiki.com/Shroud_of_Hate_Recourse](https://eqlwiki.com/Shroud_of_Hate_Recourse) — "The buff component of the Shroud of Hate ATK siphon", Shadow Knight level 39, 10 minutes. [eqlwiki.com/Shroud_of_Pain_Recourse](https://eqlwiki.com/Shroud_of_Pain_Recourse) — 404 (no recourse page; benefit is the AC transfer to the caster). Spell names + levels from the wiki, not guessed.

- **Source:** #710 TheOneGargoyle Sep 19, 8:07 PM CT (2026-09-20 01:07 UTC). <https://github.com/DranakCorps-bot/EQBuddy/discussions/710> — New thread. 0 comments. Footer: `EQBuddy 1.99.18 · Windows 26200`. u/Dranak75 not involved.

- **Ask (verbatim, the whole entry):** "Love this app. The watch buff list doesn't seem to contain the Shadowknight spells Shroud of Hate and Shroud of Pain - any chance we can add them please ?" (plus the version/device footer above).

- **Already shipped / Checked (origin/main, this run 2026-09-19):** `src/EQBuddy.Core/Data/BuffDurations.json` grep: "Shroud of Undeath", "Shroud of Death", "Shroud of the Spirits" present; **no** "Shroud of Hate", **no** "Shroud of Pain" → the reporter's observation holds on tip. `scripts/harvests/eqlwiki/buffs-report.md`: 360 buffs across 207 landing lines; neither spell named anywhere (not in the catalog, not in the "Excluded" section, not in shared-landing lines). The three relevant wiki pages are already cached in-tree: `scripts/harvests/eqlwiki/cache/Shroud of Hate.12036ea6.wikitext`, `Shroud of Pain.c9d04a83.wikitext`, `Shroud of Hate Recourse.bd3849de.wikitext` — the harvest has seen them. Unchecked this pass: whether the in-app watch *editor* lets a user add a custom buff rule that would cover these today — hypothesis, unverified; the reporter's "watch buff list" phrasing most plausibly means the default/auto-tracked catalog. Not confirmed against a running app.

- **Hypothesis (label as such):** the gap is upstream in the eqlwiki buffs harvest / the BuffDurations.json generation (both spells are 10-minute Shadow Knight buffs with wiki pages already cached), i.e. a catalog entry (or two), not UI work. Whether the "Recourse" pages or the base spells are the tracked landing lines is an implementation decision — Scribe does not assert either.

- **Class:** V0 (catalog content in an existing trackable lane; no new code asserted). Do not write FABLE.md from Scribe.

- **Holds re-read (this run, 2026-09-19):** live Holds block empty at last known state; process notes stand (new-thread thank-yous come to Helm before any post; no promise of review/fix beyond "captured and sent on for review"). Reddit stays harvest-only. Talking to u/TheOneGargoyle is fine if, and only if, Helm posts.

- **Scribe 2026-09-19 8:1x PM CT (cron intake):** New EQBuddy intake — first new GitHub community item above the #690/#679 baseline. Do not implement. Do not write FABLE.md. Do not open the work. Thank-you drafted below for Helm QA/post — NOT auto-posted.

- **DranakCorps-bot thank-you (draft, for Helm QA/post — do not post without Helm. No promises, dates, pricing, or ToS.):**

  > Hi TheOneGargoyle — thank you for the heads-up on Shroud of Hate and Shroud of Pain. Captured and sent along for review.

  > — EQBuddy team



### Banestrike achievement tracking (Untapped Potential / General / Tradeskill / Slayer / EQL)



- **Priority:** `someday` (real ask, not this gate) — **not authorized** (new thread; no code opened yet — Scribe intake only).

- **Place:** Achievements surface — EQBuddy surfaces individual event lines (`You have completed achievement: …` appears in the reporter's #679 haul block) but has no persistent per-achievement progress card today. Neighbourhood: #235 "Import achievements button does not function" (import/entry flow, different ask) and `docs/WatchListGuide.md` (watch/alerts on loot + motes; not the same thing). **Do not fold into #435 / #165 / #235 / #679 (#679 is a loot-parse gap, different surface).**

- **Source:** #690 FatGuyGamin Sep 19, 3:51 AM CT (2026-09-18 08:51 UTC). https://github.com/DranakCorps-bot/EQBuddy/discussions/690 — New thread. Category: Ideas. 0 comments. Footer: `EQBuddy 1.99.18 · Windows 26200`. u/Dranak75 not involved.

- **Ask (verbatim, the whole entry):** "I would love the ability to track achievement progress in regards to Banestrike. I've downloaded that log but it is damn near impossible for my old man brain to make much sense out of. Like the Untapped Potential, General, Tradeskill, Slayer, & Everquest achievements."

- **Already shipped (checked on origin/main, this run 2026-09-19):** `LogParser.cs` recognises `You have completed achievement: <name>` as a session event line; `Motes.cs` handles the `Mote of X Potential` family across the loot stream. **Not grepped this pass:** whether an achievement-progress model or a Banestrike-specific card already lives in `src/EQBuddy.UI.*` on tip — treat as *unchecked* and confirm before coding. No Banestrike-specific surface is visible in origin/main.

- **Hypothesis (label as such):** the shape is (a) a persistent per-achievement tally across sessions keyed off the `completed achievement: …` lines, with the Banestrike achievement *categories* the reporter named (Untapped Potential / General / Tradeskill / Slayer / Everquest) as the axis, and (b) a card/surface that reads it — the reporter's framing ("downloaded that log but it is damn near impossible to make sense out of") is an aggregation/display ask, not a parse ask. Banestrike category structure is game-truth; eqlwiki is the lane for any category/list copy (wiki-first). Not to be conflated with #235 (import-button failure) or with watch/alerts.

- **Class:** V1–V2 (new persisted achievement-progress lane + surface + categorization). Do not write FABLE.md from Scribe.

- **Off-topic here:** none reported.

- **Holds re-read (HELM.md this run, 2026-09-19):** Live Holds block is empty; only process notes (new-thread thank-you still comes to Helm; promise of review/fix comes to Helm before it posts). No retired hold applies (not #208 / #228 / #226 / #231). Talking to FatGuyGamin is fine.

- **Scribe 2026-09-19 02:20 AM CT (cron intake):** New intake. Do not implement. Do not write FABLE.md. Do not open the work. Do not fold into #435 / #165 / #235 / #679. Thank-you drafted below for Helm QA/post — not auto-posted.

- **DranakCorps-bot thank-you (draft, for Helm QA/post — do not post without Helm. No promises, dates, pricing, ToS.):**

  > Hi FatGuyGamin — thank you for the request and for naming the Banestrike categories you want to track. Captured and sent on for review.
  >
  > — EQBuddy team



### Reddit: eql-gearbot-plus — guild gear-donation + crafting/gathering work-order Discord bot (wiz3n, harvest-only)

- **Priority:** someday (harvest; not authorized. Do not reply on Reddit.) Competitive context; third-party tool + poster, not a reporter filing into our repo — **no DranakCorps-bot thank-you drafted and no Reddit reply** (harvest-only pattern; cf. SEQO / jbenga item-ID DB / Character-Sheet-Planner / 3D-map shelf).
- **Place:** competitive context around the Inventory / Gear + crafting-work-order area, **not** an EQBuddy surface. Shape is a *guild / guild-Discord* management workflow (gear donation + crafting / gathering work orders + eqlwiki autocomplete/verification + inventory-dump GUI) — community/guild lane, not a player-session surface. Do **not** fold into the #435 batch (merge-flag inventory trio) or #165 (bag flags) — different ask (guild work-order tracking with eqlwiki autocomplete), not player inventory. Not an eqlwiki-first SUGGEST (third-party tool *cites* eqlwiki as its verification source; that is our shared game-truth lane they build against).
- **Source:** u/wiz3n r/EQLegends 2026-09-17, 2:38 PM CT (17:38 UTC). https://www.reddit.com/r/EQLegends/comments/1wj0qof/bot_website_to_manage_giving_getting_and_making_items_in_eql/ Harvest-only. 4 comments at harvest (2026-09-17 sweep window). Public repo: https://github.com/wizen/eql-gearbot-plus (verified exists 2026-09-17; JavaScript; created 2026-09-17 00:54 UTC; 1 star; pushed 2026-09-17 16:51 UTC — "EverQuest Legends Discord bot & companion website to manage donated gear and crafting/gathering work orders"). u/Dranak75 not in the thread. Poster handle "wiz3n" rhymes with GitHub reporter "wizen" (discussions #189 / #190) — *hypothesis only, do not assert identity.*
- **Ask:** none directed at EQBuddy. OP verbatim: "Pseudonym from Rivervale, here. I've made something helpful for people who manage a guild / guild discord. [https://github.com/wizen/eql-gearbot-plus] Basically a guild gear donation and crafting / gathering work order management system, with autocomplete and verification via eqlwiki, and a companion website that allows you to donate straight from your inventory dump via gui. Enjoy!"
- **Comments (harvest colour):** u/AM_86 "But why.gif"; u/SuperHooligan "Who needs donations in the easiest game ever?"; u/Rat_Rat "Omg - someone made a tool to help others. Quick internet - shit on it because you don't find it immediately useful!"; OP follow-ups: "For the moment you have a point, but I'm sure (gods, I hope) they'll add in more stuff." and "Besides, it's more than donations. It's work orders, so ppl can say they need 800 tumpy tonics or 667 fruit or whatever. Just a way to record who has what and who needs what." All in-thread community colour; no EQBuddy-named help; no DranakCorps-named help.
- **Already shipped / checked:** EQBuddy's inventory / catalog / quest-flag lane (reads the same `/outputfile inventory` dump; wiki-first on item truth; quest badges on the Loot card) is the in-app shape; the #435 merge-flag trio and #165 bag-flag ask are the closest in-app inventory shapes — all `someday / Not authorized`. The specific "guild donation + crafting/gathering work-order + eqlwiki autocomplete + inventory-dump GUI" shape has **no direct EQBuddy counterpart** today; it is a community/guild-management lane, not a player-session lane.
- **Checked:** harvested 2026-09-17 via arctic-shift (post body + 4 comments) + repo metadata from `repos/wizen/eql-gearbot-plus`. No comment, vote, or message. Did not open, clone, or run the bot/website; did not install anything; did not visit the companion site beyond the README title.
- **Holds re-read (HELM.md this run — 2026-09-17 ~6:10 AM CT tip, PR #663 DRA-164 SIGNED):** Live Holds empty. Play Console OFF. Soft-leave on Reddit replies unchanged (harvest-only unless Helm/David authorize). Do not fold into #435 / #165 / #243. No FABLE.md. No implement. Do not start Claude. Thank-you: **none drafted** — competitive-context self-promo, not a GitHub reporter; no DranakCorps-bot public reply on r/EQLegends without Helm authorization.

### Reddit: UI - Recommendations (cashsusclaymore, harvest-only)

- **Priority:** someday (harvest; not authorized. Do not reply on Reddit.)
- **Place:** in-game UI context, not an EQBuddy surface. OP is asking for a better in-game UI / minimap ("Maybe with mini map ?") and in their second line names us by mistake: "I'm getting the feeling there's no UI pack like eq buddy or companion ?" — they mean an in-game UI pack; EQBuddy is the local log/overlay tool, not an in-game UI pack. In-thread community answers already resolved it: `/load` the modern UI, in-game map packs (Good's, Bewall's), and the spinips UI pack. Do not fold into any EQBuddy item; do not treat as an EQBuddy ask.
- **Source:** u/cashsusclaymore r/EQLegends Sept 14, 6:01 AM CT (11:01 UTC). https://www.reddit.com/r/EQLegends/comments/1wgx61u/ui_recommendations/ Harvest-only. 8 comments at harvest (2026-09-16 13:31 UTC); OP resolved in-thread ("Yeah. Something like that. Tyvm.", 2026-09-16 13:31 UTC). Thread names "eq buddy" in passing; u/Dranak75 not in the thread.
- **Ask:** none directed at EQBuddy. OP verbatim: "My ui looks like junk, I'm not inspired to fix it myself. Am I able to find a better smoother ui ? Maybe with mini map ?" and "I'm getting the feeling there's no UI pack like eq buddy or companion ? Maybe I'm asking wrong like my layout sucks."
- **Comments (harvest colour):** u/LazyTruth147 9-15 8:16 AM CT: "The default UI is beyond bad. If you haven't done it, do /load and pick the modern UI."; u/OneOf8 9-15 10:34 AM CT: in-game map packs (Good's / Bewall's, Drive links); u/danceofjimbeam 9-16 7:59 AM CT: "I'm using https://github.com/itsspin/spinips and am liking it" (third-party UI pack, community, not a DranakCorps artifact — do not link on our side); u/Grammeton 9-16 10:46 PM CT: chat-window / font / transparency tips. All community in-game-UI advice; no EQBuddy-named help.
- **Already shipped / checked:** none applies — in-game UI is game territory, not EQBuddy's. EQBuddy's map (2D, /loc dot) and the "map should show facing" ask are the closest in-app neighbourhood; do not fold.
- **Checked:** harvested 2026-09-16 via arctic-shift (post body + all 8 comments). No comment, vote, or message. No thank-you draft — OP resolved in-thread before any reply was warranted; nothing to thank.


### Reddit: Character Sheet Planner thread (bonkedagain33, harvest-only)

- **Priority:** someday (harvest; not authorized. Do not reply on Reddit.)
- **Place:** competitive context around the Inventory / Gear surface, not an EQBuddy surface. A third-party "EQ Legends Character Sheet Planner" tool with in-thread gear-weighting discourse. Different shape from EQBuddy's in-app inventory + catalog (wiki-first on item truth). Do not fold into the #435 batch (merge-flags / best-per-slot / vendor-trash) or any in-app surface; do not treat as an EQBuddy ask.
- **Source:** u/bonkedagain33 r/EQLegends Sept 14, ~8:19 AM CT (13:19 UTC). https://www.reddit.com/r/EQLegends/comments/1wg384k/eq_legends_character_sheet_planner/ Harvest-only. Score 4, 2 comments at harvest (2026-09-16). Thread does not name DranakCorps; u/Dranak75 not in the thread.
- **Ask:** none directed at EQBuddy. OP: "My question is gear weighting. Do you use their genetic gear weighting or do you adjust a bit to make more attributes more important. Like raising AC and lowering something else? TLDR: What attributes are most important and which are secondary or even ignored. Currently a warrior, Shaman, Necro."
- **Comments (2):** nucleardemon searches the wiki manually per slot, keeps near-full suits of planar armor and "running around collecting exalts"; alytle uses the top 3/5 as a starting point but "mostly just decide[s] for myself." Community gear-weighting discourse, no EQBuddy-named reply.
- **Already shipped:** EQBuddy's inventory / catalog area is the in-app territory (wiki-first on item truth); the #435 batch (merge-flags, gold-star best-per-slot, vendor-trash with keep gates) is the closest in-app shape and remains `someday` / `Not authorized` as of 2026-09-16.
- **Checked:** harvested 2026-09-16 via arctic-shift (post + 2 comments). No comment, vote, or message. Did not open or test the planner tool; did not visit the planner's site.

### Reddit: two open loot/inventory filter tools (jbenga: item-ID DB + /outputfile inventory cleaner, harvest-only)

- **Priority:** someday (harvest; not authorized. Do not reply on Reddit.)
- **Place:** competitive context around the Inventory & Loot area, not a new EQBuddy surface. The Inventory Cleaner shape overlaps the #435 batch (Inventory: flag items that can merge into one stack/slot; best-per-slot; vendor-trash) — do not fold into it, this is a third-party tool, not a reporter. Item-ID mapping is catalog territory (wiki-first: eqlwiki holds the item names; a public ID→name table is not an EQBuddy ask). Do not treat as an EQBuddy bug or feature ask.
- **Source:** u/jbenga r/EQLegends Sept 15 ~8:48 PM CT. https://www.reddit.com/r/EQLegends/comments/1whjne9/built_a_free_communitysourced_item_id_database/ Harvest-only. Score 1 at harvest; the post's comment count field read 0 but the comments endpoint held four short in-thread comments (community churn talk; one commenter pointing to loadoutlegends.com as an EQL-specific item-ID database — community signal, not the OP's site). Thread does not name DranakCorps; u/Dranak75 not in the thread.
- **Ask:** none directed at EQBuddy. OP wants contributors to upload EQL loot-filter exports to crowd-source an open item-ID → item-name/icon-ID DB (trust by confirmation count, full JSON/CSV export, no API key, refreshed daily; "no account info, no game play automation"). Second tool, Inventory Cleaner: free read-only browser parser of the game's /outputfile inventory export — duplicate-by-ID, "confirmed merge candidates" vs "+0 dues", projected merge XP/tier.
- **Already shipped:** EQBuddy reads the same /outputfile inventory dump and ships the catalog (wiki-first on item truth); the #435 merge-flag trio and #165 bag-flags are the in-app versions of this shape, all `someday` / `Not authorized` as of 2026-09-16.
- **Checked:** harvested 2026-09-16 via arctic-shift (post + comment list). No comment, vote, or message. Could not copy more thread signal — comments 0 at harvest. Did not open or test either tool.

### Reddit: SEQO (Simple EQ Overlay) — re-harvest on 2026-09-17 post with full body (harvest-only)

- **Priority:** `someday` (real ask, not this gate) — **harvest-only**; no reply on Reddit. Not authorized; Scribe intake only. Do not treat as an EQBuddy ask.
- **Place:** competitive context, overlay/companion chain (cf. EQLegends Advisor entry, cashsusclaymore loot-filter tools, eql-gearbot-plus context, jbenga loot-filter tool, foraern 3D map app). Overlaps in claimed surface with EQBuddy items that are already filed under `someday` / `waiting` / or closed: #120 (configurable buff sets with missing-buff indicator — "Camp timers … buff fade" and "Unlock tracker" in SEQO touch this surface), #94 (attack-speed debuff alerts with cure/dispel — "charm break" alerts in SEQO touch this surface), #208 (chips and alerts on a different monitor — SEQO is a separate overlay that claims "Never touches the game or its files … read the log file"; different shape, but same neighbourhood), #217 (wiki-contribution pack — SEQO data lineage cites eqlegendstools / Alanna's race unlock guide / Manlaan's epic checklist), #109 (raid-instance spawn timers), #435 (merge-flag inventory), #165 (bag/inventory quest-vs-junk flags), #227 (standalone Motes card), #159 (never delete log data). Do **not** fold this competitive-context entry into those in-app asks — this is a third-party overlay, not a request to EQBuddy, and the author explicitly disclaims competition.
- **Source:** u/Extension-Chair-7250 r/EQLegends **Sept 17, ~3:00 PM CT (2026-09-17 20:00 UTC)** — a second / refresh post with a full body, following the initial Sept 15 post (1whn7xz) that harvested body-removed. https://www.reddit.com/r/EQLegends/comments/1wj4m84/i_built_an_overlay_for_myself_with_a_dps_meter/ . Score 0, **2 comments** at 2026-09-19 re-harvest. u/Dranak75 not involved. Harvest-only; nothing posted back.
- **Ask (verbatim, the whole entry):**

>  Be gentle with me, I don't get anything out of this — I built it for myself and figured others might find it useful. I am not trying to promote or compete with others, just wanted something tailored to my needs.
>  
>  It's called SEQO (Simple EQ Overlay). It never touches the game or its files. All it does is read the log file the game already writes to disk, the same way GamParse and nParse worked for 20 years of classic EQ. No injection, no memory reading, no automation. If you type /outputfile inventory, achievements and faction in game it also reads those files to keep your progress current on its own.
>  
>  What it does:
>  * DPS meter with per-fight and session views, spell and proc breakdowns, and pet damage tracked separately
>  * Loot tracking with real drop rates from your own kills, a keep/junk/sell advisor, and a run tracker for instances (start it, clear, end it - coin, motes and every drop counted with coin per hour)
>  * Unlock tracker for races, classes and deities with live faction progress bars and the fastest known grind for each
>  * Epic checklists for every class with the exact Legends item names, checked off automatically from your bags, bank and key ring
>  * Best in slot lists for all 16 classes with a check on everything you already own
>  * Plane of Sky quest tracker - all 95 turn-ins, items check off as you loot them
>  * World map with route planning, druid ring / wizard spire markers, and nearest-port info
>  * Camp timers with placeholder support, plus rare spawn, charm break, buff fade and AFK alerts
>  * Syncs between your computers through Dropbox/Drive if you play on more than one
>  
>  It's fully open source so you can review every line of code before you run it, and I'm actively maintaining it - the last update went up this week. Link is in the comments.
>  
>  The data comes from work by the eqlegendstools site, Alanna's race unlock guide and Manlaan's epic checklist on the wiki. Go support them.
>  
>  Happy to answer questions or take feature requests.

- **Comments (verbatim, all 2):**
  - u/GrendeL- (2026-09-17): "Where’s the link ?"
  - u/hrethnar (2026-09-17): "Cool!   In before comments about AI slop."
  - **No repo / GitHub / source-code link captured from either comment.** The author says "Link is in the comments" but neither harvested comment contains one. If the link appears later, re-harvest and update this entry.
- **Already shipped (checked on origin/main, 2026-09-19):** EQBuddy reads the same /outputfile inventory dump and ships the catalog (wiki-first on item truth); the #435 merge-flag trio and #165 bag-flags are the in-app versions of the loot / merge / bag-flag shape; #235 + #101 are the achievement-import / achievement-marking flows; #165 and #173 cover several of the same surfaces. None is this overlay as a product.
- **Checked:** re-harvested 2026-09-19 via arctic-shift (post 1wj4m84 + comment list, 2 comments). No comment, vote, or message. Repo link **still not captured** from either comment — will re-harvest if it appears. Did not open or test the tool. No ToS, pricing, dates, or promises implied.

### Reddit: 3D EQ Legends Map App, Apple Silicon (foraern, harvest-only)

- **Priority:** someday (harvest; not authorized. Do not reply on Reddit.)
- **Place:** competitive context on the Map surface, not a new EQBuddy surface. EQBuddy's map is a 2D tile map with the /loc dot and facing; a 3D dungeon viewer is a different shape. Nearby "Map should show facing, not only a /loc dot" is an existing EQBuddy ask — do not fold.
- **Source:** u/foraern r/EQLegends Sept 9, 10:00 AM CT (15:00 UTC). https://www.reddit.com/r/EQLegends/comments/1wbo085/3d_eq_legends_map_app_apple_silicon/ Harvest-only. Score 35, 24 comments at harvest (2026-09-16).
- **Ask:** none directed at EQBuddy. OP built an AI-generated 3D map for navigating "some of the more confusing dungeons"; Mac only today, "can easily port it to three.js so it's available on other systems." OP is asking whether it's of interest and (in-thread mods) clarifying the attached recording is the real app.
- **Already shipped:** EQBuddy map (2D, /loc dot) and the facing ask are the in-app territory; nothing in EQBuddy today claims 3D.
- **Checked:** harvested 2026-09-16 via arctic-shift. No comment, vote, or message. Did not open or run the app; comment thread not deep-harvested (no EQBuddy-named thread at harvest). u/Dranak75 not in thread.

### Bag / inventory window — "what is this item" flags for quest vs junk (the loot-window treatment, in your bags)

- **Priority:** someday (real ask, no auth, no player-facing break, no log needed). Not authorized. Soft leave.

- **Place (hypothesis):** Inventory / Gear & Loot — the existing Inventory window's item list (`LootSurface` / `InventoryView` already reads the game's `/outputfile inventory` dump and shows bags and bank by container) + the catalog's existing quest-relevance flag (which is what puts the quest badges on the Loot card you're describing adjacent to this). The ask is not new data — it's surfacing badges the Loot card already has, in a second surface. Not wiki / not eqlwiki-first (this is a personal-inventory display ask, not a catalog / game-truth miss).

- **Source:** EQBuddy discussion #165 (themadpoet-dotcom), Aug 15 ~9:38 AM CT (14:38 UTC). Category: Ideas. New thread. https://github.com/DranakCorps-bot/EQBuddy/discussions/165 — reporter on EQBuddy 1.86.0, Windows 26200. One reply in-thread from DranakCorps-bot (Aug 15 ~5:11 PM CT / 22:11 UTC) scoped the ask before I file it; quoted below as the "Bot reply" block.

- **Ask (verbatim, reporter's own words):** "it would be nice to have a way for it to tell us what an item is in our bags - ie quest/trash etc like it does in the loot window - not sure if this is possible but even if we had to link it for that to work it'd be awesome so I could sort through the stuff I just picked up without checking on the last session"

- **Ask (scoped):** Inventory-window item list should carry the same "this is quest-relevant / this is junk" markers the Loot window already does. Reporter wants to sort freshly-looted / bagged items without going back to the Loot card or to a previous session; the "like it does in the loot window" clause is the ask's own shape — no new data, just a second surface catching up.

- **Already shipped / checked:**

  - Inventory / Loot window split is live (⚙ → Inventory…, the container split, the quest-link popups) — this item's "Already shipped" line in the bot reply is accurate as of Aug 15; not a gap.

  - Loot card already quest-badges items from the catalog — reporter is explicitly pointing at this ("like it does in the loot window") so the catalog flag is not the ask.

  - Nearby (do not fold):

    - #104 "Able to tell you BiS from what is in your storage/bank/inventory" (Aug 12 — the same "what is this item / what does it do" shape asked from a *different* angle — BiS / gear score, not quest/junk flags).

    - #435 batch (Aug 8 — the inventory "flag this item" trio: merge-into-stack / best-per-slot / vendor-trash flags). All three are the same "badges on the Inventory window" shape, but from different angles, and all three are already `someday / Not authorized` in the file. Do not collapse #165 into any of them, but flag that they share the same surface.

- **Bot reply (DranakCorps-bot, Aug 15 ~5:11 PM CT / 22:11 UTC — quoted for Helm QA, not for the file):** "Good idea, and closer than you'd think — the pieces are already there, just not wired together. The Inventory window (⚙ → Inventory…) already reads the game's `/outputfile inventory` dump and shows your bags and bank by container. And EQBuddy's item catalog already knows which items are quest-relevant — that's what puts the quest badges on the Loot card you're describing. So this is mostly a matter of showing the badges the Loot card already has in the Inventory window too, which is exactly the shape of change I like: no new data, no new guessing, one surface catching up with another. Two things I'd want to get right: \"Trash\" is a judgement, not a fact. EQBuddy can say \"no known quest use, no recipe, not wearable\" — it shouldn't say \"trash\", because a vendor-value item you're saving for a turn-in you haven't started isn't trash. Same reason the Gear Locker says \"outclassed\" rather than \"sell this\". Would \"no known use\" work for you, or do you actually want a sell/keep verdict? Freshness. The inventory comes from a dump file you generate with `/outputfile inventory`, so it's a snapshot rather than live. If EQBuddy's badging your bags it should say how old that snapshot is, or you'd be sorting against yesterday's bags. Meanwhile, today: the Inventory window is the fastest way to see everything you're carrying without a session, and clicking an item name anywhere in EQBuddy opens its info popup with quest links. Not as good as badges in place, but it beats checking the last sessio[n]"

- **Hypothesis (label as such):** the surface already parses `/outputfile inventory` (InventoryFile.cs) and the catalog already has the quest-relevance flag (Loot card uses it); the change is surface-only. If Helm signs, this is a V1 (one line of UI / one flag re-surfaced across one surface).

- **Class:** V1 (surface parity, no new data / no new guess). Do not write FABLE.md.

- **Thank-you draft (DranakCorps-bot, for Helm to QA/post) — DO NOT auto-post. No dates, no pricing, no ToS, no promises:**



  > Thanks for filing this — and for the "like it does in the loot window" clause, which is the clearest way to say it. I've captured the ask and sent it along for review: Inventory window (⚙ → Inventory…) carries the same quest-relevant / no-known-use flags the Loot card already shows, on your bags and bank. Two honest notes I'll fold in: "trash" would be a judgement, not a fact (a vendor-value item you're saving for a turn-in you haven't started isn't trash), so the flag will say "no known quest use / no recipe / not wearable" rather than a sell/keep verdict. And the bags come from a `/outputfile inventory` snapshot, so the flag will sit next to how old that snapshot is — you won't be sorting against yesterday's bags without knowing it. In the meantime, the Inventory window is already the fastest way to see what you're carrying without a session, and clicking any item name opens its info popup with quest links.



  *(Draft ends — leave for Helm to QA/post as DranakCorps-bot. Scribe does not auto-post public replies.)*

- **Holds re-read (HELM.md this run):** Live Holds empty. Play Console OFF. Soft LEAVE list unchanged — nothing here touched. #243 / #435 / #104 do-not-fold noted; this item stands alone.



---



### Inventory dump → item DB / quest-flag match (not Excel export)

- **Priority:** someday (real ask; not authorized). Core “flag quest affiliation from `/outputfile inventory`” is largely in EQBuddy’s shipped lane; the reporter’s remaining gap is an **item database / name-match** dump (or eqlwiki-first shared DB), not Excel export of bags. Soft leave unless owner opens.

- **Place:** Inventory / Quest Tracker — personal bag+bank from the game dump. Player inventory, not shared eqlwiki-first SUGGEST. Not a group meter. Not #208. Nearby: #243 Sky leftover audit (shipped path), #435 inventory flags (someday) — do not fold; different asks.

- **Source:** Reddit r/EQLegends u/medullah Sep 9, 8:10 AM CT (13:10 UTC). https://www.reddit.com/r/EQLegends/comments/1wbl5sk/any_exportable_database_of_items_or_easy_way_to/ Title: “Any exportable database of items or easy way to look at an inventory list for quest items/stats?” Harvest-only (no Scribe reply).

- **Ask (scoped):** An exportable **item database** (names matchable automatically against an inventory list) and/or a path that flags which quest(s) each carried item belongs to. Reporter already auto-imports `/outputfile inventory` into Excel — the gap is not “EQBuddy should export my bags to Excel.” Title ask is the DB/match half; in-app quest-flag from the dump remains related but largely shipped.

- **Reporter clarification 2026-09-09 ~1:22 PM CT (flip this run):** “Yep I have that, and currently have it set to automatically import into an excel document. I was hoping to find a database dump that I can match the item names automatically.” (comment id p8smp9u)

- **Reporter example (verbatim colour, not a second Ask):** Elemental Binder from the Hole → Magician Epic (not in game yet); wants to hold it for a possible Kunark-era epic — future-expansion hold is context, not a request that EQBuddy invent unreleased quest data.

- **Thread colour (own lines, not the Ask):**

  - u/walletinsurance: “Literally what EQ companion and eq buddy do”

  - u/Tamalor: points at Companion App + wiki link button

  - u/AppleBottmBeans: Codex/Claude + `/output inventory` × eqlwiki (third-party workflow; not ours)

  - u/medullah follow-up (earlier): “couldn’t find a way to get a dump of all my items, just look at them one at a time” → discoverability of the in-game `/outputfile inventory` command / EQBuddy’s read path (see separate line below; **flipped done for this reporter**)

  - u/walletinsurance ~1:12 PM CT: `/outputinventory`; open bank + dragon horde; pull runes out of currency storage to show in tracking

  - u/Dranak75 (David) ~2:00–2:04 PM CT already pointed at `/outputfile inventory` — do not file; no Scribe Reddit reply

- **Already shipped (quoted local WC `src` this run):** `InventoryFile` summary: game’s `/outputfile inventory` dump parsed “into base-name → count so the quest tracker can answer \"what could I turn in with what I’m already carrying\"” (`src/EQBuddy.Core/InventoryFile.cs`). Prior SCRIBE/#241 path: inventory dump trues Quest Tracker have-counts to bags+bank (1.99.14). EQBuddy is log-local — not a web app you upload a dump into (prior SCRIBE note). Latest tag still `v1.99.18`.

- **Checked:** Reddit post + nested comments via arctic-shift. WINDOW/WIDGET/PHONE — no. Grepped `InventoryFile.cs` header on David’s PC WC. **No** grepped Excel/CSV “inventory + quest affiliation export” surface on main `src` this run (unchecked against every export path outside Inventory/Loot/Quest-named files).

- **Hypothesis, unchecked against live Quests UI:** have-counts and turn-in readiness are shipped; a player-facing “this item → these quest names” export or spreadsheet may not be. Discoverability of `/outputfile inventory` is no longer the miss for this reporter (they have Excel auto-import).

- **Place note (hypothesis):** “exportable database of items” is shared game truth → first place-option is paste-ready eqlwiki / world item DB before EQBuddy hosts a dump. Personal bag matching stays player-side. Bar to SUGGEST higher than show. Soft leave. Do not fold #243 / #435.

- **Class:** V0–V1 for discoverability / docs; V1 only if owner later opens an EQBuddy-hosted item DB (not the default). Do not write FABLE.md.

- **Holds re-read (HELM.md this run):** Live Holds empty. Talking on Reddit is still harvest-only unless Helm/David authorize a reply.

- **Scribe 2026-09-09 ~1:10 PM CT (cron intake):** New Reddit intake. Do not implement. Do not open the work. Do not reply on Reddit.

- **Helm 2026-09-09 ~1:16 PM CT:** SIGNED someday / Soft leave. No Reddit reply (thread already named EQBuddy). Do not fold into #243 / #435. Weekend Evolved Guide work is a different lane.

- **Scribe 2026-09-09 ~6:05 PM CT:** Reporter answered; narrowed Ask off Excel-export framing. Priority still someday / Soft leave. No implement. No Reddit reply.

- **Helm 2026-09-09 ~6:15 PM CT:** SIGNED flip — Ask = item DB / name-match (not Excel export). Discoverability line done for this reporter. Soft leave / someday. No Reddit reply (u/Dranak75 already in-thread). Not needs-david. Soft LEAVE inventing implement or Claude kick from this alone. Open `#491` is Guide UX (BOT_ONLY) — different lane; Soft LEAVE folding medullah into it.



### Inventory: flag items that can merge into one stack/slot

- **Priority:** someday (real ask, not this gate; not authorized)

- **Place:** Inventory / Gear & Loot — personal bag/slot view. Player inventory, not shared game truth / eqlwiki. Not a group meter. Not #208.

- **Source:** #435 TobyCatVA Sep 8, 1:51 AM CT (06:51 UTC). https://github.com/DranakCorps-bot/EQBuddy/discussions/435 New thread. Category: Ideas. 0 comments. Footer: EQBuddy 1.99.18 · Windows 26200. Three asks in one post — this is ask 1 of 3.

- **Ask (verbatim, ask 1):** "Flagging inventory items which can be merged together, instead of occupying multiple inventory slots."

- **Already shipped (quoted main `src` this run):** `InventoryFile` parses `/outputfile inventory` into per-location `Entry` rows (Location / Name / Count) and also folds base-name → count for quest turn-in math; stack counts are honored when aggregating. `InventoryView` has by-slot and by-container renders. **No** grepped `Mergeable` / merge-flag UI symbol on main `src\EQBuddy*.cs` this run.

- **Checked:** DISCUSSION body via GraphQL. WINDOW/WIDGET/PHONE — no. Grepped main `src\EQBuddy.Core\InventoryFile.cs` + `InventoryView.cs` headers/symbols only (not full inventory UX pass).

- **Hypothesis, unchecked against live bag UI:** dump already knows per-slot rows of the same stackable; the ask is a visible flag when two+ occupied slots could collapse into one stack, not a new dump parser.

- **Class:** V0–V1 (inventory flag on existing dump rows). Do not write FABLE.md.

- **Holds re-read (HELM.md this run):** Live Holds empty. Talking to TobyCatVA is fine.

- **Scribe 2026-09-08 ~5:16 AM CT (cron intake):** New intake. Do not implement. Do not open the work. Thank-you draft below covers all three #435 asks — one reply.

- **Helm 2026-09-08 ~5:35 AM CT:** SIGNED thank-you (mailbox retrospective ACK of Soft ~5:30 tip); land SCRIBE; Soft leave (someday).

- **Replied:** 2026-09-08 ~5:31 AM CT (Scribe) https://github.com/DranakCorps-bot/EQBuddy/discussions/435#discussioncomment-18347525 — SIGNED thank-you covering all three asks. Do not edit posted comments.



### Inventory: gold-star best-owned equippable piece per slot type

- **Priority:** someday (real ask, not this gate; not authorized)

- **Place:** Inventory / gear compare — personal owned+equippable ranking. Player gear, not shared eqlwiki BIS list (wiki-feed bar for SUGGEST stays higher). Not a group meter.

- **Source:** #435 TobyCatVA Sep 8, 1:51 AM CT. https://github.com/DranakCorps-bot/EQBuddy/discussions/435 Same Ideas thread. Ask 2 of 3.

- **Ask (verbatim, ask 2):** "A gold star for BIS(that you currently own and can equip in each slot for each type. Basically giving the player feedback as to what is the best the player currently owns: best 1 hand blunt, best 2 hander, best 1 hand slash... this is based off of stats or ratio or ratio with stats as tie breakers."

- **Already shipped:** Inventory/Gear Locker surfaces exist; tip text on Inventory mentions swap/vendor framing. **No** grepped owned-BIS / gold-star / BestOwned marker on main `src` this run. Global wiki BIS is a different Place (shared game truth → eqlwiki first).

- **Checked:** DISCUSSION body. WINDOW/WIDGET/PHONE — no. Symbol grep on main Inventory/Gear sources only.

- **Hypothesis, unchecked:** rank among items the dump says you own and that are equippable for this character/class/slot, using ratio/stats with stats as tie-break — scoped to owned, not server-wide BIS.

- **Class:** V1 (owned-equippable rank UI) — if it needs a new cross-cutting gear-score model, bump Class later; do not write FABLE.md from Scribe.

- **Scribe 2026-09-08 ~5:16 AM CT:** New intake. Do not implement.

- **Helm 2026-09-08 ~5:35 AM CT:** SIGNED; Soft leave (someday).



### Inventory: $ vendor-trash flag with keep gates (craft / low-level quest)

- **Priority:** someday (real ask, not this gate; not authorized)

- **Place:** Inventory flag / vendor advice — personal bag. Related-but-not-same as #226 global client-side ignore of vendor trash (ignore ≠ $ flag with keep rules). Not shared eqlwiki. Not a group meter.

- **Source:** #435 TobyCatVA Sep 8, 1:51 AM CT. https://github.com/DranakCorps-bot/EQBuddy/discussions/435 Same Ideas thread. Ask 3 of 3.

- **Ask (verbatim, ask 3):** "A dollar sign icon showing us the vendor trash with configurable gates like keep if used in X?X?X?X crafting, or keep if used in quests recomended for level 20 and under."

- **Already shipped:** Inventory tip mentions “what to vendor”; #226 follow-up asked global ignore for vendor trash/gems (different control). **No** grepped VendorTrash / dollar-sign keep-gate control on main `src` this run.

- **Checked:** DISCUSSION body. WINDOW/WIDGET/PHONE — no. Prior SCRIBE #226 vendor-trash ignore note only.

- **Hypothesis, unchecked:** needs item-tag data (craft ingredient / quest use / level band) plus a player-configurable keep gate; without those sources the $ flag would be a static trash list only.

- **Class:** V1–V2 borderline if craft/quest keep rules need new data sources — leave Class V1 for the icon+gate shell; do not write FABLE.md from Scribe.

- **Scribe 2026-09-08 ~5:16 AM CT:** New intake. Do not implement.

- **Helm 2026-09-08 ~5:35 AM CT:** SIGNED; Soft leave (someday). Do not fold into #226.



### mobile pairing link uses ethernet IP, not Wi-Fi

- **Priority:** **FIXED-shipped 2026-09-04** in `v1.99.18` (PR #286 `3b6fff2f`; tag `dbcfb3a1`). Wi-Fi tiebreak + pairing address picker. Item was taken/deleted at build; re-noted here for the shipped-status loop only.

- **Source:** #264 brhanson2-cyber. https://github.com/DranakCorps-bot/EQBuddy/discussions/264

- **Ask:** pairing QR used ethernet IP; wanted to force Wi-Fi.

- **Replied:** 2026-09-04 ~2:23 PM CT Helm-signed shipped-status (DranakCorps-bot): https://github.com/DranakCorps-bot/EQBuddy/discussions/264#discussioncomment-18294975 — prefers Wi-Fi and you can pick; in the latest release (`v1.99.18`).



### bonus XP weekend line not parsed (with a bonus)

- **Priority:** **FIXED-shipped 2026-09-04** in `v1.99.18` (tag `dbcfb3a1`). On main via PR #274 (`XpRx`); public tag/release cut. Loop closed on the thread with Helm-signed shipped-status.

- **Place:** log XP parse / Experience session rates. Player session. Not shared game truth / eqlwiki. Not a group meter. Not #208 mobile sounds. Not #264 pairing (same reporter, different ask — do not fold).

- **Source:** #273 brhanson2-cyber Sep 4, 8:18 AM CT. https://github.com/DranakCorps-bot/EQBuddy/discussions/273 New thread. Footer: EQBuddy 1.99.17 · Windows 26200.

- **Ask:** "The bonus exp weekend has modified the xp message and eqbuddy is not registering any exp today."

- **Reporter evidence (nested reply ~9:08 AM CT):** `[Fri Sep 04 09:04:24 2026] You gain experience (with a bonus)! (3.200%)`

- **Off-topic follow-up (own line, not thread colour):** after the second thank-you, reporter: "Awesome response on your side. Have a great long weekend!"

- **Already shipped:** origin `main` `LogParser` XpRx is now `^You gain (?<party>party )?experience(?: \(with a bonus\))?!(?: \((?<pct>[\d.]+)%\))?$` (PR #274 https://github.com/DranakCorps-bot/EQBuddy/pull/274 merged 2026-09-04T15:07:40Z). Local WC may still show the old regex until pull — quote origin, not stale WC.

- **Checked:** DISCUSSION body + nested log paste. ORIGIN main LogParser XpRx (via contents API). Not checked against a live bonus-weekend session after the merge.

- **Replied:** 2026-09-04 ~9:03 AM CT (capture thank-you, asked for a paste) and ~9:55 AM CT (thanks for the exact line) — both DranakCorps-bot on #273.

- **Replied:** 2026-09-04 ~2:23 PM CT Helm-signed shipped-status (DranakCorps-bot): https://github.com/DranakCorps-bot/EQBuddy/discussions/273#discussioncomment-18294977 — "You gain experience (with a bonus)!" counted again; in the latest release (`v1.99.18`).

- **Class:** V0-V1 (one regex). Do not write FABLE.md.

- **Scribe 2026-09-04 1:16 PM CT:** Mid-window miss — opened after 5 AM catch-up; Claude already fixed before this 1 PM run. Backfill only so SCRIBE matches evidence. No new thank-you. #208 untouched by this item.



### Server Status Widget (new feature ask)

- **Priority:** someday (real ask, not this gate; not authorized)

- **Place:** a NEW transparent / persistent companion widget surface — server status + maintenance updates. Player session, not shared game truth. NOT a catalog / quest-data item (the wiki-first / eqlwiki rule is not triggered by the ask itself). Near the Instance charges timer on the widget and the Progress window, but this is a NEW server-status surface — distinct ask, not a timer on the existing widget. Do not fold into the Instance charges timer item. Not #208 mobile sounds.

- **Source:** #262 Bigman397 Sep 1 14:28 UTC (~9:28 AM CT). https://github.com/DranakCorps-bot/EQBuddy/discussions/262 New thread. 0 replies. Footer: EQBuddy 1.99.16 - Windows 26200.

- **Helm 2026-09-01 1:10 PM CT:** Thank-you signed. Waiting, not authorized. Do not implement. Do not write FABLE.md. Do not fold into Instance charges. #208 untouched.

- **Replied:** 2026-09-01 ~1:12 PM CT https://github.com/DranakCorps-bot/EQBuddy/discussions/262#discussioncomment-18238169

- **Ask (verbatim, the whole entry):** "A transparent widget to show server status and maintenance updates would be sweet so I can pretend to work while waiting for the servers to return <3 Thanks for the hard work, this tool is awesome" Single paragraph plus the client-footer; that is everything the reporter wrote.

- **Already shipped:** no server-status or maintenance widget. EQBuddy ships an updater (one-hop releases) and a What's-new panel; neither surfaces server status. No server-status claim was made on-thread this run; not verified against a running widget.

- **Hypothesis, unchecked (labeled):** "server status and maintenance updates" is most likely EQL server up/down plus the maintenance-window wording. The upstream data source for server status is NOT identified and was NOT harvested this pass (do not open third-party status sites). Treat it as a new-surface ask, not a data-source / integration ticket, until Helm opens the product call.

- **Scribe 2026-09-01 07:20 AM CT:** New intake. Real feature ask, not a gate-blocked bug, not authorized. HELM.md Holds re-read: live hold #208 is do-not-open on mobile sounds (sbaum23) — not this reporter, not this ask; talking to Bigman397 is not the hold, opening the work is. Not #208. Do not implement. Do not write FABLE.md. Do not open the work. Do not fold into the Instance charges timer item. Thank-you drafted for Helm's sign-off — NOT posted.



- **Thank-you draft (for Helm's sign-off — DRAFT, NOT POSTED):**

  > Hi Bigman397 — thanks for the note, and the "pretend to work while waiting for the servers to return" line did earn a smile. A transparent server-status widget is a fun idea. I've captured it and sent it along for review — I can't promise a date on it, but it's logged and in front of us. And thanks back for the kind words.



### Debuff + hot-ready tracking "similar to GINA" (new feature ask)

- **Priority:** someday (real ask, not this gate; not authorized)

- **Place:** companion tracking surface — combined debuff (slow / disease / poison / stun) + hot-ready (cooldown) display. Player session, not shared game truth. NOT a catalog / quest-data item (so the wiki-first / eqlwiki rule is not triggered). Near the slow chip / SpokenAlerts / SlowDebuffCatalog (#94) and the #237 slow-% thread, but this is a NEW GINA-style combined debuff+hot-ready ask, not a fix to the existing slow chip. Do not fold into #94 / #237. Not #208 mobile sounds.

- **Source:** #261 ebaboyy Sep 1 11:43 UTC (~6:43 AM CT). https://github.com/DranakCorps-bot/EQBuddy/discussions/261 New thread. 0 replies. Footer: EQBuddy 1.99.16 - Windows 19045.

- **Helm 2026-09-01 1:10 PM CT:** Thank-you signed. Waiting, not authorized. Do not implement. Do not write FABLE.md. Do not fold into #94/#237. #208 untouched.

- **Replied:** 2026-09-01 ~1:12 PM CT https://github.com/DranakCorps-bot/EQBuddy/discussions/261#discussioncomment-18238167

- **Ask (verbatim, the whole entry):** "Debuff and Hot tracking similar to GINA." Single-line body; footer only. That is everything the reporter wrote.

- **Already shipped:** slow-debuff chip + optional voice (#94, SlowDebuffCatalog) shows one debuff type (slow), not a combined debuff + hot-ready panel. Hypothesis, not verified against a running widget — a claim, not a fact.

- **Hypothesis, unchecked (labeled):** "GINA" is the reporter's reference to an external companion; NOT verified and NOT harvested into (do not open third-party sites). "Hot tracking" is most likely hot-ready (cooldowns). Not treating "GINA" as an established EQBuddy surface.

- **Scribe 2026-09-01 06:50 AM CT:** New intake. Real feature ask, not a gate-blocked bug, not authorized. Not a hold. Not #208. Do not implement. Do not write FABLE.md. Do not open the work. Thank-you drafted for Helm's sign — NOT posted.



- **Thank-you draft (for Helm's sign-off — DRAFT, NOT POSTED):**

  > Hi ebaboyy — thanks for sending this in. The debuff + hot-ready idea, and the GINA reference, are a really helpful way to point us at what you're picturing, and it's exactly the kind of thing the tracker is for. I've captured it and sent it along for review. It's now in the team's queue — I can't promise timelines or a fixed date on my end, but it's logged and in front of us. Thanks again for the note.



### cards reset to Gear & loot + Motes

- **Priority:** **FIXED-shipped 2026-09-04** in `v1.99.18` (PR #285 `a223c628`; tag `dbcfb3a1`). Hidden cards stay hidden. Loop closed with Helm-signed shipped-status.

- **Place:** widget card visibility / Options Cards & windows. Player session. Not shared wiki. Nearby #250 motes scroll; #227/#228 motes card restore — same theme area, not the same report (reset-after-hide vs stretch/scroll). Do not fold. #208 mobile sounds — not this (reporter wants existing mez alerts + DPS, not new mobile sound work).

- **Source:** #252 TiconaX Aug 29/30 night CT. https://github.com/DranakCorps-bot/EQBuddy/discussions/252 New thread. Category: as filed. 0 replies. Footer: EQBuddy 1.99.15 · Windows 26200.

- **Ask:** "The cards always reset to having 2 cards open even though I have hidden all of them. Gear & loot and + Motes. I just need the DPS and sound alerts for mez dropping."

- **Already shipped:** Options card show/hide; starred motes restore path; mez/DPS alerts exist.

- **Checked:** GitHub discussion body via harvest. Not grepped settings keys this pass.

- **Helm 2026-08-30 5:20 AM CT:** Signed. Waiting, not authorized. Thank-you may post as written. Do not fold into #227/#228/#250. #208 untouched.

- **Replied:** https://github.com/DranakCorps-bot/EQBuddy/discussions/252#discussioncomment-18207017

- **Replied:** 2026-09-04 ~2:23 PM CT Helm-signed shipped-status (DranakCorps-bot): https://github.com/DranakCorps-bot/EQBuddy/discussions/252#discussioncomment-18294976 — cards you have hidden now stay hidden; in the latest release (`v1.99.18`).

- **Replied:** 2026-09-04 ~2:29 PM CT Helm-signed clarification (DranakCorps-bot): https://github.com/DranakCorps-bot/EQBuddy/discussions/252#discussioncomment-18295056 — if Gear & loot / Motes (or Progress) already came back on an older build, hide them once more in Options → Cards & windows and it will stick; `v1.99.18` stops the reset but does not undo a restore that already happened.



### expand / minimize cursor miss starts a new session

- **Priority:** **BUILT 2026-08-26 (Claude), staged in 1.99.12.** Your hypothesis was

  exactly right (eqbuddy-fb verified it in source before Helm authorized; the fix built to

  it): the swap only changed visibility, Left stayed put, the right edge travelled by the

  width delta. The window now anchors its RIGHT edge across the swap in BOTH directions —

  both bars put the toggle second from right, so click-click toggling keeps the cursor on

  the pair. Arithmetic in `WidgetMetrics.RightAnchoredLeft` (trap 1; Avalonia converts its

  physical Position), verified on the real exe by the new `scripts/mode-swap-verify.ps1`,

  whose FIRST run caught the anchor computing before `UpdateMiniChips` — the mini bar's

  width IS its chips, which is also why the miss magnitude was content-dependent and read

  as habitual rather than universal. Helm's constraints honoured: no public reply posted;

  the post-ship status reply will go to Helm for sign-off. disberon credited in What's-new.

- **Helm 2026-08-26 6:37 AM CT:** Loop-close accepted. No public reply until 1.99.12 is tagged. Then the status draft comes here.

- **Place:** WIDGET. Mini dashboard expand vs the full title bar (Settings / Start a new session / Minimize). Not a pop-out window. Not the phone. Not Gate 5 overlay. Not a group meter. Not PR #238 / unreleased 1.99.11 pop-out resize.

- **Source:** #239 disberon Aug 25, 6:33 PM CT. https://github.com/DranakCorps-bot/EQBuddy/discussions/239 New thread. 0 replies. Footer: EQBuddy 1.99.10 · Windows 26200.

- **Ask:** "Can you make it so when you hit the expand button, it stays aligned with the minimize button? Right now when I hit expand the cursor is over settings/start new session and habitually I may just click again to minimize and instead i click start new session."

- Thank you.

- **Already shipped:** both controls exist on 1.99.10. Mini bar: Expand (`OnRestore`, tooltip "Expand (or double-click)") then Close (`MainWindow.xaml:78–81`). Full title bar, left to right after the character name: feedback, Mobile, Settings (`GearBtn`), Start a new session (`ResetButton`), Minimize to dashboard (`OnMinimize`), Close (`MainWindow.xaml:159–172`). Latest tag is still v1.99.10.

- **Checked:** WIDGET chrome in `MainWindow.xaml`. I could not check window / phone. I did not run the binary, so I did not watch the cursor after expand. No screenshot.

- **Hypothesis, unchecked against a running widget:** after expand, MiniRoot hides and NormalRoot’s title bar is wider, so the same cursor spot is no longer over Minimize — it lands on Settings or Start a new session.

- **Class:** V0–V1 likely (localized layout / hit-target). Do not write FABLE.md.

- **Off-topic here:** none reported.

- **Replied:** 2026-08-25 (Scribe) https://github.com/DranakCorps-bot/EQBuddy/discussions/239#discussioncomment-18154931

- **Replied:** 2026-08-26 (Scribe) v1.99.12 right-edge fix https://github.com/DranakCorps-bot/EQBuddy/discussions/239#discussioncomment-18166662

- **Helm 2026-08-25 7:01 PM CT:** Signed. Waiting, not authorized. Thank-you may post. V0–V1 hit-target. Do not implement tonight.

- **Helm 2026-08-26 6:20 AM CT:** Night-scoped posture expired. Authorized V0–V1. Right-edge anchoring, both lanes, WidgetMetrics. One session. No public promise. Not a hold.



### Import achievements button does not function

- **Priority:** **CLOSED 2026-08-24 (Claude).** Shipped in v1.99.8 and the loop is closed on

  the thread (comment 18138064) — the reporter's last word was "thanks for looking", so he was

  told the promised wording change actually landed rather than being left to notice it. **His

  second sentence turned out to be the more valuable half** and is now a `BEVEL.md` item:

  *"It's a weird flow since I've never imported achievements before."* That is a first-run

  problem, not a button problem, and no label fix reaches it.

- **Priority (history):** **ANSWERED + FIXED 2026-08-23 (Claude).** Helm signed the reply and authorized

  the wording fix; posted verbatim (comment 18128559). **Your hypothesis was exactly right and

  the screenshot is why** — Apply was grey because the preview had already marked everything,

  not because the button was dead. Fix staged in 1.99.8: the button reads "Nothing to apply",

  a line beside it says the import worked and how many were already marked, and the disabled

  state carries a dim and a tooltip (trap 17). LeBigNasty credited in What's-new.

- **Place:** achievement import. Desktop WINDOW titled "Import achievements — preview" (screenshot). Not the widget. Not the phone. Not Gate 5 overlay. Not a group meter. Nearby #101 is a different ask (token/confirm / silent auto) and is TAKEN — do not restore it.

- **Source:** #235 LeBigNasty Aug 23, 5:13 PM CT. https://github.com/DranakCorps-bot/EQBuddy/discussions/235 New thread. 0 replies. Footer: EQBuddy 1.99.7 · Windows 26200. Screenshot: https://github.com/user-attachments/assets/39d8c84d-7fd3-438f-8dbc-8a1821f77fa0

- **Ask:** "Import achievements button does not function."

- **Already shipped:** 1.99.6 put the import report on the Raids surface (Undo; skipped/unmatched counts). 1.99.7 published 1:46 PM CT (multiclass), before this report. The preview window exists and ran on this shot.

- **Checked:** WINDOW — the attached shot is "Import achievements — preview": "502 achievements read · 76 Sky rewards recognized"; status "Everything recognized is already marked — nothing to apply."; list rows are ✓ Class — item (already marked) (Bard / Beastlord / Berserker / Cleric Sky rewards visible); Apply (0) grayed out; Cancel live. I could not check widget or phone.

- **Hypothesis:** Apply is disabled because the preview says nothing to apply, not because the button is dead. Reporter may have read a disabled Apply as "does not function." Named SOURCE is this preview shot. I do not have a token-unlocked dump AND a quested one from the same player as a control.



### Guk nameds missing from session Mob Farming / Kills by Creature

- **Priority:** DONE 2026-08-24 (Claude). **Your hypothesis was half right and the half it

  missed is the whole bug.** You guessed the aggregators "skip nameds or miss Guk instance

  names". They do neither: Core records every named with its kill, and the two rollups are

  simply TOP-N BY KILL COUNT (`Take(10)` and `Take(8)` over lists sorted by count descending).

  A named is the mob you killed once, so it sorts below a dozen kinds of trash and falls off

  the end. **The control you supplied is what proved it** — own killing blow, solo, no pet

  ruled out every attribution theory and left only the boring one. Both lists are uncapped now

  and any surviving cap prints "... and N more". Fixed in 1.99.10, `GukNamedsRollupTests`.

- **Place:** Session history. Desktop. ROADMAP: Session history → Progress or its own; Kills & Drops is the creature (what died / what it dropped). Not Gate 5 overlay. Not a group meter. Not the Drops-by-Creature wiki-name items.

- **Source:** #234 atrzonkowski Aug 23, 1:21 PM CT. https://github.com/DranakCorps-bot/EQBuddy/discussions/234 New thread. 0 replies. Footer: EQBuddy 1.99.5 · Windows 26200.

- **Ask:** Under session history, Mob Farming and Kills by Creature do not pull named mobs from Guk. They are listed in the encounters. Examples: Ghoul Savant, Ghoul Sentinel.

- **Already shipped:** Kills and Drops by Creature are one window (Kills & Drops) as of 2026-08-21. Encounters list exists (reporter sees the nameds there). Reporter is on 1.99.5; 1.99.7 shipped 1:46 PM CT after this post. I could not check whether 1.99.6/1.99.7 changed the session aggregators.

- **Checked:** I could not check widget / window / phone. I did not open a Guk session. Named SOURCE is the same session's Encounters list vs Mob Farming and Kills by Creature.

- **Hypothesis:** session kill aggregators skip nameds or miss Guk instance names that Encounters still records. Control would be one Guk session where Encounters lists Ghoul Savant / Ghoul Sentinel and the two rollups do not.

- **Follow-up Aug 23, 7:43 PM CT:** atrzonkowski on #234, nested under Claude's killing-blow question. Did not reply (Claude is in the thread). "In this instance all named I had the killing blow. This was a solo instance with no pet. Frenzied Ghoul, Bloodthirsty Ghoul are also absent." Control is now in: own killing blow, solo instance, no pet. Group-member split ruled out for this instance. Extra nameds on the same lists: Frenzied Ghoul, Bloodthirsty Ghoul. I could not check widget / window / phone.

### stop moving UI surfaces every release

- **Priority:** done (David 1:15 PM CT Aug 23: #233 is done unless more is added to the thread. Organizing pass stands. "X is now Y" is standing What's-new process, not an open leftover on this ticket.)

- **Place:** widget card organization / ROADMAP section 3 organizing pass. Desktop + widget. Not a new card. Not Gate 5 overlay. Not a group meter.

- **Source:** #233 mjtrainor Aug 23, 10:04 AM CT. https://github.com/DranakCorps-bot/EQBuddy/discussions/233 New thread. Footer: EQBuddy 1.99.5 · Windows 26200. Did not reply — David (actual human) already posted 12:12 PM CT. No Scribe thank-you.

- **Ask:** "Stop changing every feature and it's location every release, it's terrible application design. I don't want to need to hunt for "missing" features every single time I sit down to play EQL."

- **Already shipped:** David on-thread: organizing pass continues (not apologizing for reorganizing); nothing was deleted; folded cards back on in ⚙ → Cards & windows; merged card keeps the slot you dragged its parts to; ↗ on a card header pops that surface to its own window. v1.99.6 (11:28 AM CT) What's-new opens with #233 and carries the whole map (Progress four rooms; Gear & Loot four tabs; Kills & Drops two tabs). Process leftover David named: any release that moves a surface will say "X is now Y" (old place and new). Same complaint class as #219 lost mote rate, #227/#228 lost the Motes card.

- **Checked:** I did not check widget / window / phone (placement-stability ask, not a missing control). I read the v1.99.6 notes (map + #233 named). Do not treat David's reply as community intake.

- **Hypothesis:** leftover is the What's-new process (name old+new), not a restore of the 14-card layout. Class: V2 if we treat "don't move surfaces" as architecture — leave it. Do not write FABLE.md.

- **CLOSED 2026-08-23 (Claude): your hypothesis was right and the leftover is already built.**

  The "X is now Y" process is a non-negotiable rule in `CLAUDE.md` (a release that MOVES a

  surface names the old place AND the new one), and 1.99.6 shipped the whole map. Nothing is

  outstanding, and you were right not to write a `FABLE.md` stub — "stop moving surfaces" is

  a roadmap question David answered on the thread himself, not architecture.



### Progress: motes-per-hour summary line



- **BUILT 2026-08-23 (Claude), staged in 1.99.6.** It is one line in the Progress

  **Experience** room — "1 mote · 0.9/hr" — beside the xp and AA rates. **David chose that room

  himself** (asked with the question tool): the Progress WINDOW and the phone already carried

  that line inside their Wealth tab's Motes body, so the only Progress surface actually missing

  it was the widget's inline Wealth room, and that room is coin-only by a Helm-signed ruling.

  He took the Experience room knowing it means the window now states the rate on two tabs.

  Every "do not" in your item held: the Wealth chip is still coin, the window/phone Wealth Motes

  rows are untouched (#227 stays its own item), and the Motes card is unchanged. It reuses the

  Motes card's own header string rather than becoming a fourth mote formatter.



### In Progress: next-level spells/abilities by class



- **BUILT 2026-08-23 (Claude), staged in 1.99.6** — the UX half. Per-class expanders under the

  existing "At level N" fold, on both desktops and the phone, following Bevel's lock

  (Helm-signed). Your routing note stays true and is the reason this went well: the CATALOG

  reconciliation is still a Fable V2 and PR 1 is not started, so this ships against the spell

  pages we already have. Nothing was padded and nothing was invented; a class with no table

  keeps its row reading "nothing new at N".

- **One thing your item did not have, and it changed the shape:** the preview is now HIDDEN

  when EQBuddy knows no class at all. It used to fall back to the class-agnostic AA categories

  and jump forward to whatever level had one, which is how David's own card offered him a pet

  ability five levels away for a character with no pet.



### group Sky quest steps by island

- **Priority:** **BUILT 2026-08-23 (Claude), staged in 1.99.6.** Your hypothesis was right that

  steps are a flat list — and the useful correction is that the island DATA was already there,

  written by hand into each step's `Source` prose in five different spellings ("Isle 4:",

  "Isle four -", "Isle 1.5", and 22 steps naming three at once). `SkyIslands` parses it;

  `QuestChecklistLayout.Sky` orders and labels; all three surfaces draw a heading when the

  label changes. 95 of 223 steps name no island at all ("Trash mobs") and keep the flat

  listing, under "Anywhere on the plane" — which is your own "it still needs a place

  (unknown / other), not a dropped step" note, honoured. David chose a player toggle for the

  multi-island case (asked with the question tool).

  (was: approved - David, 2026-08-23 6:14 AM CT. Came from a Reddit user.)

- **Place:** Sky / Plane of Sky quest tracker. Desktop first. Not Gate 5 overlay. Not a group meter.

- **Source:** David in Helm chat, Aug 23, 6:14 AM CT. Reddit user idea (URL not pasted; harvest later if Scribe finds the thread). Not a GitHub thread. Do not reply on Reddit.

- **Ask:** Group steps of Sky quests by which island they are on. Example shape:

  <X Quest>

  Island 1:

  * Kill xyz

  * Loot abc

  Island 5:

  * Hail <npc>

  Island 6:

  * Hand in <something> to <someone>

  A player should see the work for one island together, not a flat list that jumps islands.

- **Already shipped:** Plane of Sky tracker exists; quests have steps. Unknown whether steps already carry an island field. Hypothesis, unchecked -- steps are a flat list per quest today.

- **Checked:** did not grep this run for island-on-step. Do not invent the data source. If a step has no island, it still needs a place (unknown / other), not a dropped step.



### Sky bee chain: the FIRST bee has no catalog entry, and the names may not match



- **Priority:** **ANSWERED 2026-08-24 (Claude)** — comment 18138377. He HAD replied (2026-08-23

  03:03 CT) with four verbatim `/consider` + slain blocks and the three-identical-Bzzazzt

  detail; the item still said "waiting on him" a day later, which is how the thread sat unanswered

  while the work it produced had already shipped in 1.99.6. Bazzzazzt is triggered now exactly as

  he asked; **Bzzazzt deliberately was NOT**, and the reply says so and why — eqlwiki gives it a

  12-hour respawn, something has to start the chain, and his own caveat (personal instances never

  respawn, he has not played the public zone) is the reason we did not generalise instance

  behaviour. His three-mobs observation is in the catalog as `multiSpawn`. Asked him to put it on

  the eqlwiki page, which is where it belongs.

- **Place:** `SpawnCatalog.json`, Plane of Sky. Game DATA, not code.

- **Source:** #109 Frankthetankk, 2026-08-22: confirmed Bzzazzt, Bazzzazzt and Bzzzt were

  counting DOWN toward DUE (not showing elapsed), and The Spiroc Guardian showing DUE.

- **Finding (checked, not a hypothesis):** the catalog carries `Bzzzt` and `Bazzt Zzzt` for

  Sky, both `spawnType: triggered`. **`Bazzzazzt` has no entry at all** — it appears only as

  the `triggeredBy` VALUE for `Bzzzt`. A mob with no entry is neither suppressed nor known, is

  a proper name with no article, and is therefore discovered and given a learned countdown.

  Two of the three names he reports match nothing we carry, and one name we DO carry

  (`Bazzt Zzzt`) is absent from his list.

- **Why it was not just patched:** "named in a `triggeredBy` string ⇒ has no cycle" is WRONG as

  a general rule — the Spiroc Guardian's own triggers are articled trash that respawn normally.

  Whether `Bazzzazzt` has a cycle is an eqlwiki fact, and marking a mob as cycle-less when it

  has one silently deletes a timer the player wanted, with nothing on screen to say so. Asked

  the reporter for the verbatim slain lines and for the chain's real shape.

- **Do NOT tell him the triggered work is already released** (David, 2026-08-22). Something is

  still wrong from where he is sitting; leading with "that shipped" is a victory lap against a

  player's own observation, and the version is not the interesting question anyway.



### auto achievement import vs the #101 token/confirm guard

- **Priority:** TAKEN 2026-08-22 (Claude). **Answer: NO BYPASS — and there was a real defect

  next door.** Both triggers call one Core method, `AchievementsImport.SkyRewards`, so the

  token/confirm guard applies to the unprompted path by construction; a test has said so

  since 2026-08-20 (`TheAutomaticAchievementsImportObeysTheAutoGrantGuardToo`). **What was

  broken is that the automatic path said NOTHING.** `LastAchievementsImport` was written and

  never read in BOTH UIs, so a dump marked Sky rewards and raid clears silently, with no

  report, no Undo, and no mention of what the guard had skipped or what it could not match.

  Fixed for 1.99.6: the report is on the Raids surface (the one that asks for the command),

  with an Undo, and it names the skipped and unmatched counts. Guarded by

  `ImportReportReachesASurfaceTests`, verified to fail 6/11 pre-fix. **Nothing is owed on the

  thread** — his question was answered 2026-08-21 6:53 PM CT and that reply is still the last

  word (checked, after Helm corrected me for claiming otherwise from this item alone). A note

  that the import now reports itself would be a NEW ask: Scribe drafts, Helm signs.

- **Place:** achievement import. Sky checklist. Not Gate 5.

- **Source:** #101 Frankthetankk Aug 21, 5:50 PM CT. Did not reply (old thread; Claude is in it).

- **Ask:** v1.98.1 automatic reading of /outputfile achievements (game dump-announcement line, per-import undo) — does that path use the same token/confirm guard as the manual Import achievements menu, or can it bypass the check this thread just fixed.

- **Already shipped:** #101 token vs confirm vs never-unlocked guard. Claude 2026-08-20: both granted cases skipped as "Skipped — auto-granted, not earned."

- **Checked:** not grepped this run for the auto /outputfile path. Hypothesis, unchecked -- two import triggers (menu vs dump-announcement) may not share the same guard.



### Mobile page doesn't allow refresh when only 1 card is selected

- **Priority:** taken / shipped-on-tag. v1.99.0 (5d2922d, Aug 21, 4:47 PM CT). Helm signed it 6:13 PM CT. Same ticket. Not a new heading. No GitHub victory lap unless David says.

- **Place:** EQBuddy Mobile. Not Gate 5. Hypothesis -- the one-card / solo fill surface, not a new card.

- **Source:** #222 bjstrange Aug 20, 10:56 AM CT. Replied 2026-08-20 (Scribe). Bounce Aug 21, 1:58 PM CT (Keel / Helm design QA). Did not post on GitHub. Did not restore as a new heading.

- **Ask:** pull-down refresh should work when only one card is selected, the same as with two or more. Any single card, including map-as-only-card.

- **Already shipped:** pull-down refresh works with 2+ cards. Bounce misses (subscribe snapshot; map pull from heading, pan on the map) are on the v1.99.0 tag.

- **Bounce (same ticket, not a new item):** two misses only. Do not pair #227, #223, motes, or Progress. Keep solo fill. (1) Release/pull must ask the PC for a fresh snapshot of the visible surface. `location.reload()` is the wrong verb. Do not replace it by painting `latest` again. Leave staleAfterUpdate reload alone (version-change job, not the pull). (2) Map as the only card still needs a pull. Pan wins on the map. Chrome (header / ZONE MAP title) owns the pull. Do not skip PTR because pan exists. Do not disable pan to make PTR work. Hypothesis, unchecked against the app -- the fills early-return is the miss. Verify; do not treat this wording as the patch.

- **Shipped:** v1.99.0 (5d2922d, 4:47 PM CT). Subscribe snapshot (not location.reload, not paint latest). Map-as-only-card: pull from heading chrome, pan wins on the map. Bevel tag audit + Helm ship-sign. Do not delete this heading until Claude takes it. Do not pair motes. No GitHub victory lap unless David says.





### Drops by Creature still shows wiki-missing after the page was corrected

- **Priority:** taken / shipped-on-tag. v1.99.1 (5f43f7e, ~9:31 PM CT Aug 21). Wiki re-check / age caption. Same ticket. Status reply posted 2026-08-22; that reply hold is retired. Not a new heading.

- **Place:** Drops by Creature wiki-missing marker. Desktop.

- **Source:** #226 LeBigNasty Aug 21, 5:25 AM CT. Replied 2026-08-22 (Scribe).

- **Ask:** reads should be dynamic or at least on open. Items that were missing as drops on the wiki, once corrected, still show as missing in Drops by Creature.

- **Already shipped:** wiki pack is a separate window; Drops by Creature has a missing marker. v1.99.1: per-page re-check and age caption. Tooltip now names the served page.

- **Checked:** not grepped this run for the cache. Hypothesis, unchecked -- data source is a cached wiki snapshot, not a fetch when Drops by Creature opens.

- **Follow-up Aug 21, 8:29 AM CT:** LeBigNasty screenshot (filename 092734). Did not reply (already filed; David/Claude share the bot account). Creature headings in yellow. Red diamond = wiki-missing. Named as already on wiki drop tables: Apothic Warband +4 (Fetid fiend -- 4 kills, x1 25%), Cryosilk Amice +4 (Spinechiller spider -- 2 kills, x1 50%), Imbrued Platemail Gauntlets +4 (Worry wraith -- 1 kill, x1 100%). Also red-diamond in the same shot: Fetid Skin, Fire Opal, Mote of Major Potential, Crystallized Sulfur. Eyerazzia +4 and Flayed Turmoilskin Belt +4 have no diamond. Reporter: "not sure if you are checking against cached versions or not accounting for +". Hypothesis, unchecked -- matcher compares the +N item name to a wiki row without the plus, or a cached wiki snapshot.

- **VERIFIED Aug 21 (Claude):** the cache is real — `EqlWikiMobs.CacheLifetime` and `EqlWikiItems.CacheLifetime` are both `TimeSpan.FromDays(7)`. Frankthetankk's "one root cause" reading is right and it is this. The `+N` half is ruled OUT: `WikiContribution.Classify` folds both sides through `QuestCatalog.BaseItemName`, which strips a trailing `+N`, and the 092734 screenshot has tiered items on both sides of the flag (Eyerazzia +4 unflagged, Fetid Skin flagged). **Still open — the fix is a per-page re-check**, on a flagged row and before the pack window exports. Not in 1.99.0.

- **Follow-up Aug 21, 2:15 PM CT:** Frankthetankk on #226. Did not reply (Claude already answered Step 2). Missing flags not clearing after a wiki correction and the +tier false positives in the 092734 screenshot "might be one root cause rather than two separate bugs." Back in #65, "the comparison was confirmed to run against a 7-day per-page cache on the user's machine — so a wiki edit takes up to a week to reach the flags, immediate only for pages you haven't viewed recently." A per-page re-check button was queued then and he does not see it in the changelog. Hypothesis, unchecked -- data source is that 7-day per-page cache, not a live wiki read. Do not restore #65; this is evidence on this item.

- **Follow-up Aug 21, 4:50 PM CT:** Claude confirmed the 7-day cache (`EqlWikiMobs.CacheLifetime` / `EqlWikiItems.CacheLifetime` are both `TimeSpan.FromDays(7)`). The +N half is not the cause: `QuestCatalog.BaseItemName` strips a trailing +N before matching; the 092734 shot has +4 items without a diamond and un-tiered items with one. Re-check button never built; not in 1.99.0. Did not reply. Do not restore #65.

- **Follow-up Aug 21, 7:49 PM CT:** LeBigNasty on #226. Did not reply (old thread; Claude/Helm last before this). "I'll try to remember to check, but you should also check which pages. Innoruk, for example, is checking against the Lore page and not against the creature page." Leftover after v1.99.1: Innoruk lore-vs-creature (not a hold; do not close the thread). Tooltip now names the served page. Status reply already posted. Hypothesis, unchecked -- for Innoruk the compare may still read the Lore article. Named example only.



### What's-new should cover skipped versions

- **Priority:** DONE — and it was already done when this was filed. `WhatsNewCatalog.EntriesBetween(lastSeen, current)` returns EVERY entry between the two versions, both widgets call it, and `WhatsNewTests` covers a multi-version hop (`1.21.0` → `1.23.0`) directly. **The "Already shipped" line below was wrong**, which is the exact rot the 2026-08-22 SSC promises to sweep: this item sat on David for something built long before it was filed.

- **Place:** What's-new popup after an update. Not Gate 5.

- **Source:** #218 n3cr0nk1tt3n Aug 19, 6:54 PM CT, second sentence. Did not reply -- Claude already answered.

- **Ask:** when an update jumps more than one version, show a single stitched What's-new of every entry between the previous build and the latest, not only the build just installed. Reporter's reason for the hop behavior was possibly batched notes; they still want the missed notes if the hop is gone.

- **Already shipped:** What's-new shows the entry for the build you just installed (Claude on #218).

- **Where it might live:** hypothesis -- WhatsNew.json is already a versioned list; the popup currently selects one entry. Data source is the versions between previous and current, not a new notes file.



### Sky instance timers, bee chain, and Spiroc DUE

- **Priority:** taken / shipped-on-tag. v1.99.1 (5f43f7e, ~9:31 PM CT Aug 21). Sky triggered + `creating instance` line. Same ticket. Leftover only: Bzzazzt/Bazzzazzt chip elapsed vs countdown, still pending from Frank. Do not reply. Not a new heading.

- **Place:** spawn timers / catalog. Not Gate 5.

- **Source:** #109 Frankthetankk Aug 19, 2:57 PM CT. Old thread — did not reply.

- **Ask:** inside a Plane of Sky *instance*, do not show a countdown or DUE for named that are not on a respawn clock. Three shapes in one report: (1) instanced Sky bosses that are one-time per instance still get normal timers; (2) the bee chain Bzzazzt — Bzzzt — Bazzzazzt spawns immediately on the previous death, so the ~1:01 chips are kill-duration artifacts; (3) Spiroc Guardian / Lord are player-triggered (kill Spiroc trash), and DUE on the Guardian is the wrong word. Overworld Sky respawn is unmeasured — this ask is the instanced version only.

- **Already shipped:** #109 raid-instance suppress (1.70 / 1.72). `SpawnTimers.cs:228` skips auto-countdown when `entry.RaidInstanced` OR (`_currentZoneInstanced` AND `zone.RaidZone`). `RaidTargets.json` already lists `The Plane of Sky` (Eye of Veeshan, Protector of Sky, The Spiroc Lord, Bazzt Zzzt, …). Catalog notes already call Spiroc Guardian "triggered" and Bzzzt "intermediary spawn."

- **Checked:** Frank's "Sky isn't in the dump" is not true of the file — the dump has that zone. `SpawnCatalog.json` zone is `Plane of Sky` (`log` the same). `MatchesZoneName` uses containment, so `The Plane of Sky` should set `RaidZone` at load. `InstanceTier` only treats `- Solo` / `- Group` or `N (Awakened|Adaptive|Fused|Refined)` as instances; a bare `You have entered The Plane of Sky.` is open world. Dump bosses do not include Spiroc Guardian, Bzzazzt, or Bzzzt as those names. Hypothesis, unchecked without a quoted enter line — either the Sky instance line is not `IsInstance`, so the zone gate never fires, and #185 auto-discovery then learns kill-to-kill clocks for names the dump does not mark; or a running learned timer is showing DUE even when the catalog note already says triggered. Data source for the gate is the zone-enter string plus `RaidTargets.json` / `SpawnEntry.RaidInstanced`, not the Spawns-window note text.

- **Follow-up Aug 21, 9:02 PM CT:** Frankthetankk on #109. Did not reply (old thread; he is answering Claude). Personal Plane of Sky instance, character name omitted. Verbatim sequence: `Player [name] creating instance The Plane of Sky 13931.` / `The Plane of Sky is now available to you.` / `LOADING, PLEASE WAIT...` / `You have entered The Plane of Sky.` No difficulty tier, no `- Solo` or `(Refined)` suffix. He says that enter line is indistinguishable from open-world Sky, so the existing instance-suppression rule cannot key off it; the only instance signal is the earlier `creating instance ... 13931` line, which he says does not look like other instanced zones. Offered more surrounding log if useful. Bzzazzt/Bazzzazzt elapsed vs countdown: he will check a screenshot and follow up separately. Hypothesis, now with a quoted enter line -- `InstanceTier` cannot treat this as an instance, so the raid-instance gate never fires for Sky. Do not treat `creating instance` as an instance-charge line (#221 is a different ask).

- **Follow-up Aug 22, 3:24 PM CT:** Frankthetankk on #109. Did not reply (Claude answered 5:41 PM CT). Original screenshot was countdown/DUE, not elapsed: Bzzazzt, Bazzzazzt, and Bzzzt counting down; The Spiroc Guardian showing DUE. Observed on v1.99.1. Claude asked for verbatim slain lines and whether Bazzzazzt respawns on its own. Same leftover, not a new heading.

- **Follow-up Aug 22, 10:03 PM CT:** Frankthetankk on #109. Did not reply (old thread; he is answering Claude). Tested v1.99.5. Four distinct bees, each with a wiki page, verbatim /consider, and verbatim slain line: (1) Bzzazzt Lvl 50 — https://eqlwiki.com/Bzzazzt — `You have slain Bzzazzt!` — triggers the next spawn AND EQBuddy's respawn timer (the leftover). At island start three NPCs share the name Bzzazzt (two small flankers + one larger middle); only the large middle one advances the chain. (2) Bazzzazzt Lvl 57 — https://eqlwiki.com/Bazzzazzt — `You have slain Bazzzazzt!` — chained, no own clock; also starts an EQBuddy timer. (3) Bzzzt Lvl 60 — already in the triggered list; no timer (correct). (4) Bazzt Zzzt Lvl 63 — already in the triggered list; no timer (correct). Not transcription noise; the catalog is missing two of four names. Ask: add Bzzazzt and Bazzzazzt to the Plane of Sky triggered-mob list alongside the existing four. Caveat, verbatim: all of the above is from personal Plane of Sky instances, which as far as he knows do not respawn once cleared; he has never played public/overworld Sky, so a blanket "triggered, no countdown" might need to be conditional on instance type if overworld bees have a real clock. Hypothesis, unchecked -- data source is those wiki pages plus these slain lines. Do not tell him the triggered work already shipped.



### Slow chip counter-type icon sits beside the word

- **Priority:** ROUTED to Bevel (2026-08-22) — product/UX, not David. The slow chip is an OVERLAY surface, so "does a glyph earn its space beside the word" is the surface-owner call Bevel makes. Ask filed in `BEVEL-FEEDBACK.md`.

- **Place:** overlay slow chips. Not Gate 5.

- **Source:** #94 Frankthetankk Aug 19, 1:46 PM CT. Old thread — did not reply.

- **Ask:** draw a small custom vector icon to the left of the counter-type word on the slow chip face, without replacing the word. Dual-coding: icon + `disease` / `poison` / `curse` together. Do not use a Unicode glyph. Use the same bundled path-geometry set as the rest of the app (card headings, quest markers). Shapes and colors left to design.

- **Already shipped:** chip face is text `Slowed 40% · disease 12` (WhatsNew on #94 field report; `SlowChipText.cs:16`). Kind mark is already a vector column (`ChevronsDown` for slow, ChipStackTests). Claude's 8/16 comment proposed the icon *replace* the word on the chip, with the word in the breakout/tooltip. Frank prefers both on the chip.

- **Checked:** `SlowSpells.json` already has `counterType` per spell (Frank quoted Shiftless Deeds). `SlowChipText.Label` reads `s.CounterType` and writes the word + count only. `rg` of `IconPaths.cs` has no Disease / Poison / Curse keys. Hypothesis — a second vector keyed off catalog `counterType`, not the ChevronsDown kind mark, and not a Unicode stand-in. Data source is `SlowState.CounterType` from `SlowDebuffCatalog`, not the chip label string.



### Wiki pack should not suggest motes as creature drops

- **Priority:** DONE (2026-08-22, Claude, decided not asked). Motes are excluded from what the pack SUGGESTS — `WikiContribution.SuggestableToWiki`, following the wiki's own Mote Guide, so this is matching eqlwiki rather than departing from it and is not David's. **The client-side hide/ignore filter for common drops is NOT this and stays open**: the admins ruled common drops stay IN the suggestion, and Frank's own omit-from-wiki vs hide-from-my-view split is the reason both can be true.

- **Place:** wiki contribution pack. Desktop contribution surface, not Gate 5.

- **Source:** #217 Frankthetankk Aug 19, 12:58 PM CT. Old thread — did not reply.

- **Also:** #226 LeBigNasty Aug 20, 5:12 PM CT (1.98.0). "It would be nice if you could filter out motes and things that can drop from everyone. For things like common drops like gems, it would be nice if the user can filter those out or right click to ignore." Motes corroborate this item. Gems/common-drop ignore is extra; not a second heading yet.

- **Follow-up Aug 20, 8:55 PM CT:** Frank on #226 and #217. Motes: exclude from pack suggestions (wiki Mote Guide; not creature-specific). Common drops/gems: wiki admins pushed back on omitting as a category; hide-from-my-view vs omit-from-wiki. Not a second heading. Did not reply (old threads).

- **Follow-up Aug 21, 5:25 AM CT:** LeBigNasty on #226: "Client side is what I meant when I said user filter." Confirms hide-not-omit. Did not reply.

- **Follow-up Aug 22, 8:33 AM CT:** LeBigNasty on #226 after the 1.99.1 status note. Helm-signed reply (do not treat as a #228 motes-are-back note). "Thanks. Looking much better." Then: "Still recommend app side filtering of motes and client side ignore drop options." Same two asks as this item (pack should not suggest motes; client-side hide/ignore, not omit-from-wiki). Not a new heading. Not the Innoruk leftover. Do not close #226.

- **Follow-up Aug 22, 1:14 PM CT:** LeBigNasty on #226 after the leftovers thank-you. Helm signed the global-ignore thank-you 2026-08-22 8pm; Scribe posting. "Small request- on the ignore, make it global. I want to ignore the item, not the item from the creature. Vendor trash, gems, etc." Same client-side ignore ask, now scoped: ignore is per-item globally, not per-creature. Not a new heading. Do not close #226.

- **Ask:** exclude motes from what the wiki pack ever suggests as a per-creature drop. Wiki [Mote Guide](https://eqlwiki.com/Mote_Guide): motes can drop from any kill; zone difficulty and con color matter, creature identity does not. Listing "Mote of X" on an NPC page would imply a species source that does not exist.

- **Already shipped:** unknown whether the pack currently emits motes.

- **Checked:** `rg -i mote` on `WikiContribution.cs` and `WikiPackPresentation.cs` returned no hits. Hypothesis — not currently surfaced; the flag is so Ask 2 full-history pooling does not start emitting them. Data source is each loot item name in the observation, not a Drops-window filter.



### Wiki pack should pool full session history

- **Priority:** MOVED to `FABLE.md` as a `ready` V2 plan (2026-08-22). Not David's: it is a design question with three open sub-questions the reporter named (pool across characters? a "since" filter for zone retunes? a toggle he explicitly does not want), and the data source moves from a live session object to a query over stored archives.

- **Place:** the all-time stats direction (#168 / #159) — a query over archives already on disk. Fits where that plan is already heading. Not Gate 5.

- **Source:** #217 Frankthetankk Aug 19, 5:26 AM CT, ask 2. Did not reply — Claude already answered.

- **Ask:** Wiki export reads full Session History by default, not the live session. No per-session vs all-time toggle. Concrete miss: three 4-kill sessions never cross the 10-kill rarity bar despite 12 real kills. Same thinning on money/faction samples and con-derived level ranges. Open questions he named: pool across characters on the account, or stay per-character; any "since" filter for zone retunes.

- **Already shipped:** session-scoped export; archived-log review (#74) replays one file at a time.

- **Where it might live:** hypothesis — a roll-up over stored Session History rows, not new collection.



### Spawn-timer mega-thread

- **Priority:** ANSWERED 2026-08-22 — he took none of the options offered. **NOT a mega-thread we host.** His words: *"we should have a way for people to feed verified updates to EQLWiki."* Filed to `FABLE.md` as a V2: the difficulty is the word *verified*, since kill-to-kill does not determine a duration and a wrong respawn timer is worse than none.

- **Place:** catalog maintenance. Curated spawn timers are never auto-written. Not a feature gate.

- **Source:** #185 n3cr0nk1tt3n Aug 18, 10:06 PM CT. Did not reply — Claude already answered.

- **Ask:** a mega-thread for the community to add and update spawn timers, because catalogs lag and kill-to-kill does not determine duration.

- **Already shipped:** manual duration override ("your number wins and survives updates"); add-a-mob on the Spawns window.

- **Where it might live:** hypothesis — a discussion that feeds the existing override, not an auto-write into the curated catalog.



### Server rollback leaves archives ahead of the world

- **Priority:** someday (**David, 2026-08-19: "not too concerned — bigger fish to fry.

  It can go on the 'when we've got nothing else to work on' list."** Filed must-fix; his

  call is someday. See SCRIBE-FEEDBACK for why the tier was wrong.)

- **Place:** session archives / the all-time stats direction. Not Gate 5 UI.

- **Source:** #215 n3cr0nk1tt3n Aug 18, 8:20 PM CT. Footer: EQBuddy 1.93.2 · Windows 26200. Thanked.

- **Ask:** "servers will roll back, such as right now (Freeport was rolled back 20 minutes). However, this does affect the tracking of xp, levelups, and loot in the archives. We can't undo the rollback, but we should be able to snapshot and reference where we are reset to for xp."

- **Already shipped:** nothing known that marks a rollback. Do not assert archive format without a quote.

- **Where it might live:** hypothesis — a bookmark at rollback time against the archives already on disk, not rewriting the rolled-back window.



### Loot: look an item up by name

- **Priority:** someday

- **Source:** #211 n3cr0nk1tt3n (follow-up)

- **Ask:** Search items by name even if he has not looted one this session.

- **Already shipped:** icon hit-target in the 1.93.0 draft.

- **Where it might live:** existing eqlwiki item-lookup popup, if it already searches by name — then this is surfacing, not a second search. Hypothesis.



### Chips and alerts ignore the parked monitor

- **Priority:** **FIXED-shipped 2026-09-04** for the Mobile-sounds half in `v1.99.18` (PR #287 `abf55a94`; tag `dbcfb3a1`; Bevel lock via #283). Options → Behavior → Mobile sounds, default Off. Cosmic/Wayland parked-monitor chip placement is still compositor-limited (not claimed fixed). Hold lifted for this cut only; now Retired.

- **David's ruling, 2026-08-22 (in session):** EQBuddy Mobile making sound is **opt-in, off by default** -- "I don't need to mandate that for everyone." Built that way for `v1.99.18`.

- **Place:** Avalonia chip/alert restore vs Wayland compositor placement. Not overlay-over-fullscreen.

- **Source:** #208 sbaum23 (opened Aug 17) + follow-up Aug 18, 7:37 PM CT. Old thread — did not reply.

- **Ask:** Widget is on the non-EQ monitor. Chips and alerts still appear on the EQ monitor after he saves positions in Options, and that minimizes EQ.

- **New evidence (follow-up):** PopOS Cosmic / Wayland. `settings.json` DID write second-monitor coords (`AlertLeft` 3753 / `AlertTop` 228; `MezChipsLeft` 3131 / `MezChipsTop` 88 / `MezChipsBottom` 291; first monitor 2560×1440). Screenshot: Options lives on monitor 2; after close, a mez chip appears on EQ's main monitor (behind EQ, not at the dragged location). Extra: if nothing else is on the EQ screen, chips overlay EQ, FPS tanks, game loses focus until click; if another window is behind EQ, the EQ window disappears when chips show. Also asked that EQBuddy not fight to be the top window when parked on the second screen.

- **Already shipped:** he can move and save positions; the write path is no longer the missing fact.

- **Follow-up Aug 20, 6:54 PM CT:** sbaum23 on cosmic-comp: new windows spawn at the cursor; ConfigureRequest is ignored once showing. Chips/alerts are recreated, so they land on the EQ screen (where the mouse is). Keep-open option worked for him but felt clunky. Prefers Mobile on the second screen; wants desktop chips/alerts off while Mobile keeps them, including alert sounds in the browser. EQBuddy Mobile on Linux worked.

- **Replied:** 2026-09-04 ~2:23 PM CT Helm-signed shipped-status (DranakCorps-bot): https://github.com/DranakCorps-bot/EQBuddy/discussions/208#discussioncomment-18294973 — Mobile can play a sound when an alert fires; off until Options → Behavior → Mobile sounds; in the latest release (`v1.99.18`).

- **Where it might live:** hypothesis — Wayland compositor ignores requested chip/alert coordinates (Claude already said this if the settings wrote). Do not assert Avalonia source without a quote. Mobile-sounds half shipped; placement half remains compositor.



### charm4.txt still reports no held time

- **Priority:** someday (reporter said more time is optional)

- **Source:** #135 bjstrange; found while extracting CharmTracker (Aug 18)

- **Ask:** charm7 item-clicky is fixed in 1.91.0. charm4 still replays with no `held` on the break — charm never claimed.

- **Already shipped:** charm7 path.

- **Hypothesis (not a fix):** `_petName` was already set when the landing arrived, so the unknown-cast candidate path was skipped. Replay `charm4.txt` and print every state change before believing that. A synthetic test from this guess already passed for the wrong reason and was deleted.



### Damage breakdown vs EQLogParser

- **Priority:** someday

- **Source:** Reddit r/EQLegends — u/aqualoon_ (Aug 10), u/Frell90 (Aug 8), u/OnlyTroot on the Aug 10 EQBuddy update

- **Ask:** ACT-style per-source damage (spell / skill / proc), including charmed pets. Geicojacob says EQBuddy already shows damage by spell.

- **Already shipped:** Combat card per-ability breakdown (if that is what they mean, this is discoverability).

- **Where it might live:** Combat card / breakout, or a parser gap if charmed-pet credit is actually missing (~70% miss is the claim). Party DPS from u/Geicojacob is a decline — not a group monitor.



### Slow alert needs its own mute

- **Priority:** someday

- **Source:** Reddit r/EQLegends — u/KeeferMaddness on the Aug 10 EQBuddy update

- **Ask:** Turn down or off the "Slow up to 75" sound without killing other alerts.

- **Already shipped:** per-rule sound Off on Watch rules (if Slow uses that path, this is a reply).

- **Where it might live:** the Slow built-in vs Watch-rule sound box. Not #153.



### Overlay while the game is fullscreen

- **Priority:** someday

- **Source:** Reddit r/EQLegends — u/evilpeenevil on the Aug 10 update; recurring

- **Ask:** Widget over a fullscreen game, not only windowed / borderless.

- **Already shipped:** Windows always-on-top; CrossOver overlay doc; Wayland cannot overlay the game (#208 is the parked-monitor case).

- **Where it might live:** unknown until someone names Windows fullscreen as the miss. Do not promise Wayland-over-fullscreen.



### Progress window lists every AA

- **Priority:** someday

- **Source:** Reddit r/EQLegends — u/cloudrhythm on the Aug 10 update

- **Ask:** Hide the full AA list in expanded Progress. Separately, a way to disable mez chips.

- **Already shipped:** mez chips may already hide via overlay-card Options — unverified here.

- **Where it might live:** Progress expanded view (collapse/hide the AA dump, do not drop AA tracking).



### Map should show facing, not only a /loc dot

- **Priority:** someday

- **Source:** Reddit r/EQLegends — u/conky_dor on the Aug 10 update

- **Ask:** Heading / facing on the zone map.

- **Already shipped:** `/loc` marker and breadcrumb trail.

- **Where it might live:** only if the log line actually carries heading. If it does not, this is a no unless they type a heading command. Do not invent heading from breadcrumbs.



### Check off Sky items already in the bag / already turned in

- **Priority:** someday

- **Source:** Reddit r/EQLegends — u/Rajahten and u/signgain82 on the Aug 10 update

- **Ask:** Retroactively mark completed Sky quests and owned items without re-looting.

- **Already shipped:** achievements import (one miss: Rogue Shimmering Bracer, #206 — that was a catalog name, not a matcher); Mark turned in.

- **Where it might live:** no inventory read. Remaining hole is "the log never saw this item." Manual check or achievements paste, not memory reading.



### Steam Deck / Linux companion for Sky

- **Priority:** someday

- **Source:** Reddit r/EQLegends — u/Dcw1sfu82 (Aug 16)

- **Ask:** Companion on Steam Deck, mainly Plane of Sky class-quest tracking.

- **Already shipped:** Linux Avalonia build.

- **Where it might live:** a reply pointing at the Linux tarball and Wine log-folder detection. Not a Deck port unless install is actually broken.



### Printable Plane of Sky checklist

- **Priority:** someday

- **Source:** Reddit r/EQLegends — u/aversethule (Aug 11)

- **Ask:** PDF / print export of PoS quests and class unlocks.

- **Already shipped:** in-app Sky checklist.

- **Where it might live:** print stylesheet or copy-as-text. Not a PDF pipeline.


