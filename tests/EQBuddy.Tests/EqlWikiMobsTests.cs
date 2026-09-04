using System.Net.Http;
using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>Mob-page parsing for the Loot card's target-drops block. The fixture is the
/// real Lockjaw page (fetched 2026-08-06); both named and regular mobs use
/// {{Namedmobpage}}, regular ones at article-titled pages ("A Spite Golem").</summary>
public class EqlWikiMobsTests
{
    /// <summary>A stubbed fetch answering as the wiki does: the page's own title beside
    /// its wikitext. Tests that care about redirects pass a title different from the one
    /// requested — which is the whole point of WikiPageText.</summary>
    private static Task<WikiPageText?> Served(string title, string? wikitext) =>
        Task.FromResult<WikiPageText?>(wikitext is null ? null : new WikiPageText(title, wikitext));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "fixtures", "wiki", name + ".txt"));

    [Fact]
    public void LockjawPageParsesDropsWithRarity()
    {
        var mob = EqlWikiMobService.Parse(Fixture("lockjaw-mob"), "Lockjaw");
        Assert.Equal("Lockjaw", mob.Name);
        Assert.Equal("Oasis of Marr", mob.Zone);
        Assert.Equal("25", mob.Level);
        Assert.Equal("Common", mob.Drops.Single(d => d.Item == "Lockjaw Hide Vest").Rarity);
        Assert.Equal("Uncommon", mob.Drops.Single(d => d.Item == "Gator Meat").Rarity);
        // Un-annotated entries keep an empty rarity rather than an invented one.
        Assert.Equal("", mob.Drops.Single(d => d.Item == "Gnome Meat").Rarity);
        Assert.Equal(8, mob.Drops.Count);
    }

    [Fact]
    public async Task RegularMobsResolveViaTheArticleTitledPage()
    {
        // SessionStats names arrive article-stripped, first letter capitalized ("Spite
        // golem"); the wiki page is "A Spite Golem" (titles are case-sensitive past the
        // first letter). The candidate ladder bridges both gaps — this exact case shipped
        // broken once as NOT ON WIKI (2026-08-06 screenshot round).
        var requested = new List<string>();
        var svc = new EqlWikiMobService(
            Path.Combine(Path.GetTempPath(), $"mobcache-{Guid.NewGuid():N}"),
            title =>
            {
                requested.Add(title);
                return Served(title, title == "A Spite Golem"
                    ? "{{Namedmobpage\n| name = A Spite Golem\n| known_loot = \n{{:Apothic Crown}}\n}}"
                    : null);
            });
        var result = await svc.LookupAsync("Spite golem");
        Assert.Equal(ItemLookupState.Live, result.State);
        Assert.Equal(["Spite golem", "A spite golem", "The spite golem", "Spite Golem", "A Spite Golem"],
            requested);
        Assert.Equal("Apothic Crown", result.Mob!.Drops.Single().Item);
    }

    [Fact]
    public async Task ZoneDisambiguatedPagesResolveAndTheCurrentZoneWins()
    {
        // The orc-legionnaire-mid-fight case (David, live, 2026-08-07): the bare-name
        // page is a broken redirect (returns nothing), the real drops live at
        // "Orc Legionnaire (Crushbone)" and "(Deathfist)". The zone-suffix-stripped
        // fuzzy compare admits both; the player's zone picks Crushbone.
        var fetched = new List<string>();
        var svc = new EqlWikiMobService(
            Path.Combine(Path.GetTempPath(), $"mobcache-{Guid.NewGuid():N}"),
            title =>
            {
                fetched.Add(title);
                return Served(title, title switch
                {
                    "Orc Legionnaire (Crushbone)" =>
                        "{{Namedmobpage\n| name = Orc Legionnaire\n| known_loot = \n{{:Crushbone Belt}}\n}}",
                    "Orc Legionnaire (Deathfist)" =>
                        "{{Namedmobpage\n| name = Orc Legionnaire\n| known_loot = \n{{:Deathfist Slashed Belt}}\n}}",
                    _ => null,   // bare page: broken redirect, every exact candidate misses
                });
            },
            _ => Task.FromResult(new List<string>
                { "Orc legionnaire", "Orc Legionnaire (Deathfist)", "Orc Legionnaire (Crushbone)" }));

        var result = await svc.LookupAsync("Orc legionnaire", currentZone: "Crushbone");
        Assert.Equal(ItemLookupState.Live, result.State);
        Assert.Equal("Crushbone Belt", result.Mob!.Drops.Single().Item);
        // Zoneless bare page was still tried first (it outranks FOREIGN zones).
        Assert.Contains("Orc legionnaire", fetched);

        // Without a zone hint, the zoneless candidate still leads and the first
        // resolvable zone page wins — no dead end, no wrong-first bias.
        var noZone = await svc.LookupAsync("Orc legionnaireX".Replace("X", ""), "");
        Assert.Equal(ItemLookupState.Cached, noZone.State);   // second call hits the cache
    }

    [Fact]
    public async Task TheNamedMobsResolveViaTheirArticle()
    {
        // Normalize strips "the " like any article, so The Prophet arrives as "Prophet" —
        // and bare "Prophet" is missing on the wiki (David's report: a well-known named
        // showing no drops). The ladder must try the "The" forms.
        var svc = new EqlWikiMobService(
            Path.Combine(Path.GetTempPath(), $"mobcache-{Guid.NewGuid():N}"),
            title => Served(title, title == "The Prophet"
                ? "{{Namedmobpage\n| name = The Prophet\n| known_loot = \n{{:Prophet Skull}}\n}}"
                : null));
        var result = await svc.LookupAsync("Prophet");
        Assert.Equal(ItemLookupState.Live, result.State);
        Assert.Equal("Prophet Skull", result.Mob!.Drops.Single().Item);
    }

    /// <summary>The fuzzy fallback (David, 2026-08-06): when every exact form misses,
    /// wiki search results are accepted under the spawn catalog's bounded-edit-distance
    /// rule — a one-letter drift resolves, a merely-related page never does.</summary>
    [Fact]
    public async Task WikiSearchRescuesANearMissButNeverAStranger()
    {
        var svc = new EqlWikiMobService(
            Path.Combine(Path.GetTempPath(), $"mobcache-{Guid.NewGuid():N}"),
            title => Served(title, title == "Emperor Crushbone"
                ? "{{Namedmobpage\n| name = Emperor Crushbone\n| known_loot = \n{{:Crown of the Emperor}}\n}}"
                : null),
            _ => Task.FromResult<List<string>>(["Emperor Crushbone"]));
        // One letter off — every exact candidate misses, search + fuzzy resolve it.
        var result = await svc.LookupAsync("Emperor Crushbon");
        Assert.Equal(ItemLookupState.Live, result.State);
        Assert.Equal("Crown of the Emperor", result.Mob!.Drops.Single().Item);

        // A dissimilar search hit is rejected: better no answer than a wrong creature.
        var strict = new EqlWikiMobService(
            Path.Combine(Path.GetTempPath(), $"mobcache-{Guid.NewGuid():N}"),
            _ => Task.FromResult<WikiPageText?>(null),
            _ => Task.FromResult<List<string>>(["Crushbone (Zone)"]));
        Assert.Equal(ItemLookupState.NotFound,
            (await strict.LookupAsync("Emperor Crushbon")).State);
    }

    [Fact]
    public async Task MissingMobIsNotFoundAfterAllCandidates()
    {
        var svc = new EqlWikiMobService(
            Path.Combine(Path.GetTempPath(), $"mobcache-{Guid.NewGuid():N}"),
            _ => Task.FromResult<WikiPageText?>(null),
            _ => Task.FromResult<List<string>>([]));   // stubbed: no network from a unit test
        var result = await svc.LookupAsync("Utterly Fictional");
        Assert.Equal(ItemLookupState.NotFound, result.State);
        Assert.Equal(ItemLookupState.Offline,
            (await new EqlWikiMobService(
                Path.Combine(Path.GetTempPath(), $"mobcache-{Guid.NewGuid():N}"),
                _ => throw new HttpRequestException("no network"))
                .LookupAsync("Anything")).State);
    }

    // ---- #65 round five (Frankthetankk): the article-drop, caught a SECOND time ----

    /// <summary>The wiki API is asked with redirects=1, so a request for the
    /// article-stripped name SUCCEEDS by landing on the real page. v1.57.1 fixed the
    /// packs to print the resolved title — but the resolver was recording the title it
    /// ASKED for, so the resolved title was the stripped one and every link kept the
    /// wrong name. This pins the page's own title as the answer, which is the thing
    /// contribution packs print.</summary>
    // ---------------- the re-check (#226, LeBigNasty + Frankthetankk; plan: FABLE.md) ----------------

    private const string LockjawWithVest =
        "{{Namedmobpage\n| name = Lockjaw\n| known_loot = \n{{:Lockjaw Hide Vest}}\n}}";
    private const string LockjawWithoutVest =
        "{{Namedmobpage\n| name = Lockjaw\n| known_loot = \n{{:Gator Meat}}\n}}";

    private static string TempCache() => Path.Combine(Path.GetTempPath(), $"mobcache-{Guid.NewGuid():N}");

    /// <summary>The ✦ runs on a 7-day per-page cache, so a player who corrects the wiki —
    /// the thing the ✦ ASKS them to do — could not clear the flag for a week. A bypass
    /// asks the wiki now even though the cache still calls the page fresh.</summary>
    [Fact]
    public async Task ABypassRefetchesAPageTheCacheStillCallsFresh()
    {
        var dir = TempCache();
        try
        {
            var served = LockjawWithoutVest;
            var fetches = 0;
            var svc = new EqlWikiMobService(dir, title => { fetches++; return Served(title, served); });

            var first = await svc.LookupAsync("Lockjaw");
            Assert.Equal(ItemLookupState.Live, first.State);
            Assert.DoesNotContain(first.Mob!.Drops, d => d.Item == "Lockjaw Hide Vest");

            // The player edits the wiki. Without a bypass the cache answers for a week.
            served = LockjawWithVest;
            var cached = await svc.LookupAsync("Lockjaw");
            Assert.Equal(ItemLookupState.Cached, cached.State);
            Assert.Equal(1, fetches);

            var rechecked = await svc.LookupAsync("Lockjaw", bypassCache: true);
            Assert.Equal(ItemLookupState.Live, rechecked.State);
            Assert.Equal(2, fetches);
            Assert.Contains(rechecked.Mob!.Drops, d => d.Item == "Lockjaw Hide Vest");

            // …and the re-read is what the cache holds from now on.
            var after = await svc.LookupAsync("Lockjaw");
            Assert.Equal(ItemLookupState.Cached, after.State);
            Assert.Contains(after.Mob!.Drops, d => d.Item == "Lockjaw Hide Vest");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>An offline re-check must not demote a known answer to "not checked":
    /// the old read comes back as StaleCache with the OLD FetchedAt, never Offline.
    /// The #217 rule — pending is not nothing-new — and the easiest thing to get wrong
    /// with a bypass, because the obvious implementation skips the cache entirely.</summary>
    [Fact]
    public async Task AFailedBypassReturnsTheStaleReadWithItsOldTimestamp()
    {
        var dir = TempCache();
        try
        {
            var first = await new EqlWikiMobService(dir, t => Served(t, LockjawWithVest)).LookupAsync("Lockjaw");
            var fetchedAt = first.FetchedAt!.Value;

            var offline = new EqlWikiMobService(dir, _ => throw new HttpRequestException("offline"));
            var again = await offline.LookupAsync("Lockjaw", bypassCache: true);

            Assert.Equal(ItemLookupState.StaleCache, again.State);
            Assert.Equal(fetchedAt, again.FetchedAt);
            Assert.Contains(again.Mob!.Drops, d => d.Item == "Lockjaw Hide Vest");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>WHY <c>Forget</c> is not in the re-check path, kept as an executable note.
    ///
    /// Deleting the cache file first LOOKS like the honest way to force a re-read. It is
    /// not: <see cref="EqlWikiMobService.LookupAsync"/> reads the cache at the top, so a
    /// prior <c>Forget</c> leaves nothing for the offline fallback, and a failed bypass
    /// returns <c>Offline</c> instead of the stale read. That demotes a lit ✦ to "not
    /// checked" the moment the wiki is unreachable — the #217 rule inverted, on a surface
    /// built to honour it. Both windows shipped exactly that in 1.99.1; Fable 5 found it
    /// in the H4 last-look, reachable only with the wiki down, which is why neither the
    /// suite nor the staged screenshot saw it.
    ///
    /// A bypass overwrites the file on success anyway, so the delete bought nothing.
    /// <c>Forget</c> stays as an API for a caller that genuinely wants the file gone;
    /// this test is here so the next person to reach for it sees the cost first.</summary>
    [Fact]
    public async Task ForgettingBeforeABypassIsWhatCostsTheOfflineFallback()
    {
        var dir = TempCache();
        try
        {
            var first = await new EqlWikiMobService(dir, t => Served(t, LockjawWithVest)).LookupAsync("Lockjaw");
            var fetchedAt = first.FetchedAt!.Value;

            // The path both windows shipped: forget, then bypass, then the wiki is down.
            var offline = new EqlWikiMobService(dir, _ => throw new HttpRequestException("offline"));
            offline.Forget("Lockjaw");
            var afterForget = await offline.LookupAsync("Lockjaw", bypassCache: true);
            Assert.Equal(ItemLookupState.Offline, afterForget.State);   // the defect, in one line
            Assert.Null(afterForget.Mob);

            // The path they ship now: bypass alone keeps the stale read and its age.
            var kept = new EqlWikiMobService(dir, t => Served(t, LockjawWithVest));
            await kept.LookupAsync("Lockjaw");                          // re-seed the file
            var offlineAgain = new EqlWikiMobService(dir, _ => throw new HttpRequestException("offline"));
            var stale = await offlineAgain.LookupAsync("Lockjaw", bypassCache: true);
            Assert.Equal(ItemLookupState.StaleCache, stale.State);
            Assert.NotNull(stale.Mob);
            Assert.True(stale.FetchedAt > fetchedAt - TimeSpan.FromMinutes(1));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>Forget keys on the REQUESTED name — the same key the windows' session memo
    /// uses — so one name addresses both stale layers. Here the page was SERVED under a
    /// different title (a redirect), and the file still goes.</summary>
    [Fact]
    public async Task ForgetRemovesTheCacheFileKeyedOnTheRequestedName()
    {
        var dir = TempCache();
        try
        {
            var fetches = 0;
            var svc = new EqlWikiMobService(dir, t => { fetches++; return Served("The Spiroc Lord", LockjawWithVest); });
            await svc.LookupAsync("Spiroc Lord");
            Assert.Single(Directory.GetFiles(dir));

            svc.Forget("Spiroc Lord");
            Assert.Empty(Directory.GetFiles(dir));
            svc.Forget("Spiroc Lord");   // twice is harmless

            await svc.LookupAsync("Spiroc Lord");
            Assert.Equal(2, fetches);    // nothing cached: it asked again
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>At most two requests in flight, across every caller. A Drops tab with
    /// thirteen creatures used to send thirteen at once — the burst the re-check button
    /// was feared to add already existed. Eight lookups are started against a fetcher
    /// that parks every call; only two may have been ASKED until one is released.</summary>
    [Fact]
    public async Task NoMoreThanTwoFetchesAreEverInFlight()
    {
        var dir = TempCache();
        try
        {
            var parked = new List<TaskCompletionSource<WikiPageText?>>();
            var gate = new object();
            var svc = new EqlWikiMobService(dir, _ =>
            {
                var tcs = new TaskCompletionSource<WikiPageText?>();
                lock (gate) parked.Add(tcs);
                return tcs.Task;
            });

            var lookups = Enumerable.Range(0, 8)
                .Select(i => svc.LookupAsync($"Creature{i}"))
                .ToList();

            // **Not `await Task.Delay(100)` — that was a claim about the machine.** The
            // eight lookups reach the fetcher on the thread pool, so "how many have been
            // asked" 100 ms later is a fact about how many cores are free; a hosted runner
            // failed this line with 1, which reads as a broken cap and is a slow start.
            // Polling asserts the same two things without the assumption: the cap is
            // never EXCEEDED (checked on every poll, which is strictly more coverage than
            // one sample) and it is eventually REACHED.
            Assert.Equal(EqlWikiMobService.MaxInFlight, await AskedToSettle(EqlWikiMobService.MaxInFlight));

            // Releasing one admits exactly one more.
            TaskCompletionSource<WikiPageText?> one; lock (gate) one = parked[0];
            one.SetResult(new WikiPageText("Creature0", LockjawWithVest));
            Assert.Equal(EqlWikiMobService.MaxInFlight + 1,
                await AskedToSettle(EqlWikiMobService.MaxInFlight + 1));

            async Task<int> AskedToSettle(int cap)
            {
                var deadline = DateTime.UtcNow.AddSeconds(30);
                while (true)
                {
                    int asked; lock (gate) asked = parked.Count;
                    Assert.True(asked <= cap,
                        $"more than {cap} fetches had been asked for at once: {asked}");
                    if (asked == cap || DateTime.UtcNow > deadline) return asked;
                    await Task.Delay(10);
                }
            }

            // Drain so the temp dir can be deleted; each creature resolves on its first candidate.
            while (true)
            {
                List<TaskCompletionSource<WikiPageText?>> pending;
                lock (gate) pending = parked.Where(t => !t.Task.IsCompleted).ToList();
                if (pending.Count == 0 && lookups.All(l => l.IsCompleted)) break;
                foreach (var t in pending) t.TrySetResult(new WikiPageText("x", LockjawWithVest));
                await Task.Delay(20);
            }
            await Task.WhenAll(lookups);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task ARedirectedLookupKeepsThePagesOwnTitleNotTheOneWeAskedFor()
    {
        var svc = new EqlWikiMobService(
            Path.Combine(Path.GetTempPath(), $"mobcache-{Guid.NewGuid():N}"),
            // The log normalizer strips "The", so EQBuddy asks for "Spiroc Lord" — and
            // the wiki redirects that to the real page, exactly as it does live.
            title => Task.FromResult<WikiPageText?>(title == "Spiroc Lord"
                ? new WikiPageText("The Spiroc Lord",
                    "{{Namedmobpage\n| name = The Spiroc Lord\n| known_loot = \n{{:Spiroc Feather}}\n}}")
                : null));

        var result = await svc.LookupAsync("Spiroc Lord");
        Assert.Equal(ItemLookupState.Live, result.State);
        Assert.Equal("The Spiroc Lord", result.Mob!.PageTitle);
    }

    /// <summary>EQ names its gods with an epithet ("Innoruuk, the Prince of Hate") that
    /// the wiki files without ("Innoruuk (God)"). Without the base name in the ladder,
    /// EQBuddy offered to CREATE a page for a boss the wiki already documents.</summary>
    [Fact]
    public async Task AnEpithetFallsBackToTheBaseNameRatherThanProposingADuplicatePage()
    {
        var asked = new List<string>();
        var svc = new EqlWikiMobService(
            Path.Combine(Path.GetTempPath(), $"mobcache-{Guid.NewGuid():N}"),
            title =>
            {
                asked.Add(title);
                return Task.FromResult<WikiPageText?>(title == "Innoruuk"
                    ? new WikiPageText("Innoruuk (God)",
                        "{{Namedmobpage\n| name = Innoruuk\n| known_loot = \n{{:Hate Cloak}}\n}}")
                    : null);
            },
            _ => Task.FromResult<List<string>>([]));

        var result = await svc.LookupAsync("Innoruuk, the Prince of Hate");
        Assert.Contains("Innoruuk", asked);
        Assert.Equal(ItemLookupState.Live, result.State);
        // Landed on the wiki's own title, so the pack links the existing page.
        Assert.Equal("Innoruuk (God)", result.Mob!.PageTitle);
    }
}
