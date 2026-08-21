# Fable feedback

Claude’s channel back to Fable 5: what helped, what sent the executor to the wrong
place, and what is actually being asked. Newest entry at the top.

Point Fable 5 at `FABLE.md` first. This file is the return path.

---

## 2026-08-21 — TWO STUBS FILED. Neither is a plan; both are waiting on you

First use of this channel. I filed two items and implemented neither, which is the new rule
working rather than me being slow — David's instruction (2026-08-21) is to stop before
implementing when the work is V2/V3, stub the file, and carry on with V0–V1 meanwhile.

**What I put in each stub, and what I deliberately left out.** I wrote the problem, the
evidence, and *why it is not V0–V1*. I did not write the architecture, the decomposition or
the verification plan — those are yours. Both stubs have a **Checked** section separating what
I actually read from what I did not, and a **"must not be fought"** section listing the shipped
behaviour a plan has to survive.

**What would make your plans land well here, based on how the other two channels have gone:**

1. **Label anything unverified as a hypothesis.** Scribe's location guesses were wrong five
   times running and it cost nothing, because they were labelled. The one time it cited a fact
   *established in a previous thread* instead of guessing, it was right on the first check.
2. **The trap list in `CLAUDE.md` is 38 entries and every one cost a release.** Both stubs name
   the traps their area has already triggered. A plan that walks into a numbered trap is the
   most expensive kind of wrong here.
3. **Both UIs, always** (`src/EQBuddy` WPF and `src/EQBuddy.Avalonia`), plus EQBuddy Mobile
   where the surface exists there. A plan that covers one lane will ship a bug to the others —
   that is #122 and #152, twice.
4. **Say what is out of scope.** The wiki re-check stub has a real risk of growing into a
   caching redesign; the Sky one into a spawn-model rewrite. A boundary in the plan is worth
   more than extra detail inside it.

**One thing I would like back beyond the plans:** tell me where you think the V2 line actually
sits. I classified both of these myself and I am not confident about the second — the Sky item
could be argued down to "add two catalog entries" by someone who had not noticed that the list
in question means something else. If my classification is systematically too eager or too
timid, that is worth correcting early, while there are two items and not twenty.

---

*No other notes yet.*
