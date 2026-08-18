namespace EQBuddy.Core;

/// <summary>
/// Whose pet that is, and how long the charm has held.
///
/// This came out of <see cref="SessionStats"/> on 2026-08-18, and the reason is worth
/// keeping: **six distinct causes of one symptom were fixed here in three days** (#135,
/// bjstrange — "charm fading doesn't always announce the time held"), every one of them
/// found by replaying a log he attached and none of them findable by reading the code.
/// A subsystem with that defect history, six fields and five constants of its own, does
/// not belong inside a 2,500-line event dispatcher where it can only be reached through a
/// switch statement. <c>MezTracker</c> was already living out here for the same reason,
/// and had already learned one of the six lessons that charm then had to learn again (a
/// DoT tick is not the mob waking up — issue #32, and #135's charm5.txt).
///
/// The rules it holds, in the order they were paid for:
///
///  1. **A landing line is not proof.** "X has been charmed." names no caster and prints
///     in your log when a bystander charms something. Only a cast of ours already KNOWN
///     to be a charm claims on its own; everything else waits for the pet to speak.
///  2. **The pet's "Attacking … Master." tell is proof**, because it is addressed to us
///     and no stranger's pet sends it. It promotes a remembered landing and keeps the
///     landing's own timestamp — the clock started when the charm did, not when the pet
///     got around to saying so.
///  3. **A swing already in flight when the charm lands is not a break** (v1.85.0).
///  4. **A same-named creature is not necessarily yours** (v1.87.0 and charm6.txt): if
///     one has attacked another of its own name, or yours is on HOLD and therefore did
///     not start this attack, an ambiguous attacker cannot destroy the claim.
///  5. **A DoT tick is not a decision to attack** (charm5.txt) — it is the tail of a
///     spell cast before the charm existed.
///  6. **An ITEM prints no cast line at all** (charm7.txt, Puppet Strings), so every
///     per-spell mechanism above has nothing to key on and the landing must still be
///     remembered.
///
/// It owns no damage and no combat spans: when a claim is confirmed it says so through
/// <see cref="PetConfirmedFirstTime"/> and the owner merges its own provisional rows.
/// </summary>
internal sealed class CharmTracker(SpellCatalog spells)
{
    // ---- windows, all paid for by a log ----

    /// <summary>How long after a cast starts a blink can still belong to it. Charm casts
    /// run a few seconds; observed gap in real logs is ~4s. This is the FALLBACK for
    /// spells whose cast time the catalog doesn't know — known charms use the tighter
    /// per-spell arm window below.</summary>
    internal static readonly TimeSpan CastToBlink = TimeSpan.FromSeconds(30);

    /// <summary>Slack past a spell's cast time in the arm window: log timestamps round
    /// to the second and the server adds a beat, but a landing seconds after our cast
    /// COMPLETED is somebody else's charm.</summary>
    internal const double CharmArmSlackSeconds = 1.5;

    /// <summary>How long after a blink a "Master" tell still confirms the same charm.
    /// Observed gap in real logs is ~5s; pets can be slow to announce.</summary>
    private static readonly TimeSpan BlinkToClaim = TimeSpan.FromSeconds(60);

    /// <summary>The game prints a charm's fade line up to several seconds AFTER the event
    /// that actually broke it. One window covers both faces of that skew: FadeLabel looks
    /// this far back for a recorded hold (#135, v1.76.0: attack-then-fade), and the
    /// wear-off ingest treats a fade this close to an already-recorded break as that
    /// break's delayed echo rather than a new break (#135: re-charm cascade).</summary>
    private const int CharmFadeSkewSeconds = 10;

    /// <summary>A swing already in flight when the charm lands still hits YOU a beat
    /// later — the mob was mid-round and the game resolves that round before the charm
    /// takes hold (#135: "3 charms announced, the 4th didn't", different mobs, random —
    /// the pattern was in the mob's swing timer). A melee round is about 2 seconds.</summary>
    private const int CharmSettleSeconds = 3;

    /// <summary>How long proof that two creatures share the pet's name stays good.
    /// Deliberately narrower than "the pet is busy with someone else": a pet fighting a
    /// GHOUL and then turning on you is a real break with no ambiguity about identity,
    /// and suppressing that would keep crediting a creature that is now hitting you.</summary>
    private const int SameNameProofSeconds = 30;

    /// <summary>The one name every pet answers to. No creature but your own pet is ever
    /// called this, so it needs no prior identification — it works for a summoned pet
    /// that has never been given an attack order, the one case the "Attacking … Master."
    /// line cannot cover.</summary>
    private const string GenericPetName = "Your pet";

    // ---- state ----

    private string? _petName;        // normalized (article stripped, capitalized)
    private bool _petConfirmed;      // false = blink-only (charm suspected, no "Master" tell yet)

    /// <summary>A cast that preceded a blink or charmed line, held until a "Master" tell
    /// proves it was a charm. Pet carries the creature the line named: the tell must name
    /// the SAME creature to teach, so a bystander's charm coinciding with our own
    /// unrelated cast (Hugzee's Heroic Leap) can never mislabel that cast as a charm
    /// (issue #29).
    ///
    /// Spell is NULL when the landing had no cast of ours behind it at all — an ITEM
    /// clicky (#135, charm7.txt: Puppet Strings). Nothing to learn, and nothing claimed by
    /// the landing; the record exists so the caster-only "Master" tell can start the clock
    /// when the charm LANDED rather than when the pet happened to speak.</summary>
    private (string? Spell, DateTime Time, string Pet)? _candidate;

    /// <summary>#130: how long the current charm has HELD. Set only by charm-path claims —
    /// a summoned pet never "breaks".</summary>
    private (string Pet, DateTime LandedAt)? _hold;

    /// <summary>Provisional charm claims (late landings, unknown spells) start the clock
    /// too; the Master tell that confirms them keeps the original landing time.</summary>
    private (string Pet, DateTime LandedAt)? _provisional;

    /// <summary>Break time → held seconds, so the fade alert's label can say "held 4:32"
    /// (the journal scan rebuilds labels repeatedly; this is its lookaside).</summary>
    private readonly Dictionary<DateTime, double> _holdByBreak = new();

    private DateTime? _sameNameProofAt;

    /// <summary>Since when the pet has been on HOLD, from its own reply ("Now holding,
    /// Master. I will not start attacks until ordered."). A held pet does not initiate
    /// attacks, so a same-named creature swinging at you while yours is held is a
    /// DIFFERENT creature — the second, independent proof of a duplicate, and the one
    /// #135's charm6.txt needed: Bzzazzt charmed at 01:25:21, told to hold at 01:25:28,
    /// and at 01:25:36 "Bzzazzt" lands a five-hit round on the player. The same-name guard
    /// could not help, because its only proof is a creature attacking something of its own
    /// name and the two Bzzazzts never fought each other.</summary>
    private DateTime? _petHeldSince;

    // ---- what the owner reads ----

    public string? PetName => _petName;

    /// <summary>Bumped whenever a hold is recorded — a new hold can retroactively relabel
    /// an already-scanned fade, so the incremental tracked scan must know to rebuild.</summary>
    public int HoldRevision { get; private set; }

    public DateTime? CharmedSince => _hold?.LandedAt ?? _provisional?.LandedAt;

    /// <summary>The damage-source row this pet's damage belongs under. "Pet?" is the
    /// provisional state: a blink with no Master tell yet, which might be a stranger's.</summary>
    public string SourceLabel => _petName is null ? "Pet"
        : _petConfirmed ? $"Pet ({_petName})" : $"Pet? ({_petName})";

    /// <summary>Raised the FIRST time a pet is confirmed, naming it — the owner merges its
    /// own provisional "Pet? (X)" damage rows into "Pet (X)". The tracker holds no damage
    /// itself; that stays where the aggregates are.</summary>
    public Action<string>? PetConfirmedFirstTime { get; set; }

    public bool IsPet(string name)
    {
        var normalized = LogParser.Normalize(name);
        if (string.Equals(normalized, GenericPetName, StringComparison.OrdinalIgnoreCase))
            return true;
        return _petName is not null &&
            string.Equals(normalized, _petName, StringComparison.OrdinalIgnoreCase);
    }

    public void Reset()
    {
        _petName = null;
        _petConfirmed = false;
        _hold = null;
        _provisional = null;
        _candidate = null;
        _sameNameProofAt = null;
        _petHeldSince = null;
        _holdByBreak.Clear();
    }

    /// <summary>Per-spell charm arm window (approved 2026-08-13): a landing line is ours
    /// only within the spell's own cast time + slack of the cast starting. The old fixed
    /// 30s meant a bystander's charm landing 20s after our failed Beguile (3.5s cast)
    /// could steal the claim; now the window fits the spell.
    /// Two honesty guards from the review: log stamps are WHOLE seconds (a real 3.02s gap
    /// can log as 4), so the fractional cast time rounds UP before the slack is added; and
    /// a zero/absent cast time means "instant or unknown" — either way the generic window
    /// applies, never a 1.5s trap.</summary>
    private TimeSpan ArmWindow(string spell) =>
        spells.CastTimeSeconds(spell) is { } ct && ct > 0
            ? TimeSpan.FromSeconds(Math.Ceiling(ct) + CharmArmSlackSeconds)
            : CastToBlink;

    // ---- ingest ----

    /// <summary>The direct charm-success line. Returns true when it consumed the pending
    /// cast, so the owner can clear it.
    ///
    /// This line names NO caster and is bystander-visible (12 of 43 in eqlog_Hugzee had no
    /// own cast near them: other players charming nearby; David called this before it
    /// shipped wrong). Worse, "unknown cast in flight" is no proof of ownership either:
    /// Hugzee spams Heroic Leap (unknown to the catalog), and one leap coinciding with a
    /// bystander's charm would both steal the pet AND teach the catalog that Heroic Leap
    /// is a charm. So it claims ONLY behind a cast already KNOWN to be a charm — where it
    /// beats the "Attacking … Master." tell by up to 9 s of otherwise-unclaimed damage.
    /// Unknown charm spells still get learned via that tell, which is caster-only and
    /// unspoofable.</summary>
    public bool OnCharmed(CharmedEvent ch, (string Spell, DateTime Time)? pendingCast)
    {
        var name = LogParser.Normalize(ch.Name);
        if (pendingCast is { } cast && ch.Time - cast.Time <= CastToBlink)
        {
            var category = spells.Classify(cast.Spell);
            // Known charm: claim only inside ITS arm window (cast time + slack) — past
            // that our cast completed without this landing, so the line is probably a
            // bystander's charm.
            if (category == SpellCategory.Charm && ch.Time - cast.Time <= ArmWindow(cast.Spell))
            {
                EndTenureIfRenaming(name, ch.Time);   // a chain-charm ends the old tenure whole
                ConfirmPet(name);
                _hold = (name, ch.Time);   // #130
                return true;
            }
            // Outside the window but our charm cast IS still recent: degrade to the
            // provisional "Pet?" state instead of nothing (lag/rounding cases) — the
            // "Master" tell resolves it and merges the provisional damage, same as the
            // blink path.
            if (category == SpellCategory.Charm && _petName is null)
            {
                _petName = name;
                _petConfirmed = false;
                _provisional = (name, ch.Time);   // #130: the clock starts at the landing
            }
            // Unknown cast + no pet of our own: record the cast as a charm candidate — NO
            // claim, no damage credit (a bystander's charm coinciding with Heroic Leap
            // must not steal anything) — so the "Master" tell that follows the first
            // attack order can teach the spell. Before this, the learning hook only
            // existed on the blink path: a client whose charms log "has been charmed."
            // with a spell outside the catalog never learned it, and every charm waited
            // for the attack button (issue #29).
            else if (category == SpellCategory.Unknown && _petName is null)
                _candidate = (cast.Spell, ch.Time, name);
            return false;
        }
        // NO cast of ours in flight is what an ITEM looks like: Puppet Strings clicks
        // Allure and prints no "You begin casting" line, so every per-spell mechanism
        // above has nothing to key on and the landing used to be dropped on the floor —
        // no claim, and no LANDING TIME, so the wear-off 19 s later had nothing to
        // measure (#135, charm7.txt). Claims nothing here either: the line names no caster
        // and prints for a bystander's charm too. The "Master" tell decides, on the same
        // window and promotion as the unknown-cast candidate above — the only difference
        // being that there is no spell to learn.
        if (_petName is null) _candidate = (null, ch.Time, name);
        return false;
    }

    /// <summary>"X's eyes glaze over." lands BOTH bard charm songs and bard mez songs
    /// (eqlwiki: Solon's line vs Crission's/Sionachie's — identical message). The parser
    /// can't tell them apart; the pending SONG can. Returns true when it consumed the
    /// pending cast.</summary>
    public bool OnGlaze(MezzedEvent glazed, (string Spell, DateTime Time)? pendingCast)
    {
        if (pendingCast is not { } cast || glazed.Time - cast.Time > CastToBlink
            || spells.Classify(cast.Spell) != SpellCategory.Charm)
            return false;

        // Inside the song's own arm window the claim is certain; a glaze on a LATER pulse
        // (bard songs pulse ~6s and only the first "begin to sing" logs) degrades to
        // provisional — the attack-order tell confirms and merges, never loses.
        if (glazed.Time - cast.Time <= ArmWindow(cast.Spell))
        {
            EndTenureIfRenaming(glazed.Target, glazed.Time);
            ConfirmPet(glazed.Target);
            _hold = (glazed.Target, glazed.Time);   // #130
            return true;
        }
        if (_petName is null)
        {
            _petName = glazed.Target;
            _petConfirmed = false;
            _provisional = (glazed.Target, glazed.Time);
        }
        return false;
    }

    /// <summary>What the caller must do after a blink line.</summary>
    /// <param name="ConsumedCast">Clear the pending cast.</param>
    /// <param name="Ambient">The line was flavour, not a charm — change nothing else.</param>
    public readonly record struct BlinkOutcome(bool ConsumedCast, bool Ambient);

    /// <summary>Charm just landed. If one of our charm casts is still in flight the claim
    /// is certain, so skip the provisional "Pet?" state entirely.</summary>
    public BlinkOutcome OnBlink(PetBlinkEvent pb, (string Spell, DateTime Time)? pendingCast)
    {
        var blinked = LogParser.Normalize(pb.Name);
        if (pendingCast is { } cast && pb.Time - cast.Time <= CastToBlink)
        {
            var category = spells.Classify(cast.Spell);
            // Certain only inside the spell's own arm window; a blink seconds after our
            // cast completed gets the provisional treatment below instead.
            if (category == SpellCategory.Charm && pb.Time - cast.Time <= ArmWindow(cast.Spell))
            {
                EndTenureIfRenaming(blinked, pb.Time);
                ConfirmPet(blinked);
                _hold = (blinked, pb.Time);   // #130
                return new BlinkOutcome(ConsumedCast: true, Ambient: true);
            }
            // A known charm whose arm window already closed: a weak line (moan) is ambient
            // flavor again — our cast completed without it. Strong blinks fall through to
            // the provisional state.
            if (category == SpellCategory.Charm && pb.Weak)
                return new BlinkOutcome(false, Ambient: true);
            // Outside the window but our charm cast IS still recent: the strong blink
            // degrades to the provisional "Pet?" state — the landing path's own contract,
            // and what the comment above always promised. The assignment was missing
            // (review catch, 2026-08-18): CharmedSince stayed null down this path, so the
            // eventual fade had no landing time to measure a hold from. This method
            // RENAMES the pet unconditionally below, so the clock has to move with the
            // name — see EndTenureIfRenaming for the whole of what that means.
            if (category == SpellCategory.Charm)
            {
                if (_petName is not null
                    && string.Equals(_petName, blinked, StringComparison.OrdinalIgnoreCase))
                {
                    // A refresh blink for the pet we ALREADY track: with a clock running
                    // it is a true no-op — the clock holds and a confirmed identity is
                    // not re-doubted (the tail would demote it to "Pet?" and misfile its
                    // damage until the next tell re-proved what was never in question).
                    // WITHOUT a clock (a tell-only confirmation whose candidate aged
                    // out — Codex, 2026-08-18) it supplies the missing landing time.
                    if (CharmedSince is null) _provisional = (blinked, pb.Time);
                    return new BlinkOutcome(false, Ambient: true);
                }
                // A chain-charm onto a DIFFERENT creature: the old tenure ends first —
                // whole, echo-armed — then the new clock starts at this landing (#130).
                EndTenureIfRenaming(blinked, pb.Time);
                _provisional = (blinked, pb.Time);
                _petName = blinked;
                _petConfirmed = false;
                return new BlinkOutcome(false, Ambient: false);
            }
            // Unrecognised spell: hold onto it so a following "Master" tell can teach us
            // it was a charm.
            if (category == SpellCategory.Unknown)
                _candidate = (cast.Spell, pb.Time, blinked);
        }
        else if (pb.Weak)
        {
            // A moan with no cast of ours in flight is ambient flavor, not a charm —
            // never even provisional.
            return new BlinkOutcome(false, Ambient: true);
        }
        else
        {
            // A strong blink with no cast behind it: the item-clicky case again (#135).
            // Remembered, not claimed — the tell decides. Tenure bookkeeping FIRST: a
            // candidate still naming the OLD pet would block this landing's candidacy
            // (Codex, round 2).
            EndTenureIfRenaming(blinked, pb.Time);
            _candidate ??= (null, pb.Time, blinked);
        }
        // These branches rename too (upstream's tail, kept): a DIFFERENT name still
        // ends the old tenure first, or the old hold answers for the new name.
        EndTenureIfRenaming(blinked, pb.Time);
        _petName = blinked;
        _petConfirmed = false;
        return new BlinkOutcome(false, Ambient: false);
    }

    /// <summary>A pet tell. "My leader is X." rides the broadcast say channel, so a nearby
    /// player's pet answering THEIR /pet leader lands in our log too — the name is what
    /// separates them, and it has to be ours. An unknown character name can't check it,
    /// and an unverifiable claim is not one we take: the pet slot is single, so a wrong one
    /// swaps our pet's damage out for a stranger's. (The attack order names nobody and
    /// needs none — it is a tell addressed to us, which no bystander's pet ever sends.)
    /// Returns true when the tell proves a fight is on.</summary>
    public bool OnPetClaim(PetClaimEvent pc, string? characterName)
    {
        if (pc.Leader is { } leader
            && !string.Equals(leader, characterName, StringComparison.OrdinalIgnoreCase))
        {
            // …and when it names somebody ELSE, it is not merely unhelpful: it is the one
            // line in the log that DISPROVES ownership, which is chrstahl's own suggestion
            // in #177 and settles the cases inference cannot. Inference has to guess from
            // timing alone — two charmers in one camp and a landing line that names no
            // caster — and a wrong guess quietly credits a stranger's pet to us for as long
            // as it lives. Drop the claim.
            //
            // Only against a character name we actually know: with none, the leader may
            // well BE us and the "disproof" would be us releasing our own pet. And only
            // for the creature the line names, since a statement about a different pet
            // says nothing about ours.
            var disproved = LogParser.Normalize(pc.PetName);
            if (characterName is { Length: > 0 } && _petName is not null
                && string.Equals(_petName, disproved, StringComparison.OrdinalIgnoreCase))
            {
                // Deliberately NOT a charm break: nothing of ours ended, so recording a
                // hold would print a duration for a pet we never had. Damage already
                // credited stays as it was booked — rewinding aggregates would leave the
                // session totals and the per-source rows disagreeing, and the provisional
                // rows say "Pet?" precisely because they might be wrong.
                _petName = null;
                _petConfirmed = false;
                // The whole tenure ends with the disproof — a surviving /pet hold or
                // same-name proof from the disproved pet would suppress real break
                // detection on whatever we charm next (Codex, round 2).
                _petHeldSince = null;
                _sameNameProofAt = null;
                if (_hold is { } hold && string.Equals(
                        hold.Pet, disproved, StringComparison.OrdinalIgnoreCase))
                    _hold = null;
                if (_provisional is { } prv && string.Equals(
                        prv.Pet, disproved, StringComparison.OrdinalIgnoreCase))
                    _provisional = null;
            }
            // The held cast must go too, or the next landing line re-claims the creature
            // we were just told is not ours.
            if (_candidate is { } foreign
                && string.Equals(foreign.Pet, disproved, StringComparison.OrdinalIgnoreCase))
                _candidate = null;
            return false;
        }

        // A blink/charmed line that followed an unrecognised cast, now proven ours: that
        // cast was a charm spell, so remember it — permanently, via the attached store.
        // The claim must name the same creature the line did; a claim about a different
        // pet proves nothing about that cast.
        var claimed = LogParser.Normalize(pc.PetName);
        // The tell names OUR pet with certainty. If we tracked a DIFFERENT creature,
        // the landing path conservatively ignored the swap (a landing line is
        // bystander-visible), so this is where the old tenure ends (Codex, 2026-08-18)
        // — before the promotions below, which must not hand the new pet the old
        // pet's landing time through their `_hold ??=`.
        EndTenureIfRenaming(claimed, pc.Time);
        if (_candidate is { } cand && pc.Time - cand.Time <= BlinkToClaim
            && string.Equals(cand.Pet, claimed, StringComparison.OrdinalIgnoreCase))
        {
            // Only when a cast of ours produced the landing. An item clicky has no spell
            // name to attach the lesson to.
            if (cand.Spell is { Length: > 0 } learned)
                spells.Learn(learned, SpellCategory.Charm);
            _hold ??= (claimed, cand.Time);   // #130: the blink was the landing
            _candidate = null;
        }
        // A provisional charm claim the tell just confirmed keeps its original landing
        // time — the clock started when the charm did.
        if (_provisional is { } prov && pc.Time - prov.LandedAt <= BlinkToClaim
            && string.Equals(prov.Pet, claimed, StringComparison.OrdinalIgnoreCase))
        {
            _hold ??= (claimed, prov.LandedAt);
            _provisional = null;
        }
        ConfirmPet(claimed);
        // Only the attack order proves a fight; the leader response would otherwise open
        // a combat span while camped.
        return pc.Fighting;
    }

    /// <summary>Only for the creature the line names, and only when that is our pet: a
    /// nearby charmer's pet answering THEIR hold order rides the same say channel, and
    /// taking it would excuse a genuine break by ours.</summary>
    public void OnPetHold(PetHoldEvent ph)
    {
        if (_petName is not null
            && string.Equals(_petName, LogParser.Normalize(ph.PetName),
                StringComparison.OrdinalIgnoreCase))
            _petHeldSince = ph.Holding ? ph.Time : null;
    }

    /// <summary>A charm blocked BY ITSELF is a re-cast bouncing off the pet already held —
    /// evidence about the NEW cast, not the armed candidate from the original landing.
    /// Disarming there would cost a chain-charmer the claim of a genuinely held pet.</summary>
    public void OnSpellBlocked(string blockedKey, string blockedBy)
    {
        if (_candidate is { Spell: { } spell }
            && string.Equals(SpellCatalog.BaseName(spell), blockedKey, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(SpellCatalog.BaseName(blockedBy), blockedKey, StringComparison.OrdinalIgnoreCase))
            _candidate = null;
    }

    /// <summary>Attacker AND target share the pet's name: there are two of them.</summary>
    public void NoteSameNameProof(DateTime at) => _sameNameProofAt = at;

    /// <summary>A line is about to rename the pet to a DIFFERENT creature — a landing,
    /// a chain-charm's blink, a candidate swap, a tell naming a new pet. The old pet's
    /// tenure state ends FIRST, as one group: hold, provisional, same-name proof, the
    /// /pet hold order, and a candidate still naming the old pet (which could never be
    /// promoted honestly and would block the new landing's own candidacy). Two
    /// independent reviews converged here from opposite directions (2026-08-18): five
    /// hand-scoped partial cleanups at rename sites each missed fields that belong
    /// together — a stale /pet hold from the old pet even suppressed real break
    /// detection on the new one forever, since PetHeldAt compares no names.
    ///
    /// Deliberately NOT RecordBreak: a rename-created ledger entry armed the fade-echo
    /// window, which is name-agnostic — it swallowed the NEW pet's own real fade for
    /// ten seconds and labeled it with the old pet's hold (Codex, round 2). The cost of
    /// this direction is narrower and documented: the OLD charm's delayed TARGETLESS
    /// fade (Befriend Animal's shape, inside the skew window, mid-chain-charm) can
    /// close the new clock early. That wrong self-corrects on the next landing; the
    /// other wrong silently kept claiming a pet that had left.</summary>
    private void EndTenureIfRenaming(string newName, DateTime at)
    {
        if (_petName is null
            || string.Equals(_petName, newName, StringComparison.OrdinalIgnoreCase))
            return;
        _hold = null;
        _provisional = null;
        _sameNameProofAt = null;
        _petHeldSince = null;
        if (_candidate is { } stale
            && string.Equals(stale.Pet, _petName, StringComparison.OrdinalIgnoreCase))
            _candidate = null;
    }

    /// <summary>Is this wear-off line the end of OUR charm? Two shapes: one naming the
    /// pet, and Befriend Animal's, which names no target at all ("Your charm spell has
    /// worn off." — unique among the animal charms; only one charm can be active, so a
    /// targetless charm fade is ours).</summary>
    public bool IsOurCharmFade(SpellWornOffEvent wo) =>
        !wo.Pet && _petName is not null
        && spells.Classify(wo.Spell) == SpellCategory.Charm
        && (wo.Target.Length == 0 || IsPet(wo.Target));

    /// <summary>The charm broke on our pet. Returns false when the line is a stale echo
    /// and nothing should change: a fade this soon after a recorded break is that break's
    /// delayed echo, so the claim held now belongs to a re-charm of the same creature and
    /// must survive the stale line (#135: re-charm echo cascade).</summary>
    public bool OnCharmFade(SpellWornOffEvent wo)
    {
        if (IsBreakEcho(wo.Time)) return false;
        RecordBreak(wo.Time);
        _petName = null;
        _petConfirmed = false;
        return true;
    }

    /// <summary>A "pet" attacking us means the charm broke — stop crediting it. Returns
    /// true when it really did break.
    ///
    /// Unless the charm only just landed, in which case this is the mob's in-flight swing
    /// finishing its round. And never a DoT TICK: a spell the creature cast before you
    /// charmed it keeps ticking afterwards, and a tick is not a decision to attack. The mez
    /// tracker already knew this (issue #32); charm never got the same guard, so
    /// bjstrange's Choking ticked six seconds into a 5:31 charm and threw the clock away —
    /// the real fade minutes later then had no landing to measure (#135, charm5.txt).</summary>
    public bool OnIncomingHit(DamageTakenEvent dt)
    {
        if (!IsPet(dt.Attacker) || dt.OverTime
            || JustLanded(dt.Time) || SameNameDuplicateKnown(dt.Time))
            return false;
        RecordBreak(dt.Time);
        _petName = null;
        return true;
    }

    /// <summary>A "Master" tell proves the pet is ours — the owner upgrades any
    /// provisional damage when this says so.</summary>
    public void ConfirmPet(string name)
    {
        _petName = name;
        if (_petConfirmed) return;
        _petConfirmed = true;
        PetConfirmedFirstTime?.Invoke(name);
    }

    // ---- the three questions an incoming hit asks ----

    /// <summary>Did the charm land so recently that a hit on us is the mob's own in-flight
    /// swing rather than a break? (See <see cref="CharmSettleSeconds"/>.)</summary>
    private bool JustLanded(DateTime at) =>
        CharmedSince is { } landed
        && at >= landed && (at - landed).TotalSeconds <= CharmSettleSeconds;

    /// <summary>Do we know a second creature shares the pet's name right now? Then a
    /// same-named attacker hitting us proves nothing, and the claim survives until the
    /// wear-off line settles it (see <see cref="SameNameProofSeconds"/>).</summary>
    private bool SameNameDuplicateKnown(DateTime at) =>
        (_sameNameProofAt is { } seen
            && at >= seen && (at - seen).TotalSeconds <= SameNameProofSeconds)
        || PetHeldAt(at);

    /// <summary>Was the pet under a HOLD order at this moment? Then it did not start this
    /// attack, so a same-named attacker is someone else (#135, charm6.txt). Unlike the
    /// attacked-its-own-name proof this does not expire on a timer — hold is a state the
    /// pet stays in until released, and the release line says so.</summary>
    private bool PetHeldAt(DateTime at) =>
        _petHeldSince is { } since && at >= since;

    // ---- the hold ledger ----

    /// <summary>#130: close the charm-hold clock at a break and remember how long it held,
    /// keyed by the break time so the fade alert's label can carry it ("Charm (a gnoll) —
    /// held 4:32").</summary>
    private void RecordBreak(DateTime at)
    {
        var landed = CharmedSince;
        _hold = null;
        _provisional = null;
        _sameNameProofAt = null;
        _petHeldSince = null;   // the hold belonged to the pet we just stopped claiming
        if (landed is not { } l) return;
        var held = (at - l).TotalSeconds;
        if (held <= 0) return;
        _holdByBreak[at] = held;
        // A new hold can retroactively relabel an already-scanned fade (FadeLabel
        // tolerates ordering skew), so the incremental tracked scan must rebuild.
        HoldRevision++;
        if (_holdByBreak.Count > 64)
            foreach (var old in _holdByBreak.Keys.OrderBy(k => k)
                         .Take(_holdByBreak.Count - 64).ToList())
                _holdByBreak.Remove(old);
    }

    /// <summary>#135: a charm fade line landing within the skew window of an already-
    /// recorded break is that break's delayed echo, not a new break. Acting on it would
    /// measure a bogus tiny hold from any re-charm in between AND null the re-charm's live
    /// claim — the later real break then has no landing to measure from, which is exactly
    /// the missing "held M:SS".</summary>
    private bool IsBreakEcho(DateTime at) => _holdByBreak.Keys
        .Any(k => k <= at && (at - k).TotalSeconds <= CharmFadeSkewSeconds);

    /// <summary>Journal label for a fade row/alert — a charm break gets its hold duration
    /// appended (#130). The lookup tolerates ordering skew (#135, bjstrange: "doesn't
    /// always trigger the time"): when the pet turning on you is what breaks the charm, the
    /// hold gets recorded at the ATTACK's timestamp and the fade line prints a few seconds
    /// later — so an exact-time miss falls back to the most recent hold recorded within the
    /// skew window.</summary>
    public string FadeLabel(SpellWornOffEvent wo)
    {
        var label = wo.Target.Length > 0 ? $"{wo.Spell} ({wo.Target})" : wo.Spell;
        if (!_holdByBreak.TryGetValue(wo.Time, out var held))
        {
            var near = _holdByBreak.Keys
                .Where(k => k <= wo.Time && (wo.Time - k).TotalSeconds <= CharmFadeSkewSeconds)
                .OrderByDescending(k => k)
                .Cast<DateTime?>()
                .FirstOrDefault();
            if (near is not { } n) return label;
            held = _holdByBreak[n];
        }
        var t = TimeSpan.FromSeconds(held);
        var text = t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
            : $"{t.Minutes}:{t.Seconds:00}";
        return label + $" — held {text}";
    }
}
