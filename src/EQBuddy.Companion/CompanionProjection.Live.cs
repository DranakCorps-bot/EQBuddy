using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Companion;

// The surfaces that follow the fight: mez chips, the buff set, the three combat
// boards, loot and watches, and the XP/AA strip. All read the tick's shared state and
// build nothing the desktop hasn't already computed.
public static partial class CompanionProjection
{
    /// <summary>Inside the last server tick — the same threshold MezChipsWindow warns
    /// at, because a mez with less than one tick left is one you act on now.</summary>
    public const double MezWarningSeconds = 6;

    private static CompanionMezSection BuildMez(IReadOnlyList<MezState> mezzes, DateTime now)
    {
        // Same numbering rule as the desktop chips: a name is numbered ONLY when it
        // appears more than once, in snapshot order (soonest expiry first), so a lone
        // mezzed mob reads as itself rather than "Froglok (1)".
        var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in mezzes) totals[m.Target] = totals.GetValueOrDefault(m.Target) + 1;

        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var chips = new List<CompanionMezChip>(mezzes.Count);
        foreach (var m in mezzes)
        {
            var number = seen[m.Target] = seen.GetValueOrDefault(m.Target) + 1;
            var remaining = m.RemainingSeconds(now);
            double? fraction = m.ExpiresAt is { } e && e > m.LandedAt
                ? Math.Clamp((now - m.LandedAt).TotalSeconds / (e - m.LandedAt).TotalSeconds, 0, 1)
                : null;
            chips.Add(new CompanionMezChip(
                Name: totals[m.Target] > 1 ? $"{m.Target} ({number})" : m.Target,
                RemainingSeconds: remaining,
                Warning: remaining is <= MezWarningSeconds,
                Fraction: fraction,
                Detail: $"{m.Spell} by {m.Caster}"));
        }
        return new CompanionMezSection(chips);
    }

    private static CompanionBuffsSection BuildBuffs(
        IReadOnlyList<(string Class, IReadOnlyList<BuffSetEntryState> Entries)> sets,
        IReadOnlyList<BuffLossEntry> losses,
        DateTime now)
    {
        var groups = sets.Select(s => new CompanionBuffGroup(s.Class,
            [.. s.Entries.Select(e => new CompanionBuffRow(
                e.Spell, StatusName(e.Status), e.RemainingSeconds, e.Estimated))])).ToList();

        var lost = losses.Take(MaxRows)
            .Select(l => new CompanionBuffLoss(l.Spell, l.Cause, Math.Max(0, (now - l.Time).TotalSeconds)))
            .ToList();
        return new CompanionBuffsSection(groups, lost);

        // The page colors the state itself, so what travels is the state's name.
        static string StatusName(BuffSetStatus status) => status switch
        {
            BuffSetStatus.Active => "active",
            BuffSetStatus.Expiring => "expiring",
            BuffSetStatus.Missing => "missing",
            _ => "notSeen",
        };
    }

    private static CompanionCombatSection BuildCombat(StatsSnapshot? s)
    {
        if (s is null) return new CompanionCombatSection([]);
        var f = s.LastFight;
        var fightSeconds = f?.DurationSeconds ?? 0;
        var petFightTotal = f?.PetAbilities.Sum(p => p.Total) ?? 0;
        var petSessionTotal = s.PetAbilities.Sum(p => p.Total);

        return new CompanionCombatSection(
        [
            Board("damage", "Damage", "dps", f?.ByAbility ?? [], s.DamageBySource, f?.Dps ?? 0, s.SessionDps),
            Board("healing", "Healing", "hps", f?.HealsBySpell ?? [], s.HealsBySpell, f?.Hps ?? 0, s.Hps),
            Board("pet", s.PetName.Length > 0 ? $"Pet — {s.PetName}" : "Pet", "dps",
                f?.PetAbilities ?? [], s.PetAbilities,
                fightSeconds > 0 ? petFightTotal / fightSeconds : 0,
                s.CombatSeconds > 0 ? petSessionTotal / s.CombatSeconds : 0),
        ]);

        CompanionCombatBoard Board(
            string key, string label, string rateLabel,
            IReadOnlyList<SourceDamage> fight, IReadOnlyList<SourceDamage> session,
            double fightRate, double sessionRate) =>
            new(key, label,
                // The breakout's own subheaders, word for word.
                FightHeader: f is null
                    ? "No fights yet"
                    : $"{f.Name} · {f.DurationSeconds:0}s · {f.Outcome} · {fightRate:0.#} {rateLabel}",
                SessionHeader: $"Session · {s.CombatSeconds / 60:0}m in combat · {sessionRate:0.#} {rateLabel}",
                Fight: Rows(fight, fightSeconds, rateLabel),
                Session: Rows(session, s.CombatSeconds, rateLabel));
    }

    /// <summary>Ability rows through the SHARED builder the History view uses, so the
    /// phone's "1,204 · ×18 · avg 66.9 · 41.2 dps" line is the desktop's line.</summary>
    private static IReadOnlyList<CompanionAbilityRow> Rows(
        IReadOnlyList<SourceDamage> stats, double seconds, string rateLabel)
    {
        if (stats.Count == 0) return [];
        var grand = Math.Max(1, stats.Sum(d => d.Total));
        var built = HistoryPresentation.BuildBreakdownRows(stats, seconds, rateLabel, MaxRows);
        return [.. built.Select((r, i) => new CompanionAbilityRow(
            r.Name, r.Value, r.Fraction, 100.0 * stats[i].Total / grand, stats[i].Total, stats[i].Hits))];
    }

    private static CompanionLootSection BuildLoot(StatsSnapshot? s)
    {
        if (s is null) return new CompanionLootSection(0, 0, [], [], []);
        return new CompanionLootSection(
            Total: s.LootTotal,
            CraftedTotal: s.CraftedTotal,
            Items: [.. s.Loot.OrderByDescending(l => l.Count).ThenBy(l => l.Item, StringComparer.OrdinalIgnoreCase)
                .Take(MaxRows).Select(l => new CompanionCountRow(l.Item, l.Count))],
            Crafted: [.. s.Crafted.Take(MaxRows).Select(c => new CompanionCountRow(c.Name, c.Count))],
            // Watch rides with loot rather than owning a surface: the counters are one
            // short strip, and "what have I got" is one question on a phone.
            Watch: [.. s.Tracked.Where(t => t.TotalQuantity > 0).Take(MaxRows)
                .Select(t => new CompanionWatchRow(t.Name, t.TotalQuantity, t.PerHour, t.PerActiveHour, t.LastItem))]);
    }

    private static CompanionProgressSection BuildProgress(
        StatsSnapshot? s, int? level, LevelUnlockSet? unlocks, RaidKillLedger? raids)
    {
        unlocks ??= LevelUnlockSet.Empty;
        var stats = s ?? new StatsSnapshot();
        var catalog = RaidTargetCatalog.Default;
        var defeated = raids?.DefeatedCount() ?? 0;
        return new CompanionProgressSection(
            XpPercent: s?.XpPercent ?? 0,
            XpPerHour: s?.XpPerHour ?? 0,
            XpPerActiveHour: s?.XpPerActiveHour ?? 0,
            HoursToLevel: s?.HoursToLevel,
            AaGained: s?.AaGained ?? 0,
            AaTotal: s?.AaTotal ?? 0,
            AaPerHour: s?.AaPerHour ?? 0,
            Level: level,
            UnlocksLabel: level is { } lv && unlocks.Count > 0 ? LevelUnlockText.NewAtLevelLabel(lv) : null,
            Unlocks:
            [
                .. unlocks.Aas.Take(MaxRows)
                    .Select(a => new CompanionUnlockRow(a.Name, LevelUnlockText.RowValue(a))),
                .. unlocks.Spells.Take(MaxRows)
                    .Select(sp => new CompanionUnlockRow(sp.Name, LevelUnlockText.SpellRowValue(sp))),
            ],
            // The tab strip, from Core's ProgressSurface and UI.Shared's ProgressTheme —
            // the SAME two the desktop window reads. Sending it rather than rebuilding it
            // on the page is the #210 fix applied before the bug: the phone can't name a
            // different tab, order them differently, or compute a different badge.
            Tabs: [.. ProgressTheme
                .Tabs(stats, unlocks.Count, defeated, catalog.BossCount)
                .Select(t => new CompanionProgressTab(t.Key, t.Label, t.Value))],
            Wealth: BuildWealth(stats),
            Faction: [.. stats.Faction.Take(MaxRows)
                .Select(f => new CompanionCountRow(f.Faction, f.Net))],
            Raids: BuildRaids(raids, catalog));
    }

    /// <summary>Coin and motes. Coin arrives PRE-FORMATTED because the phone cannot do
    /// better than the app's own FormatCoin, and two formatters for one number is exactly
    /// how a phone and a window start disagreeing about how much plat you made.</summary>
    private static CompanionWealthBlock BuildWealth(StatsSnapshot s)
    {
        var motes = Motes.Summarize(s.Loot, s.Elapsed);
        return new CompanionWealthBlock(
            Total: StatsSnapshot.FormatCoin(s.Copper),
            Corpses: StatsSnapshot.FormatCoin(s.CorpseCopper),
            Sales: StatsSnapshot.FormatCoin(s.VendorCopper),
            PerHour: StatsSnapshot.FormatCoin(s.CopperPerHour),
            CoinDrops: s.CoinDrops,
            SalesCount: s.SalesCount,
            // Sold items are drops too (#74) — the same rows the desktop's Money surface
            // lists, with the count folded into the value column.
            Sold: [.. s.SoldItems.Take(MaxRows).Select(i => new CompanionCountRow(i.Item, i.Count))],
            MotesSummary: MotesPresentation.Summary(motes),
            Motes: [.. motes.Tiers.Take(MaxRows).Select(t => new CompanionCountRow(t.Item, t.Count))]);
    }

    /// <summary>Raid targets per zone. The row DETAIL is built here rather than on the
    /// page for the reason the desktop card states: the badge is the highest difficulty
    /// PROVEN by a witnessed kill, and a page that assembled its own could show a tier the
    /// ledger never recorded.</summary>
    private static CompanionRaidsBlock BuildRaids(RaidKillLedger? raids, RaidTargetCatalog catalog)
    {
        if (raids is null) return new CompanionRaidsBlock(0, catalog.BossCount, []);
        var zones = new List<CompanionRaidZone>();
        foreach (var zone in catalog.Zones)
        {
            var bosses = new List<CompanionRaidBoss>();
            foreach (var boss in zone.Bosses)
            {
                var rec = raids.For(boss);
                var cleared = rec is { } r && (r.Kills > 0 || r.AchievementComplete);
                var badge = rec?.HighestDifficulty() is { } hd ? $"D{hd} · " : "";
                bosses.Add(new CompanionRaidBoss(boss, cleared, rec switch
                {
                    { Kills: > 0 } k => $"{badge}{(k.Kills > 1 ? $"×{k.Kills} · " : "")}last {k.LastKill:MMM d}",
                    { AchievementComplete: true } => "cleared (from achievements)",
                    _ => "",
                }));
            }
            zones.Add(new CompanionRaidZone(zone.Zone, bosses.Count(b => b.Cleared),
                zone.Bosses.Length, bosses));
        }
        return new CompanionRaidsBlock(raids.DefeatedCount(), catalog.BossCount, zones);
    }
}
