namespace EQBuddy.UI.Shared;

/// <summary>
/// Every icon in the app as PATH GEOMETRY, on a 24×24 grid, in one table.
///
/// Why not a font, and why not emoji (docs/DesignSystem.md §4):
///
///  * The Gate 1 audit found **84 distinct non-ASCII glyphs over 857 uses**, from four
///    unrelated families, with the same concept spelled more than one way — done was
///    <c>✓</c> ×62 and <c>✔</c> ×15, refresh was <c>⟳</c> ×22 and <c>↻</c> ×4.
///  * Emoji render at a size and weight the app does not control, and vary by platform.
///    That is not hypothetical: **PRs #148 and #166 exist because icon glyphs failed to
///    render at all in Wine prefixes**, on the Linux/macOS builds that are EQBuddy's only
///    uncontested ground. A font-based set re-opens a bug already paid for.
///  * Path data is data, so it sits beside <see cref="ThemePalettes"/> and gets the same
///    anti-drift treatment, takes the palette as its fill (an icon can't go off-palette),
///    and is sized by us rather than by a font's metrics.
///
/// The UI half of this table began life inside the Avalonia <c>AppTheme</c>; it is here
/// so the WPF side draws the same shapes rather than the emoji it drew before.
///
/// Emoji survive in exactly one place: user-facing TEXT where they are content rather
/// than UI (What's New entries, discussion templates). Not in controls.
/// </summary>
public static class IconPaths
{
    /// <summary>The grid every path is drawn on. A renderer scales from here; nothing
    /// below assumes a rendered size.</summary>
    public const double ViewBox = 24;

    // ---- interaction vocabulary ----

    private static readonly Dictionary<string, string> Ui = new(StringComparer.Ordinal)
    {
        ["Settings"] = "M19.43 12.98c.04-.32.07-.65.07-.98s-.02-.66-.07-.98l2.11-1.65c.19-.15.24-.42.12-.64l-2-3.46c-.12-.22-.37-.31-.6-.22l-2.49 1a7.28 7.28 0 0 0-1.69-.98L14.5 2.42A.5.5 0 0 0 14 2h-4a.5.5 0 0 0-.5.42L9.12 5.07c-.61.23-1.18.56-1.69.98l-2.49-1a.5.5 0 0 0-.6.22l-2 3.46a.5.5 0 0 0 .12.64l2.11 1.65c-.05.32-.07.65-.07.98s.02.66.07.98l-2.11 1.65a.5.5 0 0 0-.12.64l2 3.46c.12.22.37.31.6.22l2.49-1c.51.4 1.08.74 1.69.98l.38 2.65a.5.5 0 0 0 .5.42h4a.5.5 0 0 0 .5-.42l.38-2.65c.61-.23 1.18-.56 1.69-.98l2.49 1c.23.08.48 0 .6-.22l2-3.46a.5.5 0 0 0-.12-.64l-2.11-1.65ZM12 15.5A3.5 3.5 0 1 1 12 8a3.5 3.5 0 0 1 0 7.5Z",
        ["Refresh"] = "M17.65 6.35A7.95 7.95 0 0 0 12 4a8 8 0 1 0 7.45 5.08h-2.16A6 6 0 1 1 12 6c1.66 0 3.14.69 4.22 1.78L13 11h8V3l-3.35 3.35Z",
        ["Minimize"] = "M5 12h14v2H5z",
        ["Expand"] = "M5 5h6v2H8.41l3.3 3.29-1.42 1.42L7 8.41V11H5V5Zm14 14h-6v-2h2.59l-3.3-3.29 1.42-1.42L17 15.59V13h2v6Z",
        // A single north-east arrow: "this LEAVES rather than unfolds" — the meaning the
        // SectionLink card carried as "↗" before Gate 5d. Distinct from "Expand", whose
        // two arrows point APART and mean "make this bigger"; substituting one for the
        // other reads as a resize handle, which is what the first cut of 5d shipped into
        // a screenshot.
        ["ArrowUpRight"] = "M6.4 17.6 15.99 8H10V6h9v9h-2V9.41L7.41 19 6.4 17.6Z",
        ["Close"] = "M6.4 5 5 6.4 10.6 12 5 17.6 6.4 19 12 13.4 17.6 19 19 17.6 13.4 12 19 6.4 17.6 5 12 10.6 6.4 5Z",
        ["Star"] = "M22 9.24l-7.19-.62L12 2 9.19 8.63 2 9.24l5.46 4.73-1.64 7.03L12 17.27 18.18 21l-1.63-7.03L22 9.24ZM12 15.4l-3.76 2.27 1-4.28-3.32-2.88 4.38-.38L12 6.1l1.71 4.04 4.38.38-3.32 2.88 1 4.28L12 15.4Z",
        ["StarFilled"] = "M12 17.27 18.18 21l-1.64-7.03L22 9.24l-7.19-.61L12 2 9.19 8.63 2 9.24l5.46 4.73L5.82 21 12 17.27Z",
        ["ChevronRight"] = "M8.59 16.59 13.17 12 8.59 7.41 10 6l6 6-6 6-1.41-1.41Z",
        ["ChevronDown"] = "M7.41 8.59 12 13.17l4.59-4.58L18 10l-6 6-6-6 1.41-1.41Z",
        ["Map"] = "M20.5 3l-.16.03L15 5.1 9 3 3.36 4.9c-.21.07-.36.25-.36.48V20.5c0 .28.22.5.5.5l.16-.03L9 18.9l6 2.1 5.64-1.9c.21-.07.36-.25.36-.48V3.5c0-.28-.22-.5-.5-.5ZM15 19l-6-2.11V5l6 2.11V19Z",
        ["Quest"] = "M14.4 6 14 4H5v17h2v-7h5.6l.4 2h7V6h-5.6Z",
        ["Gear"] = "M12 1 3 5v6c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12V5l-9-4Zm0 10.99h7c-.53 4.12-3.28 7.79-7 8.94V12H5V6.3l7-3.11v8.8Z",
        ["Timeline"] = "M23 8c0 1.1-.9 2-2 2-.18 0-.35-.02-.51-.07l-3.56 3.55c.05.16.07.34.07.52 0 1.1-.9 2-2 2s-2-.9-2-2c0-.18.02-.36.07-.52l-2.55-2.55c-.16.05-.34.07-.52.07s-.36-.02-.52-.07l-4.55 4.56c.05.16.07.33.07.51 0 1.1-.9 2-2 2s-2-.9-2-2 .9-2 2-2c.18 0 .35.02.51.07l4.56-4.55C8.02 9.36 8 9.18 8 9c0-1.1.9-2 2-2s2 .9 2 2c0 .18-.02.36-.07.52l2.55 2.55c.16-.05.34-.07.52-.07s.36.02.52.07l3.55-3.56C19.02 8.35 19 8.18 19 8c0-1.1.9-2 2-2s2 .9 2 2Z",
        ["Tray"] = "M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2Zm0 12h-4c0 1.66-1.35 3-3 3s-3-1.34-3-3H5V5h14v10Z",
        ["Chart"] = "M5 9.2h3V19H5V9.2ZM10.6 5h2.8v14h-2.8V5Zm5.6 8H19v6h-2.8v-6Z",

        // Added for Gate 2 (Quests). Each one replaces an emoji that was doing the same
        // job: 🔍 📌 ✓ ⚑ ↶ ⧉ 📍 📦 📚 🔎 ✕ — see docs/DesignSystem.md §4.
        ["Search"] = "M15.5 14h-.79l-.28-.27A6.47 6.47 0 0 0 16 9.5 6.5 6.5 0 1 0 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5Zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14Z",
        ["Pin"] = "M14 4v5c0 1.12.37 2.16 1 3H9c.63-.84 1-1.88 1-3V4h4m3-2H7c-.55 0-1 .45-1 1s.45 1 1 1h1v5c0 1.66-1.34 3-3 3v2h5.97v7l1 1 1-1v-7H19v-2c-1.66 0-3-1.34-3-3V4h1c.55 0 1-.45 1-1s-.45-1-1-1Z",
        ["PinFilled"] = "M16 9V4h1c.55 0 1-.45 1-1s-.45-1-1-1H7c-.55 0-1 .45-1 1s.45 1 1 1h1v5c0 1.66-1.34 3-3 3v2h5.97v7l1 1 1-1v-7H19v-2c-1.66 0-3-1.34-3-3Z",
        ["Check"] = "M9 16.17 4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41L9 16.17Z",
        ["Flag"] = "M6 3h2v18H6V3Zm3 1h10.5l-2.4 3.75L19.5 11.5H9V4Z",
        ["Undo"] = "M12.5 8c-2.65 0-5.05.99-6.9 2.6L2 7v9h9l-3.62-3.62A8 8 0 0 1 20.4 15.1l2.37-.78A10.5 10.5 0 0 0 12.5 8Z",
        ["Copy"] = "M16 1H4c-1.1 0-2 .9-2 2v14h2V3h12V1Zm3 4H8c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h11c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2Zm0 16H8V7h11v14Z",
        ["Location"] = "M12 2a7 7 0 0 0-7 7c0 5.25 7 13 7 13s7-7.75 7-13a7 7 0 0 0-7-7Zm0 9.5a2.5 2.5 0 1 1 0-5 2.5 2.5 0 0 1 0 5Z",
        ["Bag"] = "M18 6h-2A4 4 0 0 0 8 6H6c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2Zm-6-2a2 2 0 0 1 2 2h-4a2 2 0 0 1 2-2Z",
        ["Info"] = "M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20Zm1 15h-2v-6h2v6Zm0-8h-2V7h2v2Z",
        ["Book"] = "M18 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V4a2 2 0 0 0-2-2Zm0 18H6V4h1v9l2.5-1.75L12 13V4h6v16Z",
        ["Filter"] = "M10 18h4v-2h-4v2ZM3 6v2h18V6H3Zm3 7h12v-2H6v2Z",
        ["Warning"] = "M1 21h22L12 2 1 21Zm12-3h-2v-2h2v2Zm0-4h-2v-4h2v4Z",

        // Gate 3 (Spawns + timers). Replaces ▶ 🔔 🔕 ✕ 🗑 🕒 — and the bell pair is the
        // reason a vector set matters: an emoji bell ignores Foreground entirely, so
        // "alert on" had to be signalled with opacity because the glyph could not be
        // coloured (see the old IconToggle template's comment).
        ["Play"] = "M8 5v14l11-7L8 5Z",
        ["Timer"] = "M15 1H9v2h6V1Zm-4 13h2V8h-2v6Zm8-6.6 1.4-1.4a10 10 0 0 0-1.4-1.4L17.6 6A9 9 0 1 0 19 7.4ZM12 20a7 7 0 1 1 0-14 7 7 0 0 1 0 14Z",
        ["Bell"] = "M12 22a2 2 0 0 0 2-2h-4a2 2 0 0 0 2 2Zm6-6v-5a6 6 0 0 0-5-5.91V4a1 1 0 1 0-2 0v1.09A6 6 0 0 0 6 11v5l-2 2v1h16v-1l-2-2Z",
        ["BellOff"] = "M20.5 19.1 4.9 3.5 3.5 4.9l2 2A6 6 0 0 0 6 11v5l-2 2v1h13.2l1.9 1.9 1.4-1.4ZM12 22a2 2 0 0 0 2-2h-4a2 2 0 0 0 2 2Zm6-6.3V11a6 6 0 0 0-5-5.91V4a1 1 0 1 0-2 0v1.09c-.42.06-.83.16-1.22.31L18 15.7Z",
        ["Trash"] = "M6 19a2 2 0 0 0 2 2h8a2 2 0 0 0 2-2V7H6v12ZM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4Z",

        // Gate 4 (Loot). Replaces the 🎯 that led the target-drops heading — and
        // which was baked into the HEADER STRING itself, so the breakout had to
        // string-replace it back out to render a shorter version of the same line. The
        // glyph is a control, so it is a vector; the sentence is text, so it is text.
        // Gate 5 (the widget). The card headers were the app's last big block of emoji —
        // fourteen of them, on the ONE surface that is always on screen, and therefore the
        // highest-value glyphs left to remove: #148 and #166 were emoji that did not
        // render at all under Wine, on the Linux and macOS builds that are EQBuddy's only
        // uncontested ground. Names describe the SHAPE, not the card, so a card can change
        // what it shows without stranding an icon called "Kills".
        ["Swords"] = "M6.5 2 2 6.5l5.9 5.9 2.3-2.3L6.5 2Zm11 0-2.3 2.3 2.3 2.3L22 2.1 17.5 2ZM2 17.5 6.5 22l7.4-7.4-2.3-2.3L2 17.5Zm12.4-4.6 7.6 7.6L20.2 22l-7.6-7.6 1.8-1.5Z",
        ["Heal"] = "M20 9h-5V4a1 1 0 0 0-1-1h-4a1 1 0 0 0-1 1v5H4a1 1 0 0 0-1 1v4a1 1 0 0 0 1 1h5v5a1 1 0 0 0 1 1h4a1 1 0 0 0 1-1v-5h5a1 1 0 0 0 1-1v-4a1 1 0 0 0-1-1Z",
        ["Skull"] = "M12 2C7.58 2 4 5.58 4 10c0 2.38 1.04 4.51 2.7 5.98V19a1 1 0 0 0 1 1h1.55v-2h1.5v2h2.5v-2h1.5v2H17.3a1 1 0 0 0 1-1v-3.02A7.98 7.98 0 0 0 20 10c0-4.42-3.58-8-8-8ZM9 12.5a2.5 2.5 0 1 1 0-5 2.5 2.5 0 0 1 0 5Zm6 0a2.5 2.5 0 1 1 0-5 2.5 2.5 0 0 1 0 5Z",
        ["Sparkle"] = "M12 2l2.45 6.75L21 11.2l-6.55 2.45L12 20.4l-2.45-6.75L3 11.2l6.55-2.45L12 2Z",
        ["Group"] = "M16 11a3 3 0 1 0 0-6 3 3 0 0 0 0 6Zm-8 0a3 3 0 1 0 0-6 3 3 0 0 0 0 6Zm0 2c-2.33 0-7 1.17-7 3.5V19h14v-2.5c0-2.33-4.67-3.5-7-3.5Zm8 0c-.29 0-.62.02-.97.05 1.16.84 1.97 1.97 1.97 3.45V19h6v-2.5c0-2.33-4.67-3.5-7-3.5Z",
        ["Coin"] = "M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20Zm0 18a8 8 0 1 1 0-16 8 8 0 0 1 0 16Zm0-13.5a5.5 5.5 0 1 0 0 11 5.5 5.5 0 0 0 0-11Z",
        ["Scales"] = "M13 4.82A2 2 0 0 0 14 3a2 2 0 1 0-4 0 2 2 0 0 0 1 1.82V6H4v2h1.62L2.5 15.5c0 1.93 1.57 3.5 3.5 3.5s3.5-1.57 3.5-3.5L6.38 8H11v11H7v2h10v-2h-4V8h4.62L14.5 15.5c0 1.93 1.57 3.5 3.5 3.5s3.5-1.57 3.5-3.5L18.38 8H20V6h-7V4.82ZM6 16.5 4.35 12.6h3.3L6 16.5Zm12 0-1.65-3.9h3.3L18 16.5Z",

        // Gate 5c (the widget's chrome). The last controls drawn as glyphs: the procs
        // bolt, the pet paw and the phone that opens EQBuddy Mobile.
        ["Bolt"] = "M11 21H8.5c-.6 0-1-.5-.85-1.05L9.5 13H6.6c-.7 0-.75-.4-.55-.8L11.6 2.6c.2-.35.5-.6.9-.6H15c.6 0 1 .5.85 1.05L14 10h3.2c.7 0 .8.45.55.9L11.9 20.4c-.2.35-.5.6-.9.6Z",
        ["Paw"] = "M8.35 10.5a2.6 2.6 0 1 0 0-5.2 2.6 2.6 0 0 0 0 5.2Zm7.3 0a2.6 2.6 0 1 0 0-5.2 2.6 2.6 0 0 0 0 5.2ZM4.1 15.4a2.3 2.3 0 1 0 0-4.6 2.3 2.3 0 0 0 0 4.6Zm15.8 0a2.3 2.3 0 1 0 0-4.6 2.3 2.3 0 0 0 0 4.6ZM12 12.2c-2.6 0-5.6 2.6-5.6 5.2 0 1.7 1.3 2.6 2.9 2.6 1.1 0 1.9-.5 2.7-.5s1.6.5 2.7.5c1.6 0 2.9-.9 2.9-2.6 0-2.6-3-5.2-5.6-5.2Z",
        ["Phone"] = "M17 1H7a2 2 0 0 0-2 2v18a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V3a2 2 0 0 0-2-2Zm0 18H7V5h10v14Zm-5 3.2a1.2 1.2 0 1 1 0-2.4 1.2 1.2 0 0 1 0 2.4Z",

        // The fight-side chip stack. Three kinds share one window and told themselves
        // apart by emoji — ⏳ a spawn countdown, 💤 a mez, 🐌 a slow — which is the
        // #148/#166 failure on the one surface a player watches mid-pull.
        //
        // The spawn chip takes the stopwatch it already had; sleep becomes a crescent.
        // The snail did NOT survive as a snail: a spiral shell legible at 12px is more
        // drawing than this table should carry, and the chip's own label already says
        // what it is ("Turgur's Insects 75%") — so slow is an hourglass, which is the
        // thing a slow actually does. Meaning lives in the words; the icon separates the
        // kinds.
        ["Moon"] = "M12.5 2A10 10 0 1 0 22 15.2 8.5 8.5 0 0 1 12.5 2Z",
        ["ChevronsDown"] = "M3.9 2.2 1.8 4.3 12 12.5 22.2 4.3l-2.1-2.1L12 8.3Z"
                         + "M3.9 11.4 1.8 13.5 12 21.7l10.2-8.2-2.1-2.1L12 17.5Z",
        ["Hourglass"] = "M6 2v2l5 6-5 6v2h12v-2l-5-6 5-6V2H6Z",

        ["Target"] = "M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8Zm8.94 3A8.994 8.994 0 0 0 13 3.06V1h-2v2.06A8.994 8.994 0 0 0 3.06 11H1v2h2.06A8.994 8.994 0 0 0 11 20.94V23h2v-2.06A8.994 8.994 0 0 0 20.94 13H23v-2h-2.06ZM12 19c-3.87 0-7-3.13-7-7s3.13-7 7-7 7 3.13 7 7-3.13 7-7 7Z",
    };

    // ---- reward slot silhouettes ----
    //
    // The Quests mockup's centrepiece was a grid of per-ITEM reward icons. EQBuddy cannot
    // produce those: ItemCatalog carries Name/StatsText/Slots/Skill/QuestFlagged and
    // nothing else, and the 2026-08-15 spike established that although the game ships the
    // icon sheets, nothing maps an item to one (the wiki's lucy_img_ID was disproved).
    // Drawing them anyway would mean inventing an icon per item — confidently wrong art
    // on a surface whose entire value is being trustworthy (docs/DesignSystem.md §8a).
    //
    // These are driven by data we DO have — the item's own Slots and Skill — so the worst
    // case is a ring drawn for a ring. Anything the catalog doesn't know gets SlotItem,
    // which claims nothing.

    private static readonly Dictionary<string, string> Slots = new(StringComparer.Ordinal)
    {
        ["SlotWeapon"] = "M11 2h2v11h-2V2Zm-3.5 11h9v2H13v7h-2v-7H7.5v-2Z",
        ["SlotBlunt"] = "M7.5 2h9v6h-9V2ZM11 8h2v11h-2V8Zm-2 11h6v3H9v-3Z",
        ["SlotRanged"] = "M13 2h7v7h-2V5.4L6.4 17H10v2H3v-7h2v3.6L16.6 4H13V2Z",
        ["SlotShield"] = "M12 2 4 5v6c0 5 3.4 9.6 8 11 4.6-1.4 8-6 8-11V5l-8-3Z",
        ["SlotHead"] = "M12 2a8 8 0 0 0-8 8v8h5v-6h6v6h5v-8a8 8 0 0 0-8-8Zm-3 18v2h6v-2H9Z",
        ["SlotBody"] = "M8.5 2 4 4.5V11h3v11h10V11h3V4.5L15.5 2 12 5.5 8.5 2Z",
        ["SlotHands"] = "M7 8V4a1.5 1.5 0 0 1 3 0v4h1V3a1.5 1.5 0 0 1 3 0v5h1V5a1.5 1.5 0 0 1 3 0v9a8 8 0 0 1-8 8H8a4 4 0 0 1-4-4V9.5a1.5 1.5 0 0 1 3 0V8Z",
        ["SlotFeet"] = "M6 2h5v9c0 2 1 3 3 3.5l4 1.3a3 3 0 0 1 2 2.9V22H6V2Z",
        ["SlotBelt"] = "M2 9h20v6H2V9Zm8 1.5h4v3h-4v-3Z",
        ["SlotNeck"] = "M12 3a7 7 0 0 0-7 7h2a5 5 0 0 1 10 0h2a7 7 0 0 0-7-7Zm0 8.5a3.25 3.25 0 1 0 0 6.5 3.25 3.25 0 0 0 0-6.5Z",
        ["SlotRing"] = "M9 2h6v3.6a7 7 0 1 1-6 0V2Zm3 6a4.5 4.5 0 1 0 0 9 4.5 4.5 0 0 0 0-9Z",
        ["SlotEar"] = "M12 2a6 6 0 0 0-6 6h2.5a3.5 3.5 0 1 1 7 0c0 2-1.5 3-2.5 4s-1.5 2-1.5 3.5h2.5c0-1 .5-1.5 1.5-2.5S18 10.5 18 8a6 6 0 0 0-6-6Zm-1 19.5a1.75 1.75 0 1 0 3.5 0 1.75 1.75 0 0 0-3.5 0Z",
        ["SlotBack"] = "M12 2 6 5 4 22h4l1-9 3 3 3-3 1 9h4L18 5l-6-3Z",
        ["SlotItem"] = "M12 2 3 7v10l9 5 9-5V7l-9-5Zm0 2.3L18.5 8 12 11.7 5.5 8 12 4.3ZM5 9.7l6 3.4v6.6l-6-3.3V9.7Zm8 10V13.1l6-3.4v6.7l-6 3.3Z",
    };

    /// <summary>Every icon, UI vocabulary and slot silhouettes alike.</summary>
    public static readonly IReadOnlyDictionary<string, string> All =
        Ui.Concat(Slots).ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);

    public static readonly IReadOnlyList<string> Names = [.. All.Keys];

    /// <summary>Path data for an icon. Throws on an unknown name rather than drawing
    /// nothing: an invisible control is the failure mode this whole table exists to
    /// stop, and every name is a compile-time constant at the call site.</summary>
    public static string Path(string name) =>
        All.TryGetValue(name, out var data)
            ? data
            : throw new ArgumentOutOfRangeException(nameof(name), name,
                "No such icon. Add it to IconPaths, don't fall back to an emoji.");

    // ---- item → silhouette ----

    /// <summary>Which silhouette an item's own catalog record earns. Slots and skills
    /// come out of the harvested wiki with real dirt in them — case varies, and the table
    /// carries <c>SHOULDER</c>, <c>FINGERS</c>, <c>SECONDAY</c> and a stray <c>BACK,</c>
    /// — so matching is on letters only, uppercased.
    ///
    /// The weapon SKILL wins over the slot when there is one: "PRIMARY, 2H Blunt" is a
    /// hammer, and drawing a sword for it would be the confidently-wrong art §8a rules
    /// out. A SECONDARY with no weapon skill is a shield.</summary>
    public static string ForItem(IEnumerable<string>? slots, string? skill)
    {
        var s = Normalize(skill);
        if (s.Contains("BLUNT")) return "SlotBlunt";
        if (s.Contains("ARCHERY") || s.Contains("THROWING")) return "SlotRanged";
        if (s.Contains("SLASHING") || s.Contains("PIERCING") || s.Contains("HANDTOHAND"))
            return "SlotWeapon";
        if (s.Contains("SHIELD")) return "SlotShield";

        foreach (var raw in slots ?? [])
        {
            var slot = Normalize(raw);
            if (slot.Length == 0) continue;
            // Longest-prefix wins so SHOULDER/SHOULDERS and FINGER/FINGERS both land,
            // and so does the catalog's one "SECONDAY".
            if (slot.StartsWith("PRIMARY")) return "SlotWeapon";
            if (slot.StartsWith("SECOND")) return "SlotShield";
            if (slot.StartsWith("HEAD") || slot.StartsWith("FACE")) return "SlotHead";
            if (slot.StartsWith("CHEST") || slot.StartsWith("ARMS")
                || slot.StartsWith("SHOULDER") || slot.StartsWith("LEGS")) return "SlotBody";
            if (slot.StartsWith("HAND") || slot.StartsWith("WRIST")) return "SlotHands";
            if (slot.StartsWith("FEET") || slot.StartsWith("FOOT")) return "SlotFeet";
            if (slot.StartsWith("WAIST")) return "SlotBelt";
            if (slot.StartsWith("NECK")) return "SlotNeck";
            if (slot.StartsWith("FINGER")) return "SlotRing";
            if (slot.StartsWith("EAR")) return "SlotEar";
            if (slot.StartsWith("BACK")) return "SlotBack";
            if (slot.StartsWith("RANGE") || slot.StartsWith("AMMO")) return "SlotRanged";
        }
        // Quest turn-ins, components, containers, anything unequippable: a crate, which
        // says "an item" and nothing more.
        return "SlotItem";
    }

    private static string Normalize(string? value) =>
        value is null ? "" : new string([.. value.Where(char.IsLetter)]).ToUpperInvariant();
}
