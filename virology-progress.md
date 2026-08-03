# Virology Development Progress

Updated: 2026-08-03

## Completed

### Disease foundations

- Added pathogen prototypes, runtime components, registry logic, infection progression,
  analysis support, and virology equipment under `_Starlight`.
- Added pathogen resistance relaying through worn inventory items.
- Added two random ambient strains at round start, forced toward different pathogen types.
- Kept automatic ambient selection away from beneficial and emergent strains.

### Pathogen transmission

- Transmission belongs to the pathogen type, not individual symptoms.
- Viruses use the widest and strongest five-second proximity spread. They have no
  persistent environmental source.
- Bacteria no longer participates in the periodic proximity sweep. It spreads mainly
  through successful physical-contact actions: hugging/petting, pulling, melee hits,
  cuffing, and topical medical treatment.
- Fungi have no person-to-person transmission. They infect only from environmental
  sources such as rot, biological puddles, and visible strain-specific spore patches.
- All three routes use the same host eligibility, immunity, PPE/resistance, per-strain
  prevalence, and per-tier prevalence checks.
- A contact action can transmit at most once in each direction, and a contagious source
  host must have reached stage 1.
- Viral sweeps first snapshot all contagious source/strain pairs, then perform exposure
  attempts. Infecting a previously healthy host can therefore add an infection component
  without modifying the collection currently being enumerated and crashing the server.
- Symptoms only control what the host experiences.

### Pathogen protective equipment

- Replaced per-item resistance coefficients with six physical classifications:
  `FilterMask`, `SupplyMask`, `SterileBarrier`, `BioSuit`, `BioHood`, and `SealedSuit`.
  Clothing prototypes now declare only their class; all protection numbers live in the
  shared resistance system. The class field is required, so prototype validation rejects
  an unclassified PPE component instead of silently assigning the first enum value.
- Working internals or a bio hood completely blocks inhaled viruses. Gas masks and sterile
  masks provide 90% virus protection. Breath masks, medical oxygen masks, and pressure
  helmets provide none unless they are actually connected to working internals.
- Bio suits completely block contact-spread bacteria. Latex and nitrile gloves provide 90%
  protection. Hardsuits and ordinary gloves provide none because they are not sterile.
- Fungal protection adds an inhaled half to a settling-spore half. Working internals or a
  bio hood contributes 50%; filter masks contribute 40%. Bio suits contribute 50% settling
  protection and hardsuit/EVA outer suits contribute 45%.
- Without a suit, fungal settling protection comes from occupied clothing slots: uniform
  15%, outer clothing 10%, shoes 6%, gloves 4%, head 3%, and eyes 2%, capped at 40%.
- `ProtectionBypass` leaves complete protection untouched and scales down only partial
  protection. Deliberate pathogen administration still calls the direct infection API and
  bypasses PPE.
- Vox working internals are intentionally accepted as a species trait: they receive total
  virus protection and the 50% fungal inhalation half while their tank setup is functional.
- Classified items show categorical protection text when examined rather than a misleading
  standalone percentage that depends on the wearer's complete outfit.

### Symptom content

- Ambient symptoms include sneezing, coughing, fatigue, chills, watery eyes, sniffles,
  hiccups, mild stuttering, slurred speech, itching, mild dizziness, harmless rash,
  and mild sweating.
- Emergent symptoms include headache, nausea, muscle aches, fever, shortness of breath,
  shaky hands, hoarseness, blurred vision, clumsiness, vomiting, nosebleed, and
  drowsiness.
- Ambient archetypes have larger symptom pools for round-to-round variety.
- Added three inactive emergent archetypes: Station Fever, Grey Lung, and Red Flux.
- Emergent archetypes do not seed at round start and do not respawn after extinction.
- Fixed Watery Eyes so it can occur in a two-stage Spore Bloom strain.

### Contamination outbreaks

- Contamination is clamped from 0 to 100 and is composed of viral, bacterial, and fungal
  signature values that always add up to the general station meter.
- The current live source snapshot is aggregated by pathogen type before thresholds are
  evaluated. The largest signature becomes the preferred outbreak type, eliminating
  source iteration order from the result. Exact ties are selected randomly.
- At 25 contamination, the system seeds a third ambient strain.
- At 50 contamination, the system evaluates the emergent outbreak exactly once.
- At 75 contamination, the system seeds a fourth ambient strain.
- Ambient selection avoids pathogen types already present until all three types have
  appeared, then permits repeats for the fourth strain.
- With no antagonist, the emergent outbreak is guaranteed.
- With an antagonist, the emergent outbreak has a configurable 40% chance.
- Antagonist-round emergent strains retain three stages but default to an 8% prevalence
  cap, 70% transmissibility, half the normal PPE bypass (20-25% effective), 25% slower
  stage progression, and one or two extra symptoms.
- Emergent strains never respawn after eradication.
- Ambient contamination outbreaks infect a random eligible crew member. Contamination
  sources bias the pathogen type but do not determine the initial host, representing
  disease circulating through station ventilation.
- Emergent outbreaks have no source entity. The same strain starts in two random eligible
  players, falling back to one player on low population.
- Initial hosts must be in-game, non-AFK, living humanoids with a mind, outside cryostorage,
  and able to host disease.
- The same centralized eligibility rule is used for every automatic pathogen placement:
  round-start ambient seeds, 25/75 ambient seeds, the 50 emergent outbreak, pre-contagious
  emergent replacement, and ambient extinction respawn.
- Natural person-to-person transmission does not apply the AFK filter. An exposed AFK
  player can still catch a disease, preventing inactivity from acting as immunity.
- Before any initial host reaches contagious stage 1, an initial carrier who becomes AFK,
  disconnects, dies, ghosts, or enters cryostorage is cured and replaced once.
- A real cure is never replaced. Once any carrier reaches stage 1, all automatic
  replacement stops, so eradication remains permanent.
- Every threshold remains consumed after contamination falls, so it cannot retrigger.

### Contamination sources and counterplay

- Added a ten-second live-round sampler that reads existing physical station entities
  instead of maintaining a parallel hidden source list. The timer only refreshes the
  snapshot; contamination does not accumulate with elapsed time.
- Threshold outbreaks still select a random eligible initial host to represent spread
  through station ventilation. Independently, physical sources can now infect eligible
  crew within 1.5 metres when an active strain of a matching pathogen type exists.
- Actively rotting corpses contribute 3 current contamination points split between
  bacteria and fungus.
- Rotten edible items contribute 1.5 current contamination points split between bacteria
  and fungus.
- Other active rot contributes 0.9 current contamination points split between bacteria
  and fungus.
- A dual-signature source splits its contribution between bacteria and fungus so it does
  not double the general contamination.
- Biological puddles, including blood and vomit, contribute a bacterial signature based
  on reagent volume, capped at 2.4 current points per puddle, and carry a small flat local
  infection chance.
- Nutriment and protein left in floor puddles contribute a low bacterial signature,
  capped at 1.2 current points per puddle. Mold reagent contributes a fungal signature,
  capped at 1.8. These hygiene residues do not receive the biological-puddle minimum
  infection chance.
- Loose discarded food wrappers, empty tins, broken dinnerware, peels, cobs, and pits
  contribute 0.1 bacterial points each. Contained organic trash stops contributing
  immediately. Once flushed into the disposal network, the organic-trash classification
  is recursively removed from the item and its contents, so material ejected into the
  disposal room does not become a source again.
- A hydroponics tray containing a dead plant contributes 0.75 fungal points until the
  plant matter is removed. Rotting organs and gibs remain covered by the existing 0.9
  generic-rot path.
- Ordinary rot only becomes locally infectious above its configured source-point threshold.
  Puddles and fungal patches keep their configured minimum chance below that threshold.
- Sources only carry currently active matching strains. Fungal patches pin the exact
  strain that created them.
- Nearby candidates are shuffled, checked for line of sight, and passed through the
  shared immunity/PPE/prevalence rules. Each source can cause at most one infection per
  ten-second sample.
- Refrigerated, morgue-contained, and otherwise paused rot is ignored through the existing
  rotting system's `IsRotProgressing` check.
- Every refresh replaces the previous meter with the sum of sources that exist now.
  Removed, cleaned, refrigerated, expired, or otherwise inactive sources therefore stop
  contributing; there is no passive decay and no historical contamination value.
- Source points, sampling interval, local infection thresholds, and emergent overlap
  values are server CVars.
- The handheld virology monitor shows the station's total contamination on a 0-100 bar
  and lists the current viral, bacterial, and fungal signature values.
- Physical sources on the user's grid are grouped by their nearest enabled navigation
  beacon, so the monitor map reveals a contaminated area rather than an exact source
  position. Markers scale with group contamination, blink when a group contains an
  infectious source, and use different colors for bacterial, fungal, and mixed groups.
- The adjacent area list shows each beacon group's current contamination value without
  revealing individual source entities.
- Added a targeted biological decontaminator. Using it on a detected source immediately
  suppresses that physical source for five minutes and immediately rebuilds the live
  contamination snapshot. It does not subtract an arbitrary number of stored points.
- Contagious fungal hosts have one low per-sample chance to create one vivid green,
  strain-pinned spore patch. A patch lasts up to ten minutes, contributes fungal
  contamination, can infect locally, and can be removed with either the decontaminator
  or any normal absorbent mop.
- Physical environmental sources never use a viral signature. Instead, each living,
  symptomatic viral carrier on a station grid contributes 0.5 to the global viral
  signature. This airborne reading has no fake surface source or room-map marker and
  disappears when the carriers recover, die, or leave the station.
- The monitor and decontaminator are stocked in the filled virologist locker and
  ViroDrobe, so no map edit is required.
- Source sampling does not run in the lobby or after round end.

### Diagnosis and viable cultures

- Hosts now carry exactly one strain. A higher-tier strain displaces the current strain
  and grants immunity to the displaced strain; equal- and lower-tier exposures bounce.
- Identification progress and distinct sampled-host identities are stored on the
  round-local runtime strain. Separate virologists therefore contribute to the same
  station-wide result, and all progress clears with the strain registry at round restart.
- The handheld virology monitor and optional mapper-placeable console query suit sensors
  directly and show infected crew only when sensors are on vitals or coordinates mode.
  The sick-crew payload is structurally a list of names: it contains no crew coordinates,
  pathogen designation, identification state, or diagnosis details.
- Added a dedicated anonymous pathogen swab. A two-second adjacent sample can come from an
  infected host or an active contamination source; no host identity is shown on the swab.
  Source sampling performs a normal full-strength exposure against the sampler, including
  PPE, immunity, prevalence, and displacement rules.
- A filled swab transfers only into an empty `ChemistryEmptyVialSmall` and is consumed.
  The vial must then contain water before the existing tabletop centrifuge accepts it.
- The centrifuge has a six-vial pathogen batch container and one ten-second batch timer.
  Vials added while a batch is running complete together and are ejected as analysable
  cultures.
- The existing `DiseaseDiagnoser` consumes an analysable culture after a five-second
  do-after and prints the existing `DiagnosisReportPaper`.
- The first distinct patient report reveals designation, type, and symptoms, with
  `INSUFFICIENT DATA` for incubation, duration, transmissibility, and origin. A second
  distinct host completes those fields and produces one physical viable culture.
- An active-source specimen completes identification and produces the viable culture in
  one analysis. A duplicate patient is rejected before analysis and rechecked at
  completion, without consuming the specimen.
- Natural versus engineered origin is withheld until full identification. Virulent and
  beneficial strains report engineered; ambient and emergent strains report natural.
- The monitor and swabs are stocked in the ViroDrobe and printable from the medical
  lathe. No map edit, crew-monitor server, health-analyzer change, or fixed console is
  required.

### Direct pathogen analyser

- Added a handheld pathogen analyser with the health analyser's direct-use workflow and
  a short, interruptible adjacent scan.
- It scans healthy or infected crew, active contamination sources, prepared and
  unprepared cultures, viable cultures, and empty or configured pathogen injectors.
- A negative target reports that no pathogen was detected. A strain that has not been
  fully diagnosed is reported only as unidentified, without leaking its designation or
  hidden statistics.
- Fully identified strains display designation, classification, severity, origin,
  symptoms, incubation, duration, transmissibility, PPE bypass, prevalence cap, and
  target-specific context such as infection stage, culture state, injector mode, and
  remaining doses.
- Direct scans are observational and do not advance station-wide diagnosis progress or
  consume cultures and injector doses.
- The analyser is printable from the medical lathe and stocked in the ViroDrobe, filled
  virologist locker, medical resupply pack, and admin test kit without requiring map edits.

### Handheld virology monitor

- Replaced the overlapping virology detector and contamination scanner with one handheld
  monitor and retained the optional powered console on the same interface.
- Its single live view contains the station contamination bar, typed signature totals,
  beacon-grouped contamination map, area contamination list, and qualifying sick-crew
  names. Open monitor views refresh every two seconds.
- Sick crew are deduplicated and alphabetized. Sensors below vitals mode are ignored, and
  no crew position or pathogen information exists in the monitor's network state.
- Room markers reuse the physical-source sampler rather than creating another source
  registry or time-based contamination system.
- The monitor uses the existing handheld crew-monitor art so it is visually distinct from
  the health-analyser-style pathogen analyser.
- The obsolete standalone scanner prototype, BUI, localization, admin-kit entry, and
  distribution references were removed. The monitor is printable and stocked in the
  ViroDrobe, filled virologist locker, and admin test kit.

### Admin live-testing environment

- Added the admin-debug command `virotest`. It is unavailable to ordinary players and
  changes no normal-round behavior until an authorized tester runs it.
- `virotest setup self` creates or reuses one runtime strain from each ambient and
  emergent archetype, prints their round-local IDs, and spawns the detector, swab, mini
  vial, diagnoser, centrifuge, contamination tools, gas/sterile/supply masks, sterile
  gloves, bio suit/hood, and an engineering hardsuit at the tester.
- `virotest strains` lists every runtime strain with its archetype, type, tier, stages,
  transmissibility, range, PPE bypass, prevalence, and symptoms.
- `virotest status self` reports host eligibility, automatic-outbreak eligibility,
  infections, stages, progression/clear timers, identification state, effective PPE
  protection, and the final exposure multiplier.
- `virotest infect`, `cure`, `stage`, and `identify` provide deterministic infection
  setup. `stage` restarts the normal stage and symptom timers at the requested stage.
- Infections created by `virotest infect` now use a test-only three-second symptom
  interval so live checks do not wait through production cadence. `virotest fast
  <target> <strainId|all> [seconds|off]` adjusts the interval down to a safe minimum of
  0.5 seconds or restores normal authored timing. Eligible fast-test symptoms are evenly
  staggered within that interval so simultaneous local popups do not replace each other.
  Ordinary infections are unchanged.
- `virotest protection` reports the exact effective protection against a selected runtime
  strain. `virotest expose` runs one normal transmission roll with an optional base chance,
  preserving PPE, immunity, and prevalence checks.
- `virotest contamination` reads or directly sets the viral, bacterial, and fungal
  signatures, immediately exercising the normal one-shot 25/50/75 milestones.
- `virotest sample` immediately runs the normal physical-source sampler during a live
  round. `virotest sweep` immediately runs one normal viral proximity sweep.
- `virotest spore` creates a visible fungal patch pinned to the selected fungal strain.
- Entity arguments accept `self` or a network entity ID. `virotest help` prints the full
  syntax in the console.

## Current Design Rules

- Ambient disease is harmless, visible, self-clearing raw material for virologists.
- Emergent disease is not an antagonist. It creates a manageable medical job after
  station neglect and should remain unpleasant but survivable.
- Virulent disease is reserved for antagonist-level danger.
- Beneficial strain content and balance are postponed, but beneficial administration uses
  the same discrete-dose injector infrastructure as treatment and live vaccines.
- A host carries exactly one strain. Only a higher tier displaces it, and displacement
  grants immunity to the removed strain.
- Identification is per runtime strain and station-wide, not per patient infection.
- Patient identification requires two distinct hosts; a physical source completes it in
  one analysis and exposes the sampler.
- Detection through suit sensors never provides coordinates.
- A symptom must never change transmission behavior.
- Two ambient strains seed at round start; additional ambient outbreaks occur at
  contamination 25 and 75, allowing four ambient strains over a round.
- The one-time emergent check occurs at contamination 50.
- Fork-owned code and prototypes stay under `_Starlight` where possible.

## Verification

- Client and server projects build with 0 errors.
- The final server build completed with 0 errors and 330 existing code and dependency
  warnings. The dependency output includes existing high-severity advisories for
  `System.Security.Cryptography.Xml`.
- Focused contamination tests pass: 14 passed, 0 failed. They cover fixed source points,
  puddle caps, local infection chance, signature composition, one-shot milestones,
  emergent reduction, spore prototypes, proportional clamping, and snapshot replacement
  without time accumulation.
- The broader prototype serialization tests pass: 2 passed, 0 failed.
- Focused PPE tests pass: 6 passed, 0 failed. They cover the requested virus, bacteria, and
  fungus outfit matrix; bypass behavior; fourteen concrete and inherited clothing
  classifications; and zero transmissibility on both fungal archetypes.
- The PPE balance pass raised filtered virus protection and sterile-glove bacterial
  protection to 90%. The focused PPE suite was rerun afterward: 6 passed, 0 failed.
- The PPE-phase server build completed with 0 errors and no warnings from virology files.
  Existing repository warnings and `System.Security.Cryptography.Xml` advisories remain.
- The admin test-environment server build completed with 0 errors. Its real-console
  integration test passes: 1 passed, 0 failed. It verifies six test strains, all ten kit
  prototypes, infection, forced staging, identification, typed contamination, pinned
  fungal patches, and curing.
- The complete virology-focused integration set passes: 21 passed, 0 failed.
- The fast-symptom test phase server build completed with 0 errors. The focused admin
  harness test passes: 1 passed, 0 failed, including the automatic three-second interval
  and a manual one-second override. The complete virology-focused integration set was
  rerun afterward: 21 passed, 0 failed.
- Live testing showed synchronized fast fungal popups hiding the earlier watery-eyes
  message behind the later rash message. Fast-test schedules now distribute eligible
  symptoms across the interval. The server build completed with 0 errors, the focused
  harness regression passes 1/1 with distinct fungal timers, and the full virology set
  passes 21/21.
- Live testing exposed a server crash during an automatic viral spread sweep:
  `Collection was modified; enumeration operation may not execute`. Spread now snapshots
  contagious sources before adding infections. The dedicated YAML/prototype linter found
  0 errors, the focused spread regression passes 1/1, and the complete virology suite
  passes 22/22.
- Opening the contamination scanner in a real client exposed an unregistered
  `PathogenContaminationScannerUiKey` and crashed PVS serialization. The UI key is now
  marked `Serializable` and `NetSerializable`, with a regression covering all three
  scanner payload types. YAML/prototype validation found 0 errors, focused contamination
  tests pass 15/15, and the complete virology suite passes 23/23.
- Virology work was separated from the already-pushed `casino-chips` branch onto a new
  `virology` branch based directly on `origin/starlight-dev`. The four casino commits and
  their 38 changed paths are absent from this branch; the uncommitted virology work was
  preserved. `RobustToolbox` was also restored to the revision pinned by
  `origin/starlight-dev`.
- The isolated branch passes YAML/prototype validation with 0 errors and the complete
  virology integration suite with 23 passed, 0 failed. `git diff --check` passes, branch
  history contains no commits beyond `origin/starlight-dev`, and scans of tracked and
  untracked changes found no casino, Gamorrah, treasurer, or brigmed content.
- Diagnosis-phase server and client builds complete with 0 errors. A filtered rebuild
  reports no compiler or analyzer warnings from `Medical/Virology` files; an older literal
  prototype-ID warning in the contamination test was converted to a typed `EntProtoId`.
- YAML/prototype validation passes with 0 errors after all diagnosis prototypes and machine
  extensions were added.
- Focused diagnosis tests pass 4/4. They cover tier displacement and immunity, equal/lower
  bounce behavior, distinct-host progress, duplicate rejection, partial and full report
  contents, the one-analysis source shortcut, suit-sensor mode filtering, designation
  reveal, absence of coordinate fields, and machine/item prototype configuration.
- The requested prototype serialization set passes 2/2. Its first run found that the
  centrifuge created the batch container only during component initialization; declaring
  the batch alongside the inherited mixer, board, and parts containers made spawned state
  stable and the rerun passed.
- The complete virology filter finishes with 26 passed, 0 failed, and one pooled spread
  test skipped by the integration harness. That exact spread regression passes 1/1 when
  run in isolation. The same full filter passed all 27 tests in an earlier diagnosis-phase
  run.
- Initial diagnosis compilation found two typed-entity overload mismatches in the
  centrifuge path. Passing the specimen entity ID fixed both; subsequent server, client,
  and integration-test builds completed with 0 errors.
- The first complete virology rerun exposed an older admin-harness assumption that only one
  strain of an archetype could exist in the shared test process. The harness now selects
  the newest test strain, and the rerun passes.
- Live symptom testing found that the three-second test interval can continuously refresh
  the four-second drowsiness status until it causes sleep, although normal 120-second
  symptom timing cannot do so. Six seconds avoided the false result. The harness default
  still needs correction before any further symptom test.
- Live stage-3 testing also showed that Station Fever and the tested Grey Lung roll did
  not feel meaningfully different from stage 2. Jitter stage scaling extends duration
  rather than amplitude, while stronger fever heat uses the same popup.
- Automated phase order is now YAML/prototype validation first when prototype data is
  involved, then compilation and focused/full integration tests. Manual playtesting is
  reserved for timing, presentation, and player-facing behavior after those gates pass.
- The first new regression compile used an unsupported NUnit collection-count constraint;
  the explicit distinct count compiled. Its first run then exposed an older assertion
  that assumed only one concurrent infection; narrowing that assertion to its virus
  strain made the rerun pass.
- The first test-environment build found two command-only nullable/definite-assignment
  errors; both were corrected. The first integration-test compile then needed the
  test project's explicit `EntityUid` namespace; the rerun passed.
- The first PPE test compile needed an explicit collection namespace in the integration
  test project. The next compile showed that shared internals are not test-visible, so the
  deterministic calculator became a public shared utility. The first runtime prototype
  check then showed abstract entity prototypes are omitted from the runtime index; the
  test now verifies concrete descendants and passes.
- Client compilation first exposed a sprite-system namespace mismatch, then the station
  map's private control and unsupported localized-name access. The implementation now uses
  the correct client namespace, a narrow public map-control accessor, and beacon-list
  annotations; the subsequent client build passed.
- Server compilation exposed two typed-entity arguments passed to the UI service. Passing
  the scanner entity ID fixed both, and the subsequent server build passed.
- Initial compilation found missing explicit LINQ and collection imports in the two new
  testable files. Both imports were added, and the subsequent server build and focused
  test run completed with 0 errors.
- The first spread-phase server compilation found three errors: two systems needed the
  shared interaction-system range helper and the spore placement call needed its
  coordinate helper namespace. Those were corrected; the next server build completed
  with 0 errors.
- The first focused spread test run found a duplicate absorbent interaction subscription
  during test-server startup. Spore cleaning was moved to the spore target's own
  `InteractUsingEvent`; the next focused run passed all 16 tests.
- The first focused-test wrapper timed out after compilation; rerunning the already-built
  test assembly completed successfully.
- The server's existing `IAfkManager` is used rather than a custom inactivity timer. Its
  normal player threshold defaults to 60 seconds through `afk.time`.
- AFK activity is refreshed by full player input commands, connection/status changes, and
  player console commands. The manager compares real elapsed time against the configured
  threshold; sessions with no activity record are treated as AFK.
- Missing hosts or content do not consume a guaranteed outbreak; a failed antagonist
  chance roll does consume the one-time emergent check.
- End-of-phase source audit removed a duplicate virology `using` directive.
- No temporary scripts or command snippets were added to the repository.
- `dotnet build-server shutdown` removed the MSBuild/compiler workers created during
  verification.
- The manual server/client were then intentionally restarted on local port 1213 through
  the temporary background console bridge outside the repository. SpaceCold stage 2 was
  resumed with the confirmed three-second test cadence.
- The remaining `Content.Server` process was later confirmed as the old `ss14-verify`
  instance started by this work, so its server and parent launcher were stopped. No
  `Content.Server` or `Content.Client` processes remain.
- Unrelated pre-existing worktree changes were preserved.
- Discrete-injector treatment tests pass 4/4. They cover dose consumption, cure and
  immunity behavior, empty reuse, non-empty mixing rejection, shared beneficial/live
  payload support, prototype slot configuration, delayed vaccinator completion, duplicate
  start rejection, five-dose output, and automatic ejection.
- The complete virology filter passes 30 tests with the known pooled spread regression
  skipped; that spread regression passes independently. The focused vaccinator production
  regression also passes independently.
- Post-treatment YAML/prototype validation completed with 0 errors. The integration-test
  rebuild completed the server and client projects with 0 errors; only existing repository
  and dependency warnings remain.
- The final treatment diff passes `git diff --check`. Its changed-path scan contains only
  virology systems, UI, tests, localization, prototypes, and their medical distribution;
  no casino files are present.
- Pathogen-analyser YAML/prototype validation completed with 0 errors. The focused
  analyser regression passes 1/1 and the full diagnosis group passes 5/5.
- The complete post-analyser virology filter passes 31 tests with the known pooled spread
  regression skipped. The client rebuild, including the analyser XAML and wrapped report
  fields, completed with 0 errors; only existing repository warnings remain.
- The first monitor build found two client map-wiring errors: a private generated XAML
  reference and a missing nav-map namespace. Both were fixed, and the client plus
  integration-test projects then built with 0 errors.
- The first no-build YAML check exposed a stale copied server assembly still containing
  the deleted scanner IDs. Rebuilding the linter dependencies and rerunning against the
  current code completed with 0 prototype errors.
- Focused post-monitor diagnosis tests pass 5/5. They cover sensor-mode gating, a
  name-only sick-crew payload, typed contamination totals, all analyser targets, and the
  renamed monitor prototype.
- The complete post-monitor virology filter passes 31 tests with the known pooled spread
  regression skipped: 0 failed out of 32 total tests.
- The final monitor diff passes `git diff --check`. All changed paths are virology code,
  UI, tests, localization, prototypes, distribution, and this progress document; casino,
  Gamorrah, treasurer, and brigmed content is absent.
- No temporary console commands, debug output, scripts, or generated test logs remain in
  the repository.
- The lightweight hygiene-source phase builds with 0 errors. Focused contamination tests
  pass 19/19 after correcting one test-only attempt to index abstract prototypes.
  YAML/prototype validation completes with 0 errors.
- The hygiene-source diff passes `git diff --check` and contains only the contamination
  sampler, its CVars/tests, food-waste tags, and this progress document. No casino or
  unrelated fork content is present, and no temporary command/debug lines remain.
- The disposal follow-up keeps the narrow `OrganicTrash` classification at 0.1 points
  rather than treating all baggable trash as biological contamination. A one-time
  disposal transition recursively removes that tag, with no room-name, beacon-name,
  pipe search, permanent marker, or additional sampler check.
- The final organic-trash disposal build completes with 0 errors and the focused
  contamination suite passes 20/20. The final diff contains no YAML changes because all
  organic-trash prototypes remain exactly as committed and previously validated.
- The permanent-immunity live-vaccine balance build completes with 0 errors and focused
  treatment tests pass 4/4. The regression pins the carrier's permanent self-immunity,
  1.5-tile range, 3% per-target pulse chance, ten-second interval, and ten-minute duration.

## Treatment Phase

- Replaced strain-bearing treatment reagents with one reusable, discrete-dose pathogen
  injector. Payloads cannot be poured, split, diluted, metabolized, or mixed.
- The same injector supports normal treatment, live vaccines, and beneficial strains.
  Treatment batches contain five doses; live and beneficial payloads contain one dose.
- A normal treatment dose cures the matching active strain or grants matching immunity.
  A live dose creates the existing temporary immunity-shedding carrier, and a beneficial
  dose deliberately applies its matching beneficial strain.
- Doses are consumed only when administration successfully applies an effect. An exhausted
  injector resets to its empty reusable state.
- The vaccinator accepts viable culture, optional live-vaccine catalyst, and exactly one
  empty pathogen injector. A filled injector is rejected before production starts, so two
  payloads can never be mixed.
- Production now finishes after the authored delay instead of creating output immediately
  and treating the delay as a cooldown. A second run cannot begin while one is active.
- Completed injectors are ejected onto the grid. Culture, catalyst, and injector slots also
  have explicit UI ejection controls that remain locked while the machine is active.
- Filled injector names and descriptions automatically show the strain designation,
  payload use, and remaining dose count. They return to the generic empty identity when
  exhausted.
- Live-vaccine production remains restricted to virulent cultures, consumes one viroculum
  catalyst on successful completion, and now checks line of sight while shedding immunity.
- Immunity from direct treatment, vaccination, natural recovery, and live-vaccine pulses
  remains permanent for the round. Live-vaccine spread is instead constrained to a
  1.5-tile unobstructed radius with a 3% roll per eligible person every ten seconds; the
  carrier remains active for ten minutes.
- Added viroculum seeds and caps as the botany catalyst for live vaccine synthesis.
- Added ViroDrobe, cargo, medical-lathe, locker, and flatpack distribution so the complete
  virology workflow does not depend on map edits.
- Empty injectors are stocked in the ViroDrobe and admin test kit and are printable from
  the medical lathe. Obsolete antipathogen reagent data and treatment-vessel prototypes
  were removed.

## Reconstructed Git History

The previously uncommitted virology work was split into dependency-ordered commits without
mixing in the already-pushed casino fork:

1. `c4fc73ebe2` Add pathogen runtime and symptom progression
2. `72235b09b1` Seed contamination-driven pathogen outbreaks
3. `25c3afe6ed` Implement pathogen transmission and PPE resistance
4. `1c0ebc7922` Map and clean pathogen contamination sources
5. `53fd437ed9` Add pathogen detection and diagnosis workflow
6. `6a5c6610f4` Add deterministic virology test controls
7. `5e496ae17f` Add strain-specific treatment production
8. `924edbfbcd` Distribute virology equipment without map edits

History reconstruction verification:

- Server and client builds completed with 0 errors. Only existing dependency warnings remain.
- `Content.YAMLLinter` completed with 0 errors.
- Prototype save/load tests pass 2/2.
- The complete virology filter passes 26 tests with the known pooled spread test skipped;
  that spread regression has passed independently.
- `git diff --check` passes, and changed-path scans found no casino, Gamorrah, treasurer,
  or brigmed content.
- Compiler workers started by verification were shut down. The remaining running server is
  from the separate `ss14-paperwork` checkout and was left untouched.

## Next Phase

1. Manually verify that loose organic trash contributes 0.1 each, contained trash
   disappears from the next snapshot, and disposal-processed organic trash remains
   excluded after reaching the disposal room. Also verify food/mold spills, dead plants,
   and viral carriers.
2. Manually verify the analyser interaction/report layout and the monitor's contamination
   bar, map framing, room list, live refresh, and suit-sensor filtering in a live client.
3. Tune treatment production costs and batch sizes after live testing the permanent,
   low-probability live-vaccine spread.
4. Design beneficial strain content and balance separately; its injector route is ready,
   but beneficial effects remain outside this phase.
5. Revisit symptom composition, stage-3 distinction, jitter scaling, and the safe fast-test
   interval after the tool presentation pass.

## End-of-Phase Checklist

Run this after every coding phase:

- Run `Content.YAMLLinter` first when prototypes or YAML-backed content are involved.
- Build and run the narrowest relevant automated tests, then the complete virology suite
  before opening a live client.
- Review errors and warnings caused by the phase.
- Inspect `git status` and the phase diff; preserve unrelated user changes.
- Search edited files for temporary commands, debug code, and accidental artifacts.
- Stop only command-line processes known to have been started during the phase.
- Update this file with completed work, verification results, and the next phase.
