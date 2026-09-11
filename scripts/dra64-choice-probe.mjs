// DRA-64 measurement, not a shipped artifact. Lifts the SHIPPED choice logic out of
// index.html and runs it over a fake localStorage, so the claim about a poisoned stored
// choice is measured across the PERSISTENCE ROUND TRIP rather than reasoned about.
//
//   node dra64-probe.mjs
//
// Scenarios A/B/C/D are the Founder's four situations; E and F are the negatives that
// say the repair does not eat a real decision and does not repeat itself.
import { readFileSync } from "node:fs";

// argv[2] lets this run against an OLDER page, which is how the prove-fail is done:
//   git show cc020280:src/EQBuddy.Companion/Web/index.html > /tmp/before.html
//   node scripts/dra64-choice-probe.mjs /tmp/before.html     # C and D must FAIL
const pagePath = process.argv[2] || "src/EQBuddy.Companion/Web/index.html";
const page = readFileSync(pagePath, "utf8");
console.log(`(reading ${pagePath})\n`);

function lift(re, what) {
  const m = page.match(re);
  if (!m) { console.error(`absent: ${what} — treated as "the repair is not there"`); return ""; }
  return m[0];
}

// The real thing, verbatim from the shipped page — comments and all.
const playerHasPickedSrc = lift(/ {2}const playerHasPicked = [^\n]*/, "playerHasPicked");
const ensureChoiceSrc = lift(/ {2}function ensureChoice\(\) \{[\s\S]*?\r?\n {2}\}/, "ensureChoice()");
const commitSrc = lift(/ {2}function commitChoice\(\) \{[\s\S]*?\r?\n {2}\}/, "commitChoice()");

// commitChoice() also calls into the DOM; only its one DRA-64 line matters here.
const stampSrc = commitSrc.includes("choice.playerPicked = true")
  ? "choice.playerPicked = true;"
  : null;

console.log("--- lifted ensureChoice() gate ---");
console.log(ensureChoiceSrc.split("\n").filter(l => /if \(|return |enabled\[s\] =/.test(l)).join("\n"));
console.log("--- commitChoice() stamps the fact: " + (stampSrc ? "YES" : "NO") + " ---\n");

// One device. `store` is its localStorage for this token.
function device({ label, firstRun, store, pcOffers, playerTurnsAllOff = false, reopen = false }) {
  const STORE_KEY = "eqbuddy-screens-0123abcd";
  const log = [];

  function session(offeredList) {
    // ---- boot(): load the stored choice ----
    let choice = null;
    try { choice = JSON.parse(store[STORE_KEY] ?? "null"); } catch { choice = null; }
    const hadStoredChoice = !!choice;
    const saveChoice = () => { store[STORE_KEY] = JSON.stringify(choice); };
    const FIRST_RUN = firstRun;
    let offered = [];                      // what the page holds before the first snapshot
    const picked = () => choice.order.filter(s => choice.enabled[s]);

    const body = `
      ${playerHasPickedSrc}
      ${ensureChoiceSrc}
      // ---- the first snapshot's onmessage, as shipped ----
      const offerChanged = JSON.stringify(snapshotOffered) !== JSON.stringify(offered);
      offered = snapshotOffered;
      const rescued = ensureChoice();
      if (offerChanged || rescued) { saveChoice(); }
      return { rescued: rescued, noticed: !!(rescued && hadStoredChoice) };
    `;
    const run = new Function(
      "snapshotOffered", "offered", "FIRST_RUN", "hadStoredChoice",
      "getChoice", "setChoice", "saveChoiceOuter",
      `let choice = getChoice();
       const saveChoice = () => { setChoice(choice); saveChoiceOuter(); };
       ${body}`);

    const r = run(offeredList, offered, FIRST_RUN, hadStoredChoice,
      () => choice, c => { choice = c; }, () => saveChoice());

    if (playerTurnsAllOff) {
      for (const s of Object.keys(choice.enabled)) choice.enabled[s] = false;
      if (stampSrc) choice.playerPicked = true;   // commitChoice()
      saveChoice();
    }
    return { picked: picked(), ...r };
  }

  log.push(session(pcOffers));
  if (reopen) log.push(session(pcOffers));

  const first = log[0];
  const last = log[log.length - 1];
  console.log(`${label}`);
  console.log(`  FIRST_RUN : ${JSON.stringify(firstRun)}   PC offers: ${JSON.stringify(pcOffers)}`);
  log.forEach((r, i) => console.log(
    `  session ${i + 1}: picked=${JSON.stringify(r.picked)}` +
    `  rescued=${r.rescued}  told-the-player=${r.noticed}`));
  console.log(`  stored    : ${store[STORE_KEY]}`);
  console.log(`  => ${last.picked.length ? "PAINTS" : "*** BLANK PAGE ***"}\n`);
  return { first: first, last: last };
}

const PHONE = ["spawns", "session"];                            // innerWidth < 900
const WIDE = ["map", "spawns", "mez", "session", "quests"];      // innerWidth >= 900
const OFFERED = ["quests", "gear"];   // the Founder's CompanionHiddenSurfaces

// What the BROKEN build left on the Founder's phone: the first snapshot's offer-changed
// save, with FIRST_RUN(phone) missing every offered surface.
const POISONED = { "eqbuddy-screens-0123abcd":
  JSON.stringify({ order: ["quests", "gear"], enabled: { quests: false, gear: false } }) };

console.log("=========== DRA-64: why the PC paste works and the phone does not ===========\n");

const a = device({ label: "A. PC browser paste, wide viewport, clean storage",
  firstRun: WIDE, store: {}, pcOffers: OFFERED });

const b = device({ label: "B. Phone, FIRST pairing, clean storage (#550's case)",
  firstRun: PHONE, store: {}, pcOffers: OFFERED });

const c = device({ label: "C. THE FOUNDER'S PHONE — already paired under the broken build",
  firstRun: PHONE, store: { ...POISONED }, pcOffers: OFFERED, reopen: true });

const d = device({ label: "D. Phone paired while the PC shared nothing, then it shared quests",
  firstRun: PHONE,
  store: { "eqbuddy-screens-0123abcd": JSON.stringify({ order: [], enabled: {} }) },
  pcOffers: ["quests"] });

const e = device({ label: "E. NEGATIVE — the player turns every offered screen off on purpose",
  firstRun: PHONE, store: {}, pcOffers: OFFERED, playerTurnsAllOff: true, reopen: true });

const f = device({ label: "F. The repair is persisted, so it does not repeat (or re-nag)",
  firstRun: PHONE, store: { ...POISONED }, pcOffers: OFFERED, reopen: true });

const checks = [
  ["A paints (explains the working PC paste)", a.last.picked.length > 0],
  ["A needed no rescue — the wide FIRST_RUN already overlapped", a.last.rescued === false],
  ["B paints (the #550 rescue still works)", b.last.picked.length > 0],
  ["B said nothing — a first pairing is a default, not an override", b.last.noticed === false],
  ["C paints (DRA-64: the poisoned phone recovers)", c.last.picked.length > 0],
  ["C told the player its arriving picks were changed", c.first.noticed === true],
  ["D paints (same hole, reachable from a patched build)", d.last.picked.length > 0],
  ["E stays blank — a real decision is not overridden", e.last.picked.length === 0],
  ["E was not re-rescued on reopen", e.last.rescued === false],
  ["F did not rescue a second time", f.last.rescued === false],
  ["F did not re-nag on reopen", f.last.noticed === false],
];
console.log("=========== assertions ===========");
let bad = 0;
for (const [what, ok] of checks) { if (!ok) bad++; console.log(`${ok ? "ok  " : "FAIL"}  ${what}`); }
process.exit(bad ? 1 : 0);
