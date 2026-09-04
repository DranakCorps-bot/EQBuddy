# Preserving EQBuddy 1.x

**Short version:** EQBuddy Evolved (v2) is Windows-only and is the supported product line going forward. **1.x is not being taken down.** Final Linux, macOS, and Windows 1.x builds stay downloadable and usable. Active support for that line stops.

This page is the public promise. Product identity for the next major version lives in [PRODUCT.md](PRODUCT.md). The player-facing vision is [EQBuddy-Evolved.md](EQBuddy-Evolved.md).

---

## Support matrix

| Surface | Status |
|---|---|
| Windows desktop (Evolved / v2) | **Supported** product line |
| EQBuddy Mobile hosted by Windows | **Supported** second screen |
| Linux desktop 1.x | **Preserved legacy** — final builds remain downloadable and usable |
| macOS desktop 1.x | **Preserved legacy** — final builds remain downloadable and usable |
| Windows desktop 1.x | **Preserved** until the Evolved channel opens; current public downloads are still 1.x |

Current GitHub releases remain the 1.x line. Evolved is not a download yet.

"Preserved legacy" is a precise promise and it is worth spelling out: the build you have keeps working, the download stays up, and nobody will ship you a fix for it. It is not a maintained platform and it is not abandoned in the sense of being deleted.

---

## The final 1.x release

**The final cross-platform release is the "bridge" release** — one ordinary 1.99.x release whose contents are special. It carries the update-channel change that stops a Linux or macOS copy from ever being offered a Windows v2 installer, and it is the last build those platforms will be offered. **Its tag is the final legacy tag; there will not be a second one.**

**It is planned as `v1.99.18` and has not been published yet.** Until it is, the links below point at the current public 1.x release, [**v1.99.17**](https://github.com/DranakCorps-bot/EQBuddy/releases/tag/v1.99.17) — a link to a tag that does not exist yet would simply be a 404, which is a poor last thing to say to anyone. When `v1.99.18` is published, this section and those links move to it. Progress is tracked on [issue #275](https://github.com/DranakCorps-bot/EQBuddy/issues/275).

### Direct downloads — Linux and macOS

| Platform | Asset |
|---|---|
| Linux x64 | [`EQBuddy-linux-x64.tar.gz`](https://github.com/DranakCorps-bot/EQBuddy/releases/download/v1.99.17/EQBuddy-linux-x64.tar.gz) |
| macOS Apple Silicon | [`EQBuddy-osx-arm64.zip`](https://github.com/DranakCorps-bot/EQBuddy/releases/download/v1.99.17/EQBuddy-osx-arm64.zip) |
| macOS Intel | [`EQBuddy-osx-x64.zip`](https://github.com/DranakCorps-bot/EQBuddy/releases/download/v1.99.17/EQBuddy-osx-x64.zip) |

Windows 1.x users take `EQBuddySetup.exe` (or `EQBuddy-portable.zip`) from the same release.

**Every link on this page names a TAG, never `releases/latest`.** That is deliberate and permanent: `releases/latest` becomes the v2 release page the moment v2 ships, and the most prominent asset on it is a Windows installer. A legacy page that sends a Mac user to `releases/latest` would look correct in every screenshot and hand them something that cannot run.

### Running it

- **Linux** — unpack the tarball and run the `EQBuddy.Avalonia` binary inside it (`tar -xzf EQBuddy-linux-x64.tar.gz && ./EQBuddy.Avalonia`). It is self-contained; no .NET install is needed.
- **macOS** — unzip and drag `EQBuddy.app` to Applications. The bundles are **unsigned**, so the **first launch needs right-click → Open** rather than a double-click. If macOS refuses anyway, clear the quarantine flag once:

  ```bash
  xattr -dr com.apple.quarantine EQBuddy.app
  ```

  EverQuest Legends itself runs under a Windows compatibility layer on macOS, and EQBuddy finds the game's log folder inside each Wine prefix it can see (`$WINEPREFIX`, osxEQL, CrossOver bottles, Whisky bottles, PlayOnMac, `~/.wine`).

---

## What this means if you already have 1.x

**What continues:**

- Your installed 1.x build keeps working. Nothing expires, phones home, or switches itself off.
- Everything it already does keeps working offline: the parser, the shipped catalogs, session history, EQBuddy Mobile hosted by that copy.
- Final 1.x artifacts stay on GitHub (installer, portable zip, Linux tarball, macOS `.app` bundles, notes, tag, and the source state they were built from).
- Your profile is yours: `settings.json`, session history and archives are on your disk and are never touched by any of this.
- A later 1.99.x LEGACY patch, if one is ever published, is still offered on every platform. Preserved does not mean welded shut.

**What stops:**

- New features, fixes and weekly knowledge refreshes go to Windows Evolved, not to the 1.x line.
- The Linux and macOS builds are not carried forward into v2. There is no Evolved build for them and none is promised.
- Once a 2.x release is public, a non-Windows 1.x copy shows a **one-time notice** — v1 is final for that platform, the install keeps working, v2 is Windows-only — and is sent to the final legacy release page rather than to an installer it cannot run. Help → Check for updates always answers with the same notice; silence there would be a bug.

**What we will not do:**

- We will not take 1.x down or hide the final legacy release.
- We will not force a migration, self-remove an install, overwrite a working 1.x tree, or wipe personal history.
- We will not present a Windows v2 installer as the update on Linux or macOS.
- We will not require Linux or macOS users to move to Evolved to keep using the companion they already have.

Linux and macOS 1.x exist because people built them — notably [Don Thompson](https://github.com/DonThompson) for Avalonia/Linux and [quasarj](https://github.com/quasarj) for macOS. That work stays in the history and in the final 1.x downloads. It is not the supported Evolved desktop.

---

## Retention, and what backs it

The final legacy release's **assets, notes, tag, and the source state it was built from are retained permanently**. Nothing in a source tree can enforce that — a future `gh release delete` would not ask a test for permission — so it is backed where it can be: a GitHub **tag protection rule** on the final legacy tag and **branch protection** on the preserved branch, plus this page saying so in public.

A **`legacy-v1` branch** is cut from the final cross-platform state before the Avalonia project leaves the v2 mainline, so the last cross-platform tree stays browsable and buildable without resurrecting it from a tag under time pressure. **It is preserved, not maintained**: no CI is wired to it, and a green badge on it would be a support promise nobody made.

Both are Phase 0 work on [#275](https://github.com/DranakCorps-bot/EQBuddy/issues/275) and are not done yet.

---

## When v2 ships

The first v2 release notes, and this repository's README, carry a visible **Legacy Linux/macOS** section linking to the final v1 release. That obligation outlives the session that writes it, so it is enforced by `scripts/legacy-notice-guard.ps1`, which refuses a 2.x release whose notes do not carry it.

This matters more than the in-app notice does. The notice only reaches installs that took the bridge release; anyone still on an older 1.x build finds us through the release page and the README, and those are the only two places that can reach them.

---

## Forking and continuing 1.x yourself — an invitation, not a commitment

**This applies to v1 / already-published 1.x only.**

The published 1.x tree is MIT ([LICENSE](LICENSE)), and that has always been an invitation: fork it, port it, keep building on it, ship your own tool from it. If the Linux or macOS build matters to you and it is no longer being carried forward here, **continuing it independently is welcome** — that is what the license was for.

Two honest limits, so nobody reads more into this than is here:

- **It is an invitation, not a commitment.** Nobody here is promising to maintain, review, merge, support, or host a continuation, and no timeline is offered.
- **MIT's one condition is a condition**: the copyright notice travels with any substantial portion you take. In practice, name what you took — a "based on [EQBuddy](https://github.com/DranakCorps-bot/EQBuddy)" line in your README or about screen.

**None of this applies to EQBuddy Evolved.** See the next section.

---

## Licensing

**1.x / LEGACY stays MIT.** Already-published 1.x code and the existing
[LICENSE](LICENSE) remain the open-with-credit grant they always were. Those
past MIT grants stay; we do not revoke them. A community fork of that
published tree is still the invitation that applied to v1.

**Evolved is separate and proprietary.** EQBuddy Evolved (v2) — its source code, assets,
and Evolved-specific documentation — is **All Rights Reserved**. You may **not** use,
copy, modify, redistribute, sublicense, or fork Evolved, or any substantial portion of
it, without **prior written permission** from David Edwards. This is **not** an
open-source license and is **not** the MIT model used for already-published 1.x — see
[LICENSE-EVOLVED.md](LICENSE-EVOLVED.md) and [PRODUCT.md](PRODUCT.md#licensing). The v1
fork invitation above never applies to Evolved.

---

## Bridge in progress

The work that makes the promise above true *before* Evolved can ship as its own channel is tracked in **[issue #275](https://github.com/DranakCorps-bot/EQBuddy/issues/275)** (Phase 0 — final 1.x bridge, update-channel honesty, retained artifacts).

Until that gate is done, public downloads stay 1.x, and this page is the support matrix to read — not a claim that Evolved is already shipping.
