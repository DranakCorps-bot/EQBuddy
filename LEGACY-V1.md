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

---

## What this means if you already have 1.x

- Your installed 1.x build keeps working.
- Final 1.x artifacts stay on GitHub (installer, portable zip, Linux tarball, macOS `.app` bundles, notes, tag, and the source state they were built from).
- Linux and macOS users are **not** asked to move to Windows or to give up the build they have.
- When a v2 channel exists, a non-Windows 1.x install will not be offered a Windows Evolved installer as an update.

Linux and macOS 1.x exist because people built them — notably [Don Thompson](https://github.com/DonThompson) for Avalonia/Linux and [quasarj](https://github.com/quasarj) for macOS. That work stays in the history and in the final 1.x downloads. It is not the supported Evolved desktop.

---

## What we will not do

- We will not take 1.x down or hide the final legacy release.
- We will not force a migration, self-remove an install, overwrite a working 1.x tree, or wipe personal history.
- We will not present a Windows v2 installer as the update on Linux or macOS.
- We will not require Linux or macOS users to move to Evolved to keep using the companion they already have.

---

## Bridge in progress

The work that makes the promise above true *before* Evolved can ship as its own channel is tracked in **[issue #275](https://github.com/DranakCorps-bot/EQBuddy/issues/275)** (Phase 0 — final 1.x bridge, update-channel honesty, retained artifacts).

Until that gate is done, public downloads stay 1.x, and this page is the support matrix to read — not a claim that Evolved is already shipping.

---

## Licensing

**1.x / LEGACY stays MIT.** Already-published 1.x code and the existing
[LICENSE](LICENSE) remain the open-with-credit grant they always were. Those
past MIT grants stay; we do not revoke them. A community fork of that
published tree is still the invitation that applied to v1.

**Evolved is separate and proprietary.** EQBuddy Evolved (v2) code, assets,
and Evolved-specific docs are All Rights Reserved — see
[LICENSE-EVOLVED.md](LICENSE-EVOLVED.md) and [PRODUCT.md](PRODUCT.md#licensing).
They are not MIT, not “use freely, credit visibly,” and not the same
licensing posture as JMoyer’s EQL Companion. The v1 fork invitation never
applies to Evolved.
