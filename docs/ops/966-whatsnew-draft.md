# Discussion #966 — `WhatsNew.json` entry, drafted

**Status: DRAFT, and deliberately NOT in `WhatsNew.json`.** The same idiom as
[dra181-whatsnew-draft.md](dra181-whatsnew-draft.md). The top entry of `WhatsNew.json` is
`2.0.1`, which is TAGGED, and `<Version>` in `Directory.Build.props` is still `2.0.1`.
`scripts/whatsnew-guard.ps1` (in `check.ps1` and CI's required `build-and-test`) refuses
both ways in: editing the shipped `2.0.1` entry, and a `2.0.2` entry on top while
`<Version>` says `2.0.1`. Bumping `<Version>` is the release slice's call, not this fix's.
So the entry is written now, while the change is fresh, and goes in when `<Version>` moves.

**Reporter credit:** CryfaceCorpse, discussion #966.

---

## Highlight — for the `2.0.2` entry

> **THE GUIDE WINDOW NOW OPENS WHERE YOU CAN GRAB IT, AND REMEMBERS WHERE YOU PUT IT**
> (discussion #966, thanks CryfaceCorpse). On a desk with more than one monitor, the Guide
> could open on a monitor beside your main one with its title bar off the top of the screen
> — most often when that monitor is shorter than your main one, or set lower — so the only
> way to move it was the taskbar's Move and the arrow keys. It now always opens with its
> whole window, title bar included, on one of your monitors. AND IT REMEMBERS: move or
> resize the Guide and it opens there next time, restarts included. If a monitor it was on
> is switched off or unplugged, it comes back onto one you have.
