using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Companion;

/// <summary>
/// **The Helper on the phone** (DRA-71 D9; Fable's plan P15, *"phone parity stays by shared
/// module, its own slice"*).
///
/// <para><b>Every string in this record is a SENTENCE somebody else already decided.</b>
/// Nothing here is computed and nothing in <c>index.html</c> spells any of it: the words come
/// from <see cref="HelperPresentation"/>, <see cref="LevelReadout"/> and
/// <see cref="UnlockPickReadout"/> — the same producers the desktop room draws — and the
/// answers come from <c>Recommendations.Rank</c> over the same <c>HelperInputs</c>. That is
/// the whole of what "parity by shared module" buys: a feature list kept level by hand drifts
/// (#210), and a page-side literal can sit on an open phone for weeks after the PC has moved
/// on (trap 32).</para>
///
/// <para><b>The room is READ-ONLY here, and the reason is trap 35 rather than effort.</b> The
/// desktop room's controls are five <c>EqMultiPicker</c>s, a segmented strip and a toggle —
/// affordances that WRITE to the profile the PC is playing from. Porting them as controls
/// would mean a tap on a phone reaching across the LAN to change how the PC ranks while
/// somebody is playing at it, and one of those doors (the Watch skill-up preset) has a side
/// effect behind it. So the affordance ports as INTENT: the phone SHOWS what is picked, in
/// the picker face's own words, and says where the picking happens. Same fact, same source,
/// an affordance the device can actually keep.</para>
///
/// <para><b>And nothing rides a hover.</b> The plan says it in as many words — *"why-lines
/// ride the row — no hover"* — because a phone has no pointer. Where the desktop puts a
/// sentence in a tooltip (a door's tip, a goal's tip), it is a LINE here.</para>
/// </summary>
/// <param name="Question">The room's own question, in the Founder's words.</param>
/// <param name="PicksLead">What the block of picks is, and where it is changed.</param>
/// <param name="DoorsLead">The lead over any row's doors. ONE field for the whole section
/// rather than one per row: it is the same sentence every time, and a page that had to
/// remember the last one it saw would draw an unled door list until a row that carried it
/// arrived.</param>
/// <param name="Picks">What this character is working toward, one row per decision the
/// desktop room offers a picker for — and only the ones the desktop would DRAW, which is the
/// same condition: while its goal is picked, or while nothing is.</param>
/// <param name="AnswersHeading">The heading over the answers.</param>
/// <param name="SourceNote">The values line in one sentence. It is on the phone for the same
/// reason it is on the desktop: this is the surface that ranks, so this is the surface that
/// says what it ranked from.</param>
/// <param name="LevelNote">Which level the engine used, and where the number came from.</param>
/// <param name="Answers">The ranked answers, capped.</param>
/// <param name="MoneyNote">The vendor-price caveat, when a price was quoted. Empty
/// otherwise — drawn from what was BUILT, never from which goal is ticked.</param>
/// <param name="GearBaseNote">The base-vs-base caveat, when a gear row was built (DRA-149 D1).
/// Empty otherwise, and drawn from what was BUILT rather than from which goal is ticked — the
/// money note's rule beside it, for the money note's reason.</param>
/// <param name="Cap">What the answer cap held back, when it held anything (trap 50).</param>
/// <param name="GearWithheld">What the gear sweep's own per-anchor cap held back. A separate
/// field because it is spent before any row exists and so cannot ride one.</param>
/// <param name="GearBandRefused">Which zones the Farm Gear band gate refused, with their bands
/// and this character's level (DRA-84 D2). Its own field for the same reason as
/// <paramref name="GearWithheld"/>, one rule out: the row is what did not get built.</param>
/// <param name="GearEraRefused">Which places and quests the Farm Gear ERA gate refused, with
/// the era each page gives itself and the era the world is at (DRA-180 D2). Its own field
/// beside <paramref name="GearBandRefused"/> rather than folded into it: the two quote
/// different evidence, and a phone that drew the band sentence but not this one would leave the
/// player reading level numbers as the reason a place vanished for a different cause.</param>
/// <param name="MaterialEraRefused">The era gate's refusals on the FARM MATERIALS list
/// (DRA-180 D2) — its own field for the reason <paramref name="MaterialBandRefused"/> is its
/// own field.</param>
/// <param name="GearWhoWithheld">What the who rule held back — drop offers whose item page names
/// no creature in that zone and which this character has never looted there (DRA-84 D4). Its own
/// field beside <paramref name="GearWithheld"/> rather than summed into it: a cap and a rule are
/// different causes, and one number could explain neither.</param>
/// <param name="GearOffHandRefused">What the off-hand rule refused — upgrades that beat the
/// worn item on every number and are two-handed, for a character with something in SECONDARY
/// (DRA-222 D6, S7.3). Its own field beside <paramref name="GearWhoWithheld"/> for that one's
/// reason: five causes with five remedies, and a phone summing any two of them would hand the
/// player a number that explains neither.</param>
/// <param name="UnreadWorn">The worn rows EQBuddy could not read about, named (DRA-149 D2). The
/// fourth field of this shape and the only one that is not a decision — the three above chose to
/// hold something back, this one is EQBuddy admitting it never had the row. It rides the wire for
/// the reason all of them do: a phone that quietly listed one fewer anchor than the PC is the two
/// surfaces disagreeing about what the player is wearing.</param>
/// <param name="UnreadWornDoors">Where to check those names, as intent — one wiki search per
/// NAMED item. Its own field rather than doors on a note, because this sentence is a caption in
/// the caption stack rather than a <see cref="CompanionHelperNote"/>, and the desktop draws its
/// doors the same way.</param>
/// <param name="MaterialBandRefused">Which zones the band gate refused on the FARM MATERIALS
/// list (DRA-149 D3). Its own field beside <paramref name="GearBandRefused"/> rather than the
/// same one: they are the same rule over the same catalog, which is precisely why one merged
/// sentence could explain neither list — and the two engines run independently, so a player can
/// have one without the other.</param>
/// <param name="MaterialWhoWithheld">What the who rule held back on the materials list
/// (DRA-149 D3). Same producer and the same words as <paramref name="GearWhoWithheld"/> — the
/// rule, the cause and the remedy are identical — on its own field for the reason above.</param>
/// <param name="AnchorsAllRemoved">One sentence per WORN ITEM whose every catalog upgrade the
/// ladder removed (DRA-180 D3). A list rather than one joined string because the phone draws
/// them as separate captions exactly as the PC does, and because the cap is a COUNT of them —
/// joining here would make <paramref name="AnchorsNotNamed"/> a number about a string. Already
/// capped and already worded by <c>HelperPresentation</c>: the projection decides no word and
/// no cap (trap 33). This is the field the Founder's bow and Baron FAILs are answered in, so a
/// phone that carried the block captions without it would be the surface that still says
/// nothing.</param>
/// <param name="AnchorsNotNamed">What the cap above held back, said out loud (trap 50).</param>
/// <param name="MaterialNote">Where the materials rows came FROM, and what still has no page
/// (DRA-149 D3). Empty where no materials row was built, which is the money note's rule beside
/// it, for the money note's reason.</param>
/// <param name="MerchantNote">Where the vendor lines came from, said once over the whole block
/// (DRA-149 D4). Empty when no profession is listed.</param>
/// <param name="MerchantDoorNote">The zone-page door, as INTENT (trap 35). One sentence over
/// the block rather than a link per line: the phone cannot open a browser on the PC, and the
/// desktop's per-row door tip is the same sentence with a zone name in it — thirty copies of it
/// down a phone screen is the wall trap 73 names in prose.</param>
/// <param name="Merchants">One entry per LISTED profession, in the curated enum's order. It is
/// the listed set and not the picked one, because "picked nothing" means "show me all eight" —
/// the rule lives in <c>TradeskillPickStore.ListedFrom</c> so the phone cannot arrive at a
/// different eight from the PC.</param>
/// <param name="Gaps">Answerable goals that produced nothing, each with its reason and —
/// where the answer is a file the game writes — the command as selectable text.</param>
/// <param name="Deferred">Selected goals whose engine does not exist yet, each naming the
/// room that answers its question today.</param>
/// <param name="Empty">The whole-room empty state, when there is nothing at all to say. Null
/// is omitted from the JSON.</param>
public sealed record CompanionHelperSection(
    string Question,
    string PicksLead,
    string DoorsLead,
    IReadOnlyList<CompanionHelperPick> Picks,
    string AnswersHeading,
    string SourceNote,
    string LevelNote,
    IReadOnlyList<CompanionHelperAnswer> Answers,
    string MoneyNote,
    string GearBaseNote,
    string Cap,
    string GearWithheld,
    string GearBandRefused,
    string GearEraRefused,
    string MaterialEraRefused,
    string GearWhoWithheld,
    string GearOffHandRefused,
    string UnreadWorn,
    IReadOnlyList<CompanionHelperDoor> UnreadWornDoors,
    IReadOnlyList<string> AnchorsAllRemoved,
    string AnchorsNotNamed,
    string MaterialBandRefused,
    string MaterialWhoWithheld,
    string MaterialNote,
    string MerchantNote,
    string MerchantDoorNote,
    IReadOnlyList<CompanionHelperMerchants> Merchants,
    IReadOnlyList<CompanionHelperNote> Gaps,
    IReadOnlyList<CompanionHelperNote> Deferred,
    CompanionHelperEmpty? Empty = null);

/// <summary>
/// One profession's vendor lines, transcribed from eqlwiki's zone maps (DRA-149 D4).
///
/// <para>Every string here is a sentence <c>HelperPresentation</c> already built, and
/// <see cref="Lines"/> carries the wiki's own words with the zone in front. The projection
/// decides nothing: which lines, how many, and what the cap says are all the desktop room's
/// answers, asked of the same <c>ZoneMerchants</c> catalog.</para>
/// </summary>
/// <param name="Profession">The trade, in the curated list's own spelling.</param>
/// <param name="Lines">Up to <c>HelperPresentation.MerchantLineCap</c> rows, one per zone.</param>
/// <param name="More">What the cap held back (trap 50). Empty when it held nothing.</param>
/// <param name="Empty">The honest empty state when no zone page names this trade. Empty string
/// when <see cref="Lines"/> has something — the two are never both set, and the page draws
/// whichever is there rather than deciding which case it is in.</param>
public sealed record CompanionHelperMerchants(
    string Profession,
    IReadOnlyList<string> Lines,
    string More,
    string Empty);

/// <summary>
/// One of the desktop room's pickers, ported as INTENT (trap 35).
///
/// <para><see cref="Face"/> is the picker face's own answer — <c>PickerFace.For</c>'s
/// "Any goal" / "Level Up · Farm Gear" / "4 goals" — so the phone and the PC say the same
/// thing about one selection rather than the page counting for itself. <see cref="Note"/> is
/// the sentence the desktop draws ABOVE the control, which is what says the empty state means
/// "all of them".</para>
/// </summary>
/// <param name="Heading">Which decision this is, in the room's own heading words.</param>
/// <param name="Face">What is picked.</param>
/// <param name="Note">What the picker does, including what empty means.</param>
/// <param name="Detail">A second line where the decision has one — the gear intent's tip, a
/// picker's own "no dump yet" sentence. Empty draws nothing.</param>
/// <param name="Prompt">The in-game command, where what is missing is a file the game writes
/// and the picker itself is the empty state that asks for it.</param>
public sealed record CompanionHelperPick(
    string Heading,
    string Face,
    string Note,
    string Detail = "",
    CompanionCommandPrompt? Prompt = null);

/// <summary>One ranked answer: its headline, which goals it serves, its why-lines, and its
/// doors as intent.</summary>
/// <param name="Serves">The cross-domain chain said out loud (HOME-005). Empty when it serves
/// one goal and there is nothing to say.</param>
/// <param name="Why">The evidence, in order. Every line is a sentence
/// <see cref="HelperPresentation.Why"/> already built, catalog label included.</param>
/// <param name="WithheldWhy">What the per-row why cap held back, when it held anything.</param>
/// <param name="Doors">Where to go next — see <see cref="CompanionHelperDoor"/>.</param>
public sealed record CompanionHelperAnswer(
    string Headline,
    string Serves,
    IReadOnlyList<CompanionHelperWhy> Why,
    string WithheldWhy,
    IReadOnlyList<CompanionHelperDoor> Doors);

/// <summary>One why-line, and whether it is the player's own evidence or the shipped
/// catalog's estimate.</summary>
/// <param name="Text">The finished sentence. A catalog line already carries its estimate
/// label (HOME-004) — the label is appended by the one producer, so a new catalog-sourced
/// fact cannot arrive here unlabelled.</param>
/// <param name="Personal">True for the player's own play. It rides so the page can weight the
/// line visually; it is NOT a second copy of the label, which is in the text.</param>
public sealed record CompanionHelperWhy(string Text, bool Personal);

/// <summary>
/// A door, on the surface that cannot open it.
///
/// <para>Every door in this room lands somewhere on the PC — a shell room, a settings tab, or
/// a browser the player opens themselves — so the phone gets the NAME and the sentence that
/// says what is behind it, under one lead that says where. The desktop puts that sentence on
/// a hover; here it rides the row, because a phone has no pointer (trap 35).</para>
/// </summary>
/// <param name="Label">The door's short name, as the desktop's button reads.</param>
/// <param name="Detail">What opens — the desktop's tooltip, riding the row.</param>
public sealed record CompanionHelperDoor(string Label, string Detail);

/// <summary>A sentence with somewhere to go and, sometimes, a command to type. The shape
/// the level note, the gaps and the deferred goals all take — one record rather than three,
/// because they are one thing: something EQBuddy cannot answer yet, and what to do about
/// it.</summary>
/// <param name="Text">The already-worded sentence.</param>
/// <param name="Doors">Where the answer lives, as intent. May be empty.</param>
/// <param name="Prompt">The in-game command, as selectable text, when what is missing is a
/// file the game writes.</param>
public sealed record CompanionHelperNote(
    string Text,
    IReadOnlyList<CompanionHelperDoor> Doors,
    CompanionCommandPrompt? Prompt = null);

/// <summary>The whole-room empty state: nothing picked has anything to say, and no gap
/// explains why. Two sentences, both <see cref="HelperPresentation.Nothing"/>'s.</summary>
public sealed record CompanionHelperEmpty(string Heading, string Explanation);

/// <summary>
/// What the host gathers for one Helper pass: the engine's own input object, the goals it is
/// ranked against, and the two presentational facts the PICKERS need that the engine does not
/// read.
///
/// <para><b><see cref="Inputs"/> is the whole point of the slice.</b> It is the same record
/// the desktop room builds, assembled by the same <c>HelperSources</c> bundle, so "the phone
/// showed something else" is a question about the inputs rather than about which engine
/// somebody called (trap 33). Nothing in the projection adds to it or narrows it.</para>
///
/// <para>The professions ride separately because they are not an engine input — Farm
/// Materials is <c>Deferred</c> and its block is a picker, a standing list and two doors
/// (DRA-71 D8). They are here so the phone can SHOW the pick rather than compute one.</para>
/// </summary>
/// <param name="Inputs">Everything <c>Recommendations.Rank</c> reads.</param>
/// <param name="Goals">What this character is working toward. Empty weighs all of them.</param>
/// <param name="Professions">The picked professions, in the curated enum's order.</param>
/// <param name="ProfessionsOffered">How many the picker offers — the curated eight, asked of
/// <c>Tradeskills</c> rather than spelled as a number, so the day a ninth earns a Mastery AA
/// the face counts against the real list.</param>
public sealed record CompanionHelperRequest(
    HelperInputs Inputs,
    IReadOnlyList<HelperGoal> Goals,
    IReadOnlyList<Tradeskill> Professions,
    int ProfessionsOffered);
