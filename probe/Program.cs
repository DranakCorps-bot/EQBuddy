using System.Diagnostics;
using EQBuddy.UI.Shared;

// DRA-4216 scratch probe: the real WholeFilePublish.Write/Read (source-linked), instrumented
// per ATTEMPT so a torn read can be classified by what the WRITER was doing at each look.
static class Probe
{
    const string Full = "tick=42 kills=10 shellLiveKillRows=10 party=3";
    const string Next = "tick=43 kills=11 shellLiveKillRows=11 party=3";

    record struct Attempt(double Start, double End, WholeFilePublish.ReadOutcome Outcome,
        int WIdxStart, int InWriteStart, int WIdxEnd, int InWriteEnd);

    static readonly Stopwatch Sw = Stopwatch.StartNew();
    static double Ms => Sw.Elapsed.TotalMilliseconds;
    static volatile int _wIdx = -1, _inWrite;

    static int Main(string[] a)
    {
        int publishes = int.Parse(a[0]), load = int.Parse(a[1]);
        var wprio = Enum.Parse<ThreadPriority>(a[2]);
        long mask = long.Parse(a[3]);
        int runs = int.Parse(a[4]);
        var me = Process.GetCurrentProcess();
        var tmp = Path.GetTempPath();
        Console.WriteLine($"ENV cores={Environment.ProcessorCount} os={Environment.OSVersion} temp={tmp} fs={new DriveInfo(Path.GetPathRoot(tmp)).DriveFormat}");
        if (mask > 0) me.ProcessorAffinity = (IntPtr)mask;
        Thread.Sleep(250);

        int redRuns = 0;
        for (var r = 0; r < runs; r++) if (Run(r, publishes, load, wprio)) redRuns++;
        var legacy = typeof(WholeFilePublish).Assembly.GetType("EQBuddy.UI.Shared.AtomicRename")!
            .GetField("_useLegacyClass", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.GetValue(null);
        Console.WriteLine($"LEGACY-LATCH {legacy}");
        Console.WriteLine($"SUMMARY red {redRuns} of {runs} (publishes={publishes} load={load} writer={wprio} mask={mask})");
        return 0;
    }

    static bool Run(int run, int publishes, int load, ThreadPriority wprio)
    {
        var dir = Directory.CreateTempSubdirectory("dra4216-").FullName;
        var path = Path.Combine(dir, "debug.txt");
        File.WriteAllText(path, Full);
        var wBegin = new double[publishes]; var wEnd = new double[publishes];
        _wIdx = -1; _inWrite = 0;
        var stop = false;
        var loaders = Enumerable.Range(0, load).Select(_ => new Thread(() => { var x = 0L; while (!Volatile.Read(ref stop)) x++; }) { IsBackground = true }).ToList();
        loaders.ForEach(t => t.Start());

        var torn = new List<(double ReadStart, double ReadEnd, List<Attempt> Log)>();
        long reads = 0, rawAbsent = 0, rawDenied = 0, rawRefused = 0;
        var started = new ManualResetEventSlim();
        var done = false;
        var log = new List<Attempt>();
        var absentLooks = new List<(double At, int WIdx, int InWrite, bool Pending, int WIdxEnd)>();
        string Instr(string p, out WholeFilePublish.ReadOutcome o)
        {
            var s = Ms; int wi = _wIdx, iw = _inWrite;
            var t = WholeFilePublish.OpenAndRead(p, out o);
            log.Add(new Attempt(s, Ms, o, wi, iw, _wIdx, _inWrite));
            if (o == WholeFilePublish.ReadOutcome.NameDidNotResolve)
            {
                rawAbsent++;
                var pe = File.Exists(p + WholeFilePublish.PendingSuffix);
                absentLooks.Add((s, wi, iw, pe, _wIdx));
            }
            else if (o == WholeFilePublish.ReadOutcome.OpenDenied) rawDenied++;
            else if (o == WholeFilePublish.ReadOutcome.OpenRefused) rawRefused++;
            return t;
        }
        var reader = new Thread(() =>
        {
            var first = true;
            while (!Volatile.Read(ref done))
            {
                log = new List<Attempt>();
                var rs = Ms;
                var text = WholeFilePublish.Read(path, out _, Instr);
                reads++;
                if (!text.Contains("shellLiveKillRows=")) torn.Add((rs, Ms, log));
                if (first) { first = false; started.Set(); }
            }
        }) { IsBackground = true };
        reader.Start();
        started.Wait();

        int skipped = 0, created = 0;
        var writer = new Thread(() =>
        {
            for (var i = 0; i < publishes; i++)
            {
                _wIdx = i; wBegin[i] = Ms; _inWrite = 1;
                var o = WholeFilePublish.Write(path, i % 2 == 0 ? Full : Next);
                _inWrite = 0; wEnd[i] = Ms;
                if (o == WholeFilePublish.Outcome.SkippedToKeepThePreviousFileWhole) skipped++;
                else if (o == WholeFilePublish.Outcome.Created) created++;
            }
        }) { Priority = wprio };
        var t0 = Ms;
        writer.Start(); writer.Join();
        var dur = Ms - t0;
        Volatile.Write(ref done, true); reader.Join();
        Volatile.Write(ref stop, true); loaders.ForEach(t => t.Join());

        var maxW = Enumerable.Range(0, publishes).Max(i => wEnd[i] - wBegin[i]);
        Console.WriteLine($"run {run}: torn={torn.Count} reads={reads} rawAbsent={rawAbsent} rawDenied={rawDenied} rawRefused={rawRefused} created={created} skipped={skipped} writeMaxMs={maxW:F1} durMs={dur:F0}");
        foreach (var g in absentLooks.GroupBy(x => x.WIdx))
        {
            var i = g.Key; var first = g.Min(x => x.At); var last = g.Max(x => x.At);
            var wd = i >= 0 ? wEnd[i] - wBegin[i] : -1; var we = i >= 0 ? wEnd[i] : -1;
            Console.WriteLine($"  WINDOW w#{i} looks={g.Count()} inWrite={g.Count(x => x.InWrite == 1)} pendingPresent={g.Count(x => x.Pending)} sameWriteAtEnd={g.Count(x => x.WIdxEnd == i)} span={last - first:F1}ms firstLookAfterWriteBegin={(i >= 0 ? first - wBegin[i] : -1):F1}ms writeEndAfterLastLook={we - last:F1}ms writeDur={wd:F1}ms");
        }
        var durs = wEnd.Zip(wBegin, (e, b) => e - b).OrderBy(x => x).ToArray();
        Console.WriteLine($"  writeDur p50={durs[durs.Length / 2]:F2} p99={durs[(int)(durs.Length * 0.99)]:F2} p999={durs[(int)(durs.Length * 0.999)]:F2} max={durs[^1]:F1} over50={durs.Count(d => d > 50)}");
        foreach (var (rs, re, l) in torn)
        {
            Console.WriteLine($"  TORN read {rs:F2}..{re:F2} ms, {l.Count} attempts:");
            foreach (var at in l.Take(3).Concat(l.Count > 6 ? l.Skip(l.Count - 3) : l.Skip(3)))
                Console.WriteLine($"    look {at.Start:F2}..{at.End:F2} {at.Outcome} w#{at.WIdxStart}{(at.InWriteStart == 1 ? "(in)" : "(out)")}->w#{at.WIdxEnd}{(at.InWriteEnd == 1 ? "(in)" : "(out)")}");
            var ids = l.Select(x => x.WIdxStart).Concat(l.Select(x => x.WIdxEnd)).Where(i => i >= 0).Distinct().OrderBy(i => i);
            foreach (var i in ids)
                Console.WriteLine($"    write#{i} {wBegin[i]:F2}..{wEnd[i]:F2} ({wEnd[i] - wBegin[i]:F2} ms)");
        }
        try { Directory.Delete(dir, true); } catch { }
        return torn.Count > 0;
    }
}
