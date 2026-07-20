id-card-access-level-virology = Virology

job-description-virologist = Research, contain, and treat infectious diseases. Maintain sample custody and outbreak protocol.

sol-pathogen-flu-name = Station Flu
sol-pathogen-flu-desc = A contagious respiratory illness causing fever, coughing, and sneezing.
sol-pathogen-sepsis-name = Wound Sepsis
sol-pathogen-sepsis-desc = A surgical and contact infection that can progress to organ stress.
sol-pathogen-bioagent-name = Engineered Bioagent
sol-pathogen-bioagent-desc = A virulent engineered pathogen used in bioterror scenarios.
sol-pathogen-bacterial-pneumonia-name = Bacterial Pneumonia
sol-pathogen-bacterial-pneumonia-desc = An airborne bacterial lung infection treated with antibiotics.
sol-pathogen-enteric-fever-name = Enteric Fever
sol-pathogen-enteric-fever-desc = A food-borne bacterial fever that stresses the gut and liver. Treated with antibiotics.
sol-pathogen-hemorrhagic-virus-name = Hemorrhagic Virus
sol-pathogen-hemorrhagic-virus-desc = A contact viral pathogen causing fever, bleeding, and organ stress. Requires specialized antiviral treatment.
sol-pathogen-neuroviral-encephalitis-name = Neuroviral Encephalitis
sol-pathogen-neuroviral-encephalitis-desc = A slow-onset viral infection of the brain. Requires specialized antiviral treatment.

sol-pathogen-unknown-description = An unidentified pathogen.
sol-pathogen-custom-base-name = Custom synthesis base
sol-pathogen-custom-base-desc = Internal binder used for gene-only custom strain assembly. Not a natural disease.
sol-pathogen-exposed = You feel a chill. Something may have gotten into your system...
sol-pathogen-symptoms-start = You start feeling the effects of {$disease}.
sol-pathogen-recovered = You seem to have recovered from {$disease}.

sol-swab-already-used = This swab has already been used.
sol-swab-collected = You collect a sample from {$target}.
sol-diagnoser-report-negative = RESULT: No pathogen detected.
sol-diagnoser-report-positive = RESULT: {$disease} detected.
    Stage: {$stage}
    Dose: {$dose}
    Blood sample: {$blood}
sol-diagnoser-report-inconclusive = RESULT: Inconclusive.
sol-diagnoser-report-name = Disease Diagnosis Report
sol-diagnoser-started = The diagnoser begins analyzing the sample.
sol-diagnoser-complete = Diagnosis complete. Report printed.
sol-vaccinator-need-sample = Insert a valid pathogen sample.
sol-vaccinator-bad-sample = Sample quality insufficient for vaccine production.
sol-vaccinator-started = The vaccinator begins synthesizing a vaccine.
sol-vaccinator-produced = Vaccine synthesized.
sol-disease-machine-busy = The machine is already processing a sample.
sol-vaccine-failed = Vaccination failed. Patient may already be symptomatic.
sol-vaccine-applied = You vaccinate {$target}.
sol-sample-swab = Contains a mucosal/surface sample.
sol-sample-blood = Contains an unprocessed blood sample.
sol-sample-blood-centrifuged = Contains an electrolyzed blood panel.

sol-blood-vial-full = This vial already contains a sample.
sol-blood-drawn = You draw blood from {$target}.
sol-blood-panel-not-ready = Blood panel not electrolyzed. Run the vial through an electrolysis unit.
sol-blood-panel-ready = The blood panel finishes processing.
sol-blood-panel-pathogen-negative = Blood panel: no pathogen markers detected.
sol-blood-panel-inconclusive = Blood panel: inconclusive.
sol-blood-panel-full = Blood panel:
    Pathogen: {$disease}
    Stage: {$stage}
    Dose: {$dose}
    Antibodies: {$immunity}
    Organ function: {$organs}
sol-blood-panel-organs-unknown = Unknown (source unavailable)
sol-blood-panel-organs-none = No organs detected
sol-blood-panel-organs-summary = damaged={$damaged}, failing={$failing}, missing={$missing}
sol-ppe-safe-doff = You carefully remove the contaminated PPE while gloved.
sol-ppe-unsafe-doff = Contaminated PPE contacts your bare skin as you remove it!

sol-surgery-tool-sterile = [color=green]Sterile[/color]
sol-surgery-tool-sterile-uses = [color=green]Sterile[/color] ({$uses} clean uses left)
sol-surgery-tool-disinfected = [color=yellow]Disinfected[/color]
sol-surgery-tool-dirty = [color=red]Dirty[/color]
sol-surgery-tool-contaminated = [color=orange]Pathogen contamination detected.[/color]
sol-surgery-tool-wash-verb = Wash tool
sol-surgery-tool-washed = You wash the tool. It is cleaner, but not fully sterile.
sol-surgery-tool-sterilized = The tool is now sterile.
sol-surgery-tool-disinfected-popup = The tool has been disinfected.
sol-surgery-dirty-tool-warning = Your surgical tools are dirty! Sterilize them before the next patient.
sol-surgery-failed-infection-risk = The failed step may have contaminated the wound.
sol-surgery-inspect-hygiene-verb = Inspect hygiene
sol-surgery-hygiene-status = Body: { $body ->
    [contaminated] [color=orange]pathogen contamination[/color]
    [dirty] [color=yellow]dirty[/color]
   *[clean] [color=green]clean[/color]
}. Gloves: { $gloves ->
    [none] none
    [sterile] [color=green]sterile[/color]
    [disinfected] [color=yellow]disinfected[/color]
    [dirty] [color=red]dirty[/color]
   *[clean] clean
}. Surgical mask: { $masked ->
    [true] worn
   *[false] not worn
}
sol-surgery-window-title = Surgery
sol-surgery-window-title-part = Surgery - {$part}
sol-surgery-no-actions = No available surgical actions for this part.
sol-surgery-action-prerequisite = Prerequisite: {$procedure} (for {$goal})
sol-surgery-action-dirty-tools = Dirty tools — infection risk elevated
sol-surgery-action-dirty-tools-sterility = Dirty tools — not sterile
sol-surgery-dirty-confirm = Your tools are not sterile. Proceed with [bold]{$surgery}[/bold] — {$step}?
sol-surgery-dirty-confirm-infection = Your tools are not sterile — infection risk elevated. Proceed with [bold]{$surgery}[/bold] — {$step}?
sol-surgery-asepsis-banner = Tools: [color=green]{$sterile} sterile[/color], [color=orange]{$dirty} dirty[/color] · Mask: { $masked ->
    [true] [color=green]on[/color]
    *[false] [color=red]off[/color]
}
sol-surgery-tool-banner = Tools: [color=green]{$sterile} sterile[/color], [color=orange]{$dirty} dirty[/color]
sol-surgery-must-lie-down = [color=red][font size=16]They need to be lying down![/font][/color]
sol-surgery-needs-table = Needs operating table
sol-surgery-remove-armor = Remove their armor!
sol-surgery-missing-tool = Missing tool
sol-surgery-disabled-tool = Disabled tool
sol-surgery-item-too-high = Item too high
sol-surgery-missing-reagent = Missing reagent
sol-surgery-missing-limb = Can't attach as limb
sol-surgery-cannot-perform = Cannot perform this step
sol-surgery-dirty-tool-before-step = Operating with non-sterile tools is unsafe.

sol-sterilizer-unpowered = The sterilization chamber controller is unpowered.
sol-sterilizer-doors-open = Close the entrance airlock to begin sterilization.
sol-sterilizer-started = Sterilization cycle started.
sol-sterilizer-interrupted = Sterilization cycle interrupted. Contaminants remain.
sol-sterilizer-complete = Sterilization cycle complete.
sol-sterilizer-not-linked = Sterilization chamber controller is not linked to two airlocks.
sol-sterilizer-invalid-geometry = Sterilization chamber geometry is invalid. Link two cardinally aligned airlocks with the controller between them.
sol-sterilizer-quarantine-lock-failed = The outer chamber airlock could not change its bolt state.

signal-port-name-sterilizer-door-a = Sterilizer door A
signal-port-description-sterilizer-door-a = Door status input for chamber airlock A.
signal-port-name-sterilizer-door-b = Sterilizer door B
signal-port-description-sterilizer-door-b = Door status input for chamber airlock B.
signal-port-name-sterilizer-quarantine-lock = Quarantine lock
signal-port-description-sterilizer-quarantine-lock = Bolts the configured outer chamber airlock while HIGH and unbolts it while LOW.

ent-SolSterilizationAirlockController = sterilization chamber controller
ent-SolSterilizationAirlockController-desc = A floor vent that runs an automatic close-fog-sterilize cycle between two linked virology airlocks.
ent-SolSterilizationFog = sterilization fog
ent-SolSterilizationFog-desc = Harmless sterilant fog filling a sealed chamber.

sol-health-analyzer-organs-header = Organs
sol-health-analyzer-organ-line = {$organ}: {$status}
sol-health-analyzer-damage-tab = Damage
sol-health-analyzer-organs-tab = Organs
sol-health-analyzer-allergies-tab = Allergies
sol-health-analyzer-no-organs = No organs detected.
sol-health-analyzer-no-known-allergies = No known allergies
sol-health-analyzer-allergies-warning = !!! ALLERGIES !!!
sol-health-analyzer-allergies-header = Allergies
sol-health-analyzer-allergy-line = - {$allergy}
sol-health-analyzer-debug-header = DEBUG VIROLOGY

sol-allergy-unknown-description = An unidentified allergy.
sol-allergy-peanut-name = Peanut Allergy
sol-allergy-peanut-desc = Reaction to peanut products.
sol-allergy-dairy-name = Dairy Allergy
sol-allergy-dairy-desc = Reaction to milk, cream, butter, cheese, and dairy ice cream.
sol-allergy-egg-name = Egg Allergy
sol-allergy-egg-desc = Reaction to eggs, mayonnaise, and foods prepared with egg.
sol-allergy-wheat-name = Wheat Allergy
sol-allergy-wheat-desc = Reaction to flour, bread, cakes, pies, donuts, and other wheat products.
sol-allergy-soy-name = Soy Allergy
sol-allergy-soy-desc = Reaction to soy milk, tofu, soy sauce, and soy foods.
sol-allergy-fish-name = Fish Allergy
sol-allergy-fish-desc = Reaction to carp and other fish products.
sol-allergy-shellfish-name = Shellfish Allergy
sol-allergy-shellfish-desc = Reaction to crab and other crustacean products.
sol-allergy-tree-nut-name = Tree Nut Allergy
sol-allergy-tree-nut-desc = Reaction to pistachios, almonds, and tree-nut products.
sol-allergy-latex-name = Latex Allergy
sol-allergy-latex-desc = Reaction to latex exposure. Food ingestion does not trigger this allergy.
sol-allergy-amoxla-name = Amoxla Allergy
sol-allergy-amoxla-desc = Reaction to the antibiotic amoxla.
sol-allergy-dylovene-name = Dylovene Allergy
sol-allergy-dylovene-desc = Reaction to dylovene.
sol-allergy-inaprovaline-name = Inaprovaline Allergy
sol-allergy-inaprovaline-desc = Reaction to inaprovaline.
sol-allergy-epinephrine-name = Epinephrine Allergy
sol-allergy-epinephrine-desc = Reaction to epinephrine-based medication.
sol-allergy-diphenhydramine-name = Antihistamine Allergy
sol-allergy-diphenhydramine-desc = Reaction to diphenhydramine-based antihistamines.
sol-allergy-cryoxadone-name = Cryoxadone Allergy
sol-allergy-cryoxadone-desc = Reaction to cryoxadone.
sol-allergy-antiviral-name = Antiviral Allergy
sol-allergy-antiviral-desc = Reaction to Sol antiviral medication.
sol-allergy-ceftriaxone-name = Ceftriaxone Allergy
sol-allergy-ceftriaxone-desc = Reaction to ceftriaxone antibiotic.
sol-allergy-ribavirin-name = Ribavirin Allergy
sol-allergy-ribavirin-desc = Reaction to ribavirin specialized antiviral.
sol-allergy-saline-name = Saline Contraindication
sol-allergy-saline-desc = Species-specific adverse reaction to saline.
sol-allergy-dexalin-name = Dexalin Contraindication
sol-allergy-dexalin-desc = Species-specific adverse reaction to dexalin.
sol-allergy-dexalin-plus-name = Dexalin Plus Contraindication
sol-allergy-dexalin-plus-desc = Species-specific adverse reaction to dexalin plus.
sol-allergy-symptoms-mild = Your skin itches and your nose begins to run.
sol-allergy-symptoms-moderate = You feel nauseated as your skin swells and breaks out in hives.
sol-allergy-symptoms-severe = Your throat tightens and every breath becomes difficult! You struggle to speak!
sol-allergy-symptoms-anaphylaxis = Your airway is rapidly closing! You can't get words out and can barely breathe!
sol-allergy-taste-append = , but you're allergic to {$allergy}!

alerts-sol-allergic-choking-name = [color=red]Allergic Reaction[/color]
alerts-sol-allergic-choking-desc = Your airway is swelling shut. Seek [color=green]epinephrine[/color] or [color=green]antihistamine[/color] treatment.

entity-effect-guidebook-shorten-allergy-reaction = shortens an active allergic reaction by about {NATURALFIXED($seconds, 1)} seconds{ $chance ->
    [1] {""}
    *[other] {" "}with a {$chance} chance
}

sol-bioterror-briefing = You are a bioterrorist. Establish a clandestine lab, culture environmental microbes into a custom strain, and deploy physical payloads. Avoid early detection.
sol-bioterror-briefing-head = You are the Head Bioterrorist. You carry the portable lab flatpacks. Lead the cell, choose a hideout, and coordinate synthesis and deployment.
sol-bioterror-briefing-member = You are a Bioterrorist cell member. Help establish the lab, scrape environmental microbes, culture traits, and deploy manufactured payloads.
sol-bioterror-contaminate-food = Contaminate with pathogen
sol-bioterror-release-airborne = Release airborne pathogen
sol-bioterror-deploy-aerosol = Release aerosol culture
sol-bioterror-food-contaminated = You contaminate the target.
sol-bioterror-surface-contaminated = You apply culture to the surface.
sol-bioterror-airborne-released = You release an airborne pathogen load.
sol-bioterror-no-station = Virology systems are not active here.
sol-bioterror-payload-invalid = This culture is inert or unrecognized.
sol-bioterror-scrape-invalid = There is nothing useful to scrape here.
sol-bioterror-scrape-depleted = This source has been scraped clean for now.
sol-bioterror-scrape-success = You collect an environmental microbial sample.
sol-bioterror-sample-name = microbial sample ({$source})
sol-bioterror-sample-analyzed-name = analyzed sample ({$source})
sol-bioterror-sample-examine-raw = It still needs analysis before incubation.
sol-bioterror-sample-examine-analyzed = { $contaminated ->
    [true] Analyzed: {$genetics}, quality {$quality}, contaminated
    *[false] Analyzed: {$genetics}, quality {$quality}
}
sol-bioterror-sample-genetics-none = no identifiable genes
sol-bioterror-sample-genetics = possible genetics: {$genes}
sol-bioterror-lab-unpowered = The machine has no power.
sol-bioterror-analyzer-started = Sample inserted. Analysis started.
sol-bioterror-analyzer-complete = Analysis complete. Click the analyzer to retrieve the sample.
sol-bioterror-analyzer-retrieved = You retrieve the analyzed sample.
sol-bioterror-analyzer-busy = The analyzer is busy.
sol-bioterror-analyzer-eject-first = Retrieve the finished sample first.
sol-bioterror-analyzer-already = This sample has already been analyzed.
sol-bioterror-analyzer-examine = Accepts environmental scrapings for genetic analysis.
sol-bioterror-analyzer-examine-running = Analysis in progress.
sol-bioterror-analyzer-examine-ready = An analyzed sample is ready for retrieval.
sol-bioterror-incubator-full = The incubator chamber is full.
sol-bioterror-incubator-need-analyzed = Insert an analyzed microbial sample.
sol-bioterror-incubator-busy = The incubator is busy.
sol-bioterror-incubator-need-nutrient = Add culture nutrient to the machine tank.
sol-bioterror-incubator-no-viable = None of the loaded samples contain usable genes.
sol-bioterror-incubator-started = Culture cycle started.
sol-bioterror-incubator-complete = Cultures ready. Retrieve them from the incubator.
sol-bioterror-incubator-retrieved = You retrieve the finished cultures.
sol-bioterror-incubator-empty-output = The incubator chamber is empty.
sol-bioterror-incubator-spoiled = Power loss spoiled the culture batch.
sol-bioterror-incubator-overgrown = An unattended culture batch overgrew and contaminated the area!
sol-bioterror-incubator-examine = Load up to six analyzed samples, then start a batch from the interface.
sol-bioterror-incubator-examine-running = Culture cycle in progress.
sol-bioterror-incubator-examine-ready = Finished cultures are ready for retrieval.

sol-culture-incubator-ui-title = Culture incubator
sol-culture-incubator-ui-nutrient = Nutrient: {$amount} / {$max} u {$reagent}
sol-culture-incubator-ui-cost = Batch cost: {$cost} u ({$count} / {$max} samples)
sol-culture-incubator-ui-samples-label = Chamber samples
sol-culture-incubator-ui-samples-empty = No samples loaded.
sol-culture-incubator-ui-sample-entry = { $contaminated ->
    [true] {$label} — {$detail}, quality {$quality} (contaminated)
    *[false] {$label} — {$detail}, quality {$quality}
}
sol-culture-incubator-ui-progress = Incubating… {$remaining} remaining ({$percent}%)
sol-culture-incubator-ui-ready = Cultures ready for retrieval.
sol-culture-incubator-ui-start = Start cycle
sol-culture-incubator-ui-retrieve = Retrieve cultures
sol-culture-incubator-ui-eject = Eject
sol-culture-incubator-ui-eject-all = Eject all samples

sol-bioterror-synth-need-culture = Insert cellular substrate or gene cultures into the synthesizer.
sol-bioterror-synth-need-substrate = Load cellular substrate into the substrate slot.
sol-bioterror-synth-need-chassis = Load cellular substrate into the substrate slot.
sol-bioterror-synth-need-stabilizer = Add culture stabilizer to the machine tank.
sol-bioterror-synth-substrate-blocked = Could not load that culture as cellular substrate.
sol-bioterror-synth-error-bad-chassis = Cellular substrate is missing or invalid.
sol-bioterror-synth-error-multi-chassis = Only one cellular substrate culture can be loaded at a time.
sol-bioterror-synth-error-duplicate-gene = Duplicate gene in recipe: {$gene}
sol-bioterror-synth-invalid = Synthesis rejected: {$error}
sol-bioterror-synth-started = Synthesis cycle started.
sol-bioterror-synth-complete = Strain {$strain} synthesized. Ampoules dispensed.
sol-bioterror-synth-failed = Synthesis failed catastrophically!
sol-bioterror-synth-spoiled = Power loss ruined the synthesis batch.
sol-bioterror-synth-busy = The synthesizer is busy.
sol-bioterror-synth-examine = Load cellular substrate and select genes, then start synthesis from the interface.
sol-bioterror-synth-examine-running = Synthesis cycle in progress.

sol-bioterror-culture-substrate-name = cellular substrate
sol-bioterror-culture-substrate-detail = cellular substrate
sol-bioterror-culture-gene-name = {$gene}
sol-bioterror-culture-gene-stack = {$gene} ×{$count}

sol-pathogen-synth-error-unknown-trait = Unknown gene: {$trait}
sol-pathogen-synth-error-duplicate-trait = Duplicate gene: {$trait}
sol-pathogen-synth-error-incompatible = {$trait} is incompatible with {$other}
sol-pathogen-synth-error-budget = Gene load {$used} exceeds capacity {$max}

sol-pathogen-synth-ui-title = Pathogen synthesizer
sol-pathogen-synth-ui-substrate-header = Cellular substrate
sol-pathogen-synth-ui-substrate-slot = Cellular substrate
sol-pathogen-synth-ui-substrate-empty = No cellular substrate loaded
sol-pathogen-synth-ui-substrate-loaded = {$name}
sol-pathogen-synth-ui-substrate-insert = Insert
sol-pathogen-synth-ui-substrate-eject = Eject
sol-pathogen-synth-ui-stabilizer = Stabilizer: {$amount} / {$max} u {$reagent} (needs {$needed} u)
sol-pathogen-synth-ui-budget = Gene budget: {$used} / {$max}
sol-pathogen-synth-ui-time = Estimated cycle: {$seconds} s
sol-pathogen-synth-ui-genes-header = Gene storage
sol-pathogen-synth-ui-genes-hint = Click genes to add or remove them from the recipe. Matching genes stack. Unselected genes stay stored. Open Strain forecast below for the projected outcome.
sol-pathogen-synth-ui-genes-empty = No genes stored. Click gene cultures onto the machine.
sol-pathogen-synth-ui-gene-button = { $selected ->
    [true] [+] {$label} ({$cost})
    *[false] {$label} ({$cost})
}
sol-pathogen-synth-ui-progress = Synthesizing… {$remaining} remaining ({$percent}%)
sol-pathogen-synth-ui-start = Start synthesis
sol-pathogen-synth-ui-clear = Clear selection
sol-pathogen-synth-ui-forecast-title = Strain forecast
sol-pathogen-synth-ui-forecast-header = Strain forecast
sol-pathogen-synth-ui-forecast-empty = Select genes to preview the assembled strain.
sol-pathogen-synth-ui-forecast-transmission = Transmission: {$value}
sol-pathogen-synth-ui-forecast-stages = Flow: incubate {$incubation} → symptoms {$symptomatic} → critical {$critical} → recover {$recovery}
sol-pathogen-synth-ui-forecast-symptoms = Symptoms: {$value}
sol-pathogen-synth-ui-forecast-organs = Organ targets: {$value}
sol-pathogen-synth-ui-forecast-treatments = Treatment susceptibility: {$value}
sol-pathogen-synth-ui-forecast-stats = {$infectivity}. {$lethality}. {$sterilant}.
sol-pathogen-synth-ui-route-none = none (add transmission genes)
sol-pathogen-synth-ui-route-contact = contact
sol-pathogen-synth-ui-route-airborne = airborne
sol-pathogen-synth-ui-route-ingestion = ingestion
sol-pathogen-synth-ui-route-fluid = fluid
sol-pathogen-synth-ui-symptom-none = none notable
sol-pathogen-synth-ui-symptom-cough = coughing
sol-pathogen-synth-ui-symptom-sneeze = sneezing
sol-pathogen-synth-ui-symptom-fever = fever
sol-pathogen-synth-ui-symptom-damage = {$type} {$amount}/tick
sol-pathogen-synth-ui-organs-none = none
sol-pathogen-synth-ui-treatments-none = none (add treatment vulnerability genes)
sol-pathogen-synth-ui-infectivity-value = Infectivity {$chance}% (dose {$dose})
sol-pathogen-synth-ui-lethality-value = Lethality {$value}%
sol-pathogen-synth-ui-sterilant-value = Sterilant susceptibility ×{$value}
sol-pathogen-synth-ui-duration-minutes = {$minutes} min
sol-pathogen-synth-ui-duration-seconds = {$seconds} s
sol-bioterror-ampoule-name = culture ampoule ({$strain})
sol-bioterror-round-end-agent-name = bioterrorist
sol-bioterror-roundend-header = Bioterror cell results:
sol-bioterror-roundend-lab = Lab established off-shuttle: {$established} (analyzer={$analyzer}, incubator={$incubator}, synthesizer={$synthesizer})
sol-bioterror-roundend-strain = Synthesized strain: {$strain}
sol-bioterror-roundend-none = none
sol-bioterror-roundend-deployed = Deployed load: {$load} / {$required}
sol-bioterror-roundend-medical = Diagnosed: {$diagnosed}. Vaccine created: {$vaccine}.
sol-bioterror-roundend-survivors = Living cell members: {$count}
sol-bioterror-objective-lab-title = Establish the clandestine laboratory
sol-bioterror-objective-lab-desc = Deploy and operate all three lab machines away from the infiltration shuttle.
sol-bioterror-objective-synth-title = Synthesize a viable custom strain
sol-bioterror-objective-synth-desc = Culture environmental traits and assemble a bounded custom pathogen.
sol-bioterror-objective-deploy-title = Deploy manufactured cultures
sol-bioterror-objective-deploy-desc = Manufacture physical payloads and release a minimum infectious load.
sol-bioterror-objective-delay-title = Delay confirmed diagnosis
sol-bioterror-objective-delay-desc = Prevent a matching vaccine for as long as possible after first deployment.
sol-bioterror-objective-survive-title = Keep the cell alive
sol-bioterror-objective-survive-desc = At least one cell member must survive.

sol-pathogen-trait-unknown-description = An unidentified microbial trait.
sol-pathogen-trait-contact-name = Contact persistence
sol-pathogen-trait-contact-desc = Improves surface contact transmission.
sol-pathogen-trait-airborne-name = Airborne adaptation
sol-pathogen-trait-airborne-desc = Enables aerosol transmission.
sol-pathogen-trait-ingestion-name = Enteric adaptation
sol-pathogen-trait-ingestion-desc = Enables foodborne infection.
sol-pathogen-trait-aerosol-name = Aerosol stability
sol-pathogen-trait-aerosol-desc = Improves environmental persistence in air.
sol-pathogen-trait-cough-name = Cough shedding
sol-pathogen-trait-cough-desc = Increases cough-driven shedding. Incompatible with sneeze shedding.
sol-pathogen-trait-sneeze-name = Sneeze shedding
sol-pathogen-trait-sneeze-desc = Increases sneeze-driven shedding. Incompatible with cough shedding.
sol-pathogen-trait-dyspnea-name = Respiratory distress
sol-pathogen-trait-dyspnea-desc = Causes shortness of breath and asphyxiation stress. Incompatible with hemorrhagic expression.
sol-pathogen-trait-hemorrhage-name = Hemorrhagic expression
sol-pathogen-trait-hemorrhage-desc = Causes bleeding and toxin stress. Incompatible with respiratory distress.
sol-pathogen-trait-yield-name = Culture yield
sol-pathogen-trait-yield-desc = Speeds incubation of isolates.
sol-pathogen-trait-slow-name = Slow incubation
sol-pathogen-trait-slow-desc = Lengthens incubation and reduces detectability.
sol-pathogen-trait-growth-name = Environmental growth
sol-pathogen-trait-growth-desc = Survives longer on surfaces and waste.
sol-pathogen-trait-virulent-name = Heightened virulence
sol-pathogen-trait-virulent-desc = Increases lethality and infectivity.
sol-pathogen-trait-liver-name = Hepatic tropism
sol-pathogen-trait-liver-desc = Targets liver tissue. Mutually exclusive with other organ tropisms.
sol-pathogen-trait-lungs-name = Pulmonary tropism
sol-pathogen-trait-lungs-desc = Targets lung tissue and worsens breathing. Mutually exclusive with other organ tropisms.
sol-pathogen-trait-heart-name = Cardiac tropism
sol-pathogen-trait-heart-desc = Targets heart tissue. Mutually exclusive with other organ tropisms.
sol-pathogen-trait-persistent-name = Environmental persistence
sol-pathogen-trait-persistent-desc = Resists environmental decay.
sol-pathogen-trait-sterilant-name = Sterilant resistance
sol-pathogen-trait-sterilant-desc = Reduces sterilant effectiveness.
sol-pathogen-trait-treat-antiviral-name = Antiviral vulnerability
sol-pathogen-trait-treat-antiviral-desc = Makes the strain treatable with Sol antiviral. Incompatible with ribavirin vulnerability.
sol-pathogen-trait-treat-ribavirin-name = Ribavirin vulnerability
sol-pathogen-trait-treat-ribavirin-desc = Makes the strain treatable with ribavirin. Incompatible with antiviral vulnerability.
sol-pathogen-trait-treat-ceftriaxone-name = Ceftriaxone vulnerability
sol-pathogen-trait-treat-ceftriaxone-desc = Makes the strain treatable with ceftriaxone. Incompatible with amoxla vulnerability.
sol-pathogen-trait-treat-amoxla-name = Amoxla vulnerability
sol-pathogen-trait-treat-amoxla-desc = Makes the strain treatable with amoxla. Incompatible with ceftriaxone vulnerability.

chat-radio-bioterror = Bioterror

sol-gamemode-virology-title = Virology
sol-gamemode-virology-desc = A bioterror cell infiltrates with a portable lab, synthesizes custom strains from environmental scrapings, and deploys physical payloads. Medical must diagnose, contain, and vaccinate. Major syndicate antags are disabled; thief and selected creature threats may still appear.
sol-virology-preset-no-ready-virologist = Virology cannot start without an eligible ready player who has Virologist set to High priority on a virology-enabled station.

reagent-name-sol-antiviral = antiviral
reagent-desc-sol-antiviral = Broad-spectrum antiviral treatment for Sol pathogens.
reagent-name-sol-antipyretic = antipyretic
reagent-desc-sol-antipyretic = Reduces fever.
reagent-name-sol-sterilizine = sterilizine
reagent-desc-sol-sterilizine = Strong sterilant for tools, surfaces, and PPE.
reagent-name-sol-antihistamine = antihistamine
reagent-desc-sol-antihistamine = Reduces mild allergic reactions.
reagent-name-sol-epinephrine-allergy = allergy epinephrine
reagent-desc-sol-epinephrine-allergy = Emergency treatment for severe allergic reactions.
reagent-name-sol-ceftriaxone = ceftriaxone
reagent-desc-sol-ceftriaxone = Broadly safe antibiotic for bacterial Sol pathogens.
reagent-name-sol-ribavirin = ribavirin
reagent-desc-sol-ribavirin = Specialized antiviral for resistant Sol viral pathogens.
reagent-name-sol-culture-nutrient = culture nutrient
reagent-desc-sol-culture-nutrient = Nutrient medium used to culture environmental microbial isolates.
reagent-name-sol-culture-stabilizer = culture stabilizer
reagent-desc-sol-culture-stabilizer = Stabilizes custom pathogen synthesis batches.

guide-entry-sl-medical-sop-virologist = Virologist
guide-entry-sl-medical-sop-virology-outbreak = Virology and Outbreak Procedure
guide-entry-sl-medical-sop-allergies = Allergies
guide-entry-bioterrorists = Bioterrorists
guide-entry-bioterror-genes = Pathogen Genes

roles-antag-bioterrorist-name = Bioterrorist
roles-antag-bioterrorist-objective = Help the cell culture environmental microbes into deployable custom strains.
roles-antag-head-bioterrorist-name = Head Bioterrorist
roles-antag-head-bioterrorist-objective = Lead the cell, deploy the portable lab, and synthesize a custom pathogen.
