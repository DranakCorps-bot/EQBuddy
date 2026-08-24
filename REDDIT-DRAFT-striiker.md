# Draft reply to StrIIker-TV (Reddit) — for David to post as Dranak75

**Status:** draft only, nothing posted. This is a data-loss apology on a thread David is
already in personally, so it is his voice and his call — not a routine signed thread reply.

**The one thing to check before posting:** ask them to look in `Logs\archive` first. If
their content is there, this thread ends well; if archiving had been switched off, it does
not, and the reply should not promise otherwise. The draft below is written to be true
either way.

---

## The draft

> I dug into this and you were right on both counts. Two separate bugs, and neither of them
> was your checkbox failing to save. I'm sorry — this one's on me.
>
> **Why ticking the box didn't save you.** The tour's first page asks whether to keep your
> logs, and the cleanup that runs at startup does wait for your answer. But there's a second
> cleanup on a timer, and that one was never told to wait. Worse, its "run every 10 minutes"
> timer starts from zero — so it fired about a second after launch and emptied your logs
> while the question was still sitting on your screen. You ticked the box and it was already
> done. The tick worked; you were asked after the fact. That's an app asking permission for
> something it had already done, which is indefensible, and it's fixed.
>
> **Why it took logs you weren't even playing, including your renamed ones.** The cleanup
> matched anything called `eqlog_*.txt`. That's how the game names its logs — and it's also
> how your saved copies were named once you renamed them. It genuinely could not tell your
> archive from a live log. It now only ever empties a file with the exact shape EverQuest
> Legends itself writes, so anything you've dated, numbered or copied — any name carrying
> a character the game never writes — is off limits permanently. (One honest caveat: a
> rename that adds *only letters*, like `_old`, still looks exactly like a game log from
> the filename alone — put a date or number in copies you keep.)
>
> **Now the part worth checking before anything else — look in your `Logs\archive` folder.**
> Since 1.84.0 EQBuddy copies a log's full contents there *before* emptying it, and that's
> on by default. It never deletes anything in that folder. Files there are named
> `eqlog_yourname_yourserver_` plus the date and time the session ended. If your logs were
> emptied by the bug above, there's a good chance the text is all still sitting on your
> disk. If that folder's empty, archiving had been turned off in Options and it's genuinely
> gone — in which case I'm sorry, and I'd rather say that than pretend otherwise.
>
> Both fixes are in the next build with the whole thing written up in the release notes.
> Thanks for pushing back after my first reply — I pointed at the consent screen instead of
> checking whether it actually worked, and you were the one who was right.

---

## Notes on choices in the draft, in case you want to change them

- **It concedes the first reply was wrong.** David's first response was essentially "this is
  literally the first thing that pops up when you open it" — which was true and beside the
  point, because the dialog was defeated a second later by code behind it. Owning that is
  what makes the rest credible, and the reporter had already been gracious about it.
- **It does not promise recovery**, because archiving can have been off. It gives them the
  check to run and states both outcomes.
- **It names no version number for the fix**, since the release is your gate. Add one if you
  want when you decide to ship.
- **No link to the repo or the release** — add if you want, but a data-loss thread is a poor
  place for a download pitch.
- **Length**: long for Reddit. The two-bug explanation is what earns the apology; if you want
  it shorter, the cuttable part is the second paragraph's detail about the timer, not the
  archive-folder paragraph, which is the actionable one.

## What is NOT drafted here

A GitHub discussion for this. You picked "draft it for you, you post" over also opening one,
so nothing has been filed in the repo. Say the word if you want it tracked publicly with a
number to credit in What's-new — right now the release notes credit StrIIker-TV by name with
no number, which is a small departure from the usual "name and discussion number" rule and is
deliberate, since the report came in on Reddit.
