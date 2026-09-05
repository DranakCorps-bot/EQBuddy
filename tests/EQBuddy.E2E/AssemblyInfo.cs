// THE THING THIS SUITE SHARES IS THE DESKTOP, AND ONLY THREE OF ITS FIVE CLASSES SAID SO.
//
// `EndToEndTests`, `HudBarTests` and `WorldOpenersTests` carry `[Collection("e2e")]`, and
// `HudBarTests` explains why in as many words: "every test here launches a real
// always-on-top widget". `ShellHostTests` launches one too and carried no attribute — and
// xUnit gives an unattributed class a collection OF ITS OWN, then runs collections in
// PARALLEL. So the README's "tests run sequentially (one app at a time)" has been false
// since that file was added: two real widgets and two real shells could be up together,
// each one always-on-top, each one found by title.
//
// **That is trap 57 exactly, and the trap's own tombstone predicted this file.** The
// deleted Avalonia suite had nineteen of twenty-one classes attributed; the two that were
// not produced a flake that named a different innocent test each time, and the fix was
// this attribute rather than a twentieth `[Collection]`. CLAUDE.md keeps the entry with a
// standing instruction — *"E-3 adds test projects. When you write one that shares
// something, write the assembly-level attribute in the same commit, not after the third
// flake."* The constraint is a fact about the SCREEN, not a property of a hand-kept list
// of class names, and a list stops covering the set the day the set grows (trap 30).
//
// It also makes the screen lock (`ScreenLock`) honest: that lock is taken once per test
// host and held for the run, so it says nothing about two tests inside one host. Serialized
// here, there is only ever one app on screen; unserialized, the lock would have been a
// guard against the OTHER seat while this seat collided with itself.
//
// Cost: none worth counting. The suite is already one app at a time by intent, so
// disabling parallelism removes an overlap nobody wanted rather than any concurrency the
// run was using. `IconGeometryTests` launches nothing and simply runs in the same line.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
