using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The charm state machine, reached directly (2026-08-18).
///
/// Every rule below was already covered through <see cref="SessionStats"/> by
/// <c>SpellTrackingTests</c>, and those tests stay — they are the integration level, and
/// they are what proved this extraction changed nothing. These are the other half: the
/// ones that could not be written before, because the only way into this code was a
/// switch statement inside a 2,500-line class. Six causes of one symptom were fixed in
/// here in three days; being able to ask it a question without building a session is the
/// point of the move.
/// </summary>
public class CharmTrackerTests
{
    private static DateTime At(int mm, int ss) => new(2026, 7, 18, 15, mm, ss);

    private static CharmTracker New(out List<string> confirmed)
    {
        var seen = new List<string>();
        confirmed = seen;
        return new CharmTracker(new SpellCatalog()) { PetConfirmedFirstTime = seen.Add };
    }

    /// <summary>Land a charm and have the pet prove it is ours — the shape every test
    /// below starts from, and the one an item clicky produces (no cast anywhere).</summary>
    private static CharmTracker Charmed(out List<string> confirmed, string pet = "a puma")
    {
        var charm = New(out confirmed);
        charm.OnCharmed(new CharmedEvent(At(0, 0), pet), pendingCast: null);
        charm.OnPetClaim(new PetClaimEvent(At(0, 1), pet), "Douglas");
        return charm;
    }

    // ---- the tracker owns no damage; it announces instead ----

    /// <summary>Its one reach back into the owner: provisional "Pet? (X)" rows become
    /// "Pet (X)" when a tell confirms. It fires ONCE — a second tell must not re-merge
    /// rows that have already moved.</summary>
    [Fact]
    public void ConfirmingAPetAnnouncesItExactlyOnce()
    {
        var charm = New(out var confirmed);
        charm.ConfirmPet("Puma");
        charm.ConfirmPet("Puma");
        Assert.Equal(["Puma"], confirmed);
    }

    /// <summary>Review catch, 2026-08-18: a KNOWN charm whose strong blink arrives after
    /// the spell's own arm window (lag, or a re-cast off a resisted first attempt) must
    /// degrade to the provisional claim the code's own comment promised — not to
    /// nothing. Nothing meant CharmedSince stayed null down this path, and the eventual
    /// fade had no landing time to measure a "held M:SS" from (#135's symptom).</summary>
    [Fact]
    public void AStrongBlinkPastTheArmWindowIsProvisionalNotNothing()
    {
        var charm = New(out _);
        // "Charm" carries a real cast time in the wiki charm catalog, so its arm window
        // is a few seconds; 20 s later is inside CastToBlink but well past the window.
        charm.OnBlink(new PetBlinkEvent(At(0, 20), "a puma"), ("Charm", At(0, 0)));
        Assert.Equal(At(0, 20), charm.CharmedSince);   // provisional — the clock started
        // The tell keeps the original landing time rather than restarting the clock.
        charm.OnPetClaim(new PetClaimEvent(At(0, 25), "a puma"), "Douglas");
        Assert.Equal(At(0, 20), charm.CharmedSince);
    }

    /// <summary>Round-2 review catch: OnBlink renames the pet unconditionally at its
    /// bottom, so a chain-charm's late blink onto a DIFFERENT creature must move the
    /// provisional clock with the name — or the label says wolf while the landing time
    /// says puma. A repeat late blink for the SAME pet still must not restart it.</summary>
    [Fact]
    public void AChainCharmsLateBlinkMovesTheClockWithTheName()
    {
        var charm = New(out _);
        charm.OnBlink(new PetBlinkEvent(At(0, 20), "a puma"), ("Charm", At(0, 0)));
        // A repeat late blink for the same pet: the running clock holds.
        charm.OnBlink(new PetBlinkEvent(At(0, 25), "a puma"), ("Charm", At(0, 6)));
        Assert.Equal(At(0, 20), charm.CharmedSince);
        // A different creature: the clock follows the rename.
        charm.OnBlink(new PetBlinkEvent(At(1, 0), "a wolf"), ("Charm", At(0, 40)));
        Assert.Equal(At(1, 0), charm.CharmedSince);
    }

    /// <summary>Round-3 review catches, both in the message-skew shapes this file
    /// documents: a chain-charm whose predecessor's fade line never reached the parser
    /// must not leave the CONFIRMED hold answering for the old pet (CharmedSince
    /// prefers _hold, so the wolf's eventual break would measure from the puma's
    /// landing); and a refresh blink for the held pet must be a true no-op — not a
    /// demotion of a confirmed identity back to "Pet?".</summary>
    [Fact]
    public void AChainCharmDropsTheOldPetsHoldAndARefreshBlinkDemotesNothing()
    {
        var charm = Charmed(out _);              // confirmed hold on "a puma" at 0:00
        Assert.Equal("Pet (Puma)", charm.SourceLabel);

        // Refresh blink for the same pet, late and strong: identity and clock hold.
        charm.OnBlink(new PetBlinkEvent(At(0, 30), "a puma"), ("Charm", At(0, 15)));
        Assert.Equal("Pet (Puma)", charm.SourceLabel);
        Assert.Equal(At(0, 0), charm.CharmedSince);

        // Chain-charm onto a wolf with no puma fade line seen: the stale hold goes,
        // and the clock is the wolf's landing, not the puma's.
        charm.OnBlink(new PetBlinkEvent(At(1, 10), "a wolf"), ("Charm", At(0, 50)));
        Assert.Equal(At(1, 10), charm.CharmedSince);
    }

    /// <summary>Codex review, 2026-08-18 (both rounds): a chain-charm must end the old
    /// pet's WHOLE tenure — in particular, the old pet's /pet hold order must not
    /// suppress real break detection on the new pet (PetHeldAt compares no names, so a
    /// stale timestamp excuses every future attack forever).</summary>
    [Fact]
    public void AChainCharmEndsTheOldTenureCompletely()
    {
        var charm = Charmed(out _);   // confirmed hold on the puma at 0:00
        charm.OnPetHold(new PetHoldEvent(At(0, 10), "a puma", Holding: true));

        charm.OnBlink(new PetBlinkEvent(At(1, 10), "a wolf"), ("Charm", At(0, 50)));
        Assert.Equal(At(1, 10), charm.CharmedSince);

        // The wolf genuinely turning on us IS a break: the puma's hold order is gone
        // with its tenure and cannot excuse it.
        Assert.True(charm.OnIncomingHit(new DamageTakenEvent(At(1, 40), "a wolf", 50, Melee: true)));
        Assert.Null(charm.CharmedSince);
    }

    /// <summary>Codex round 2: the rename must NOT arm the name-agnostic fade-echo —
    /// doing so swallowed the NEW pet's own real fade for ten seconds and labeled it
    /// with the old pet's hold. The accepted, narrower cost (the old charm's delayed
    /// TARGETLESS fade closing the new clock early) is documented on
    /// EndTenureIfRenaming.</summary>
    [Fact]
    public void ANewPetsRealFadeRightAfterAChainCharmStillBreaks()
    {
        var charm = Charmed(out _);   // confirmed hold on the puma at 0:00
        charm.OnBlink(new PetBlinkEvent(At(1, 10), "a wolf"), ("Charm", At(0, 50)));
        Assert.Equal(At(1, 10), charm.CharmedSince);

        Assert.True(charm.OnCharmFade(new SpellWornOffEvent(At(1, 15), "Charm", "a wolf")));
        Assert.Null(charm.CharmedSince);
    }

    /// <summary>Codex review, 2026-08-18: the tell is the one certain proof, so a tell
    /// naming a NEW pet ends the old tenure even though the landing path conservatively
    /// ignored the swap — CharmedSince must not keep answering with the old pet's
    /// landing time for a pet now labeled with the new name.</summary>
    [Fact]
    public void ATellNamingANewPetEndsTheOldTenure()
    {
        var charm = Charmed(out _);   // confirmed hold on the puma at 0:00
        charm.OnPetClaim(new PetClaimEvent(At(2, 0), "a wolf"), "Douglas");
        Assert.Equal("Pet (Wolf)", charm.SourceLabel);
        // Honest null, not the puma's landing: the wolf's landing was never trusted,
        // so there is no clock to lend it.
        Assert.Null(charm.CharmedSince);
    }

    /// <summary>Codex review, 2026-08-18: a pet confirmed by tell AFTER its no-cast
    /// candidate aged out has no clock at all — the next known-charm refresh blink
    /// supplies one instead of no-op'ing back to a pet that can never say "held".</summary>
    [Fact]
    public void ARefreshBlinkSuppliesTheClockATellOnlyConfirmationLacked()
    {
        var charm = New(out _);
        charm.OnBlink(new PetBlinkEvent(At(0, 0), "a puma"), null);   // item-clicky shape
        // The tell arrives past BlinkToClaim: pet confirmed, but no landing survives.
        charm.OnPetClaim(new PetClaimEvent(At(1, 30), "a puma"), "Douglas");
        Assert.Equal("Pet (Puma)", charm.SourceLabel);
        Assert.Null(charm.CharmedSince);

        charm.OnBlink(new PetBlinkEvent(At(2, 0), "a puma"), ("Charm", At(1, 40)));
        Assert.Equal(At(2, 0), charm.CharmedSince);
        Assert.Equal("Pet (Puma)", charm.SourceLabel);   // still confirmed — no demote
    }

    // ---- a landing line is never proof on its own ----

    [Fact]
    public void ALandingWithNoCastClaimsNothingUntilThePetSpeaks()
    {
        var charm = New(out _);
        charm.OnCharmed(new CharmedEvent(At(0, 0), "a puma"), pendingCast: null);
        Assert.Null(charm.CharmedSince);
        Assert.Null(charm.PetName);

        charm.OnPetClaim(new PetClaimEvent(At(0, 3), "a puma"), "Douglas");
        Assert.Equal(At(0, 0), charm.CharmedSince);   // the LANDING's time, not the tell's
    }

    /// <summary>The guard that keeps the promotion safe: the tell has to name the
    /// creature the landing named, or a stranger's charm beside our own pet's attack
    /// order hands us their pet.</summary>
    [Fact]
    public void ATellAboutSomethingElseNeverPromotesALanding()
    {
        var charm = New(out _);
        charm.OnCharmed(new CharmedEvent(At(0, 0), "a puma"), null);
        charm.OnPetClaim(new PetClaimEvent(At(0, 3), "a wolf"), "Douglas");
        Assert.Null(charm.CharmedSince);
    }

    /// <summary>A pet tell naming somebody ELSE as leader is the one line that disproves
    /// ownership (#177, chrstahl). It drops the claim rather than merely not helping.</summary>
    [Fact]
    public void ATellNamingAnotherLeaderDisprovesOwnership()
    {
        var charm = Charmed(out _);
        Assert.NotNull(charm.CharmedSince);

        charm.OnPetClaim(new PetClaimEvent(At(0, 30), "a puma", Leader: "Someoneelse"), "Douglas");
        Assert.Null(charm.PetName);
        Assert.Null(charm.CharmedSince);
    }

    // ---- an incoming hit asks three questions ----

    [Fact]
    public void AHitInsideTheSettleWindowIsTheMobsOwnInFlightSwing()
    {
        var charm = Charmed(out _);
        Assert.False(charm.OnIncomingHit(new DamageTakenEvent(At(0, 2), "a puma", 100, Melee: true)));
        Assert.NotNull(charm.CharmedSince);
    }

    [Fact]
    public void ADotTickIsNotADecisionToAttack()
    {
        var charm = Charmed(out _);
        var tick = new DamageTakenEvent(At(0, 30), "a puma", 12, Melee: false, OverTime: true);
        Assert.False(charm.OnIncomingHit(tick));
        Assert.NotNull(charm.CharmedSince);
    }

    [Fact]
    public void AHeldPetDidNotStartThisAttackSoTheAttackerIsSomebodyElse()
    {
        var charm = Charmed(out _);
        charm.OnPetHold(new PetHoldEvent(At(0, 5), "a puma", Holding: true));

        Assert.False(charm.OnIncomingHit(new DamageTakenEvent(At(0, 40), "a puma", 100, true)));
        Assert.NotNull(charm.CharmedSince);
    }

    [Fact]
    public void ProofOfTwoCreaturesSharingTheNameMakesAnAttackerAmbiguous()
    {
        var charm = Charmed(out _);
        charm.NoteSameNameProof(At(0, 10));

        Assert.False(charm.OnIncomingHit(new DamageTakenEvent(At(0, 20), "a puma", 100, true)));
        Assert.NotNull(charm.CharmedSince);
    }

    [Fact]
    public void ReleasingTheHoldRestoresTheOrdinaryBreakRule()
    {
        var charm = Charmed(out _);
        charm.OnPetHold(new PetHoldEvent(At(0, 5), "a puma", Holding: true));
        charm.OnPetHold(new PetHoldEvent(At(0, 20), "a puma", Holding: false));

        Assert.True(charm.OnIncomingHit(new DamageTakenEvent(At(0, 40), "a puma", 100, true)));
        Assert.Null(charm.CharmedSince);
        Assert.Null(charm.PetName);
    }

    /// <summary>Every guard above has to stay narrow enough that a pet which really did
    /// round on you stops being credited.</summary>
    [Fact]
    public void APetThatGenuinelyTurnsStillBreaksTheClaim()
    {
        var charm = Charmed(out _);
        Assert.True(charm.OnIncomingHit(new DamageTakenEvent(At(1, 0), "a puma", 100, true)));
        Assert.Null(charm.CharmedSince);
    }

    // ---- the hold ledger ----

    [Fact]
    public void ABreakRecordsHowLongItHeldAndTheFadeLabelCarriesIt()
    {
        var charm = Charmed(out _);
        var before = charm.HoldRevision;

        charm.OnIncomingHit(new DamageTakenEvent(At(4, 32), "a puma", 100, true));

        Assert.True(charm.HoldRevision > before);   // the tracked scan must rebuild
        Assert.Contains("held 4:32",
            charm.FadeLabel(new SpellWornOffEvent(At(4, 32), "Allure", "a puma")));
    }

    /// <summary>The fade prints a few seconds after the attack that actually broke it, so
    /// an exact-time miss falls back within the skew window (#135, v1.76.0).</summary>
    [Fact]
    public void AFadeArrivingLateStillFindsItsHold()
    {
        var charm = Charmed(out _);
        charm.OnIncomingHit(new DamageTakenEvent(At(4, 32), "a puma", 100, true));

        Assert.Contains("held 4:32",
            charm.FadeLabel(new SpellWornOffEvent(At(4, 36), "Allure", "a puma")));
    }

    /// <summary>A fade far past the skew window is a different event entirely, and must
    /// not borrow an older break's duration.</summary>
    [Fact]
    public void AFadeOutsideTheSkewWindowBorrowsNothing()
    {
        var charm = Charmed(out _);
        charm.OnIncomingHit(new DamageTakenEvent(At(4, 32), "a puma", 100, true));

        Assert.DoesNotContain("held",
            charm.FadeLabel(new SpellWornOffEvent(At(5, 30), "Allure", "a puma")));
    }

    [Fact]
    public void ResetForgetsEverything()
    {
        var charm = Charmed(out _);
        charm.Reset();

        Assert.Null(charm.PetName);
        Assert.Null(charm.CharmedSince);
        Assert.Equal("Pet", charm.SourceLabel);
    }

    // ---- the damage-source label ----

    [Fact]
    public void TheLabelSaysWhetherThePetIsProvenOrOnlySuspected()
    {
        var charm = New(out _);
        Assert.Equal("Pet", charm.SourceLabel);

        charm.OnBlink(new PetBlinkEvent(At(0, 0), "a puma"), pendingCast: null);
        Assert.Equal("Pet? (Puma)", charm.SourceLabel);   // blink only — might be a stranger's

        charm.OnPetClaim(new PetClaimEvent(At(0, 3), "a puma"), "Douglas");
        Assert.Equal("Pet (Puma)", charm.SourceLabel);
    }

    /// <summary>A moan with no cast of ours in flight is ambient flavour — the necro
    /// charms' landing line is plausible enough as scenery that it never claims alone.</summary>
    [Fact]
    public void AWeakBlinkWithNoCastIsAmbientFlavour()
    {
        var charm = New(out _);
        var outcome = charm.OnBlink(new PetBlinkEvent(At(0, 0), "a puma", Weak: true), null);

        Assert.True(outcome.Ambient);
        Assert.Null(charm.PetName);
    }

    /// <summary>"Your pet" needs no prior identification — no other creature answers to
    /// it, and it covers a summoned pet that never got an attack order.</summary>
    [Fact]
    public void TheGenericPetNameIsAlwaysOurs()
    {
        var charm = New(out _);
        Assert.True(charm.IsPet("Your pet"));
        Assert.False(charm.IsPet("a puma"));
    }
}
