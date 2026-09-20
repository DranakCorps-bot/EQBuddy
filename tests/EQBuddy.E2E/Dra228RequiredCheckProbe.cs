namespace EQBuddy.E2E;

/// <summary>
/// TEMPORARY — DRA-228 step 3, and this file is DELETED with the pull request that carries it.
///
/// The card's done bar is trap 78's: a required status check nobody has watched REFUSE a merge
/// is an assertion, not a guard. `e2e-windows` was promoted to a required context on `main` in
/// the same change; this is the red that proves the promotion fires.
///
/// **It launches nothing**, for the same reason <c>ScreenLockTests</c> and
/// <c>IconGeometryTests</c> do not: what is under test is the GATE, not the desktop. A test
/// that drove the real app would put the outcome at the mercy of the screen mutex (trap 61)
/// and of every open flake row in the ledger — and a red whose cause is ambiguous proves
/// nothing about a merge refusal. This one fails deterministically, on every runner, in
/// milliseconds.
///
/// **It must fail in `e2e-windows` and ONLY in `e2e-windows`.** `build-and-test` builds this
/// project but runs `tests/EQBuddy.Tests`, so an ASSERTION failure here leaves that job green —
/// which is the exact shape of the gap DRA-228 is about, and the only shape that proves the new
/// context is what refuses the merge. A compile error would redden both jobs and prove nothing.
/// </summary>
public sealed class Dra228RequiredCheckProbe
{
    [Fact]
    public void ThisRedExistsSoTheRequiredCheckRefusalCanBePhotographed()
    {
        Assert.Fail(
            "DRA-228 step 3: deliberate red. `e2e-windows` is now a required status check on "
            + "`main`; this test exists so the refusal can be photographed. The pull request "
            + "carrying it is closed, never merged.");
    }
}
