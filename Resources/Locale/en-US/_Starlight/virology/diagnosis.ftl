# Handheld virology monitor
pathogen-monitor-window-title = Virology Monitor
pathogen-monitor-station-unknown = Unknown station grid
pathogen-monitor-contamination-heading = Station contamination
pathogen-monitor-contamination-value = {$level}/100
pathogen-monitor-signatures = Viral {$virus}  |  Bacterial {$bacteria}  |  Fungal {$fungus}
pathogen-monitor-rooms-heading = Contaminated areas
pathogen-monitor-no-contaminated-rooms = No contaminated beacon areas.
pathogen-monitor-room-entry = {$room}: {$level}/100
pathogen-monitor-sick-crew-heading = Sick crew
pathogen-monitor-no-sick-crew = No qualifying suit sensor reports sickness.

# Direct pathogen analyser
pathogen-analyzer-window-title = Pathogen Analysis
pathogen-analyzer-target-patient = Crew patient
pathogen-analyzer-target-contaminationsource = Contamination source
pathogen-analyzer-target-culture = Biological culture
pathogen-analyzer-target-injector = Configured injector
pathogen-analyzer-none = No pathogen detected in this target.
pathogen-analyzer-unidentified = Unidentified pathogen detected
pathogen-analyzer-incomplete = Complete diagnosis before detailed strain information can be displayed.
pathogen-analyzer-context-patient = Current infection stage: {$stage}/{$maxStage}.
pathogen-analyzer-context-source = Active environmental reservoir.
pathogen-analyzer-context-viable-culture = Reusable viable culture.
pathogen-analyzer-context-analysable-culture = Centrifuged culture ready for diagnosis.
pathogen-analyzer-context-unprepared-culture = Unprepared specimen culture.
pathogen-analyzer-context-injector = {$mode}; {$doses}/{$capacity} doses remain.
pathogen-analyzer-injector-treatment = Treatment payload
pathogen-analyzer-injector-live = Live-vaccine payload
pathogen-analyzer-injector-beneficial = Beneficial culture payload
pathogen-analyzer-injector-empty = Empty payload
pathogen-analyzer-field-classification = Classification
pathogen-analyzer-field-tier = Severity
pathogen-analyzer-field-origin = Origin
pathogen-analyzer-field-symptoms = Symptoms
pathogen-analyzer-field-incubation = Incubation
pathogen-analyzer-field-duration = Duration
pathogen-analyzer-field-transmissibility = Transmissibility
pathogen-analyzer-field-bypass = PPE bypass
pathogen-analyzer-field-prevalence = Prevalence cap
pathogen-tier-ambient = AMBIENT
pathogen-tier-emergent = EMERGENT
pathogen-tier-virulent = VIRULENT

# Sampling and preparation
pathogen-swab-filled-name = filled pathogen swab
pathogen-swab-no-sample = No viable pathogen specimen can be collected from that.
pathogen-swab-collected = You collect an anonymous pathogen specimen.
pathogen-swab-requires-empty-vial = Transfer the specimen into an empty mini vial.
pathogen-swab-transferred = The swab is consumed, leaving the anonymous specimen in the vial.
pathogen-specimen-vial-name = pathogen specimen vial
pathogen-culture-vial-name = analysable pathogen culture
pathogen-machine-no-power = The machine has no power.
pathogen-centrifuge-needs-water = The specimen needs water before centrifuging.
pathogen-centrifuge-already-processed = That culture is already ready for analysis.
pathogen-centrifuge-full = The pathogen culture batch is full.
pathogen-centrifuge-inserted = Specimen added. Batch contains {$count}/{$capacity} vials.
pathogen-centrifuge-complete = Centrifuging complete. {$count} cultures are ready.

# Diagnosis
pathogen-diagnoser-not-ready = This specimen has not been prepared and centrifuged.
pathogen-diagnoser-invalid = The diagnoser cannot resolve this specimen.
pathogen-diagnoser-duplicate-host = Specimen matches a previously analysed host. A different host is required.
pathogen-diagnoser-partial = Initial analysis complete. A report has been printed.
pathogen-diagnoser-complete = Identification complete. A report and viable culture have been produced.
pathogen-diagnosis-insufficient = INSUFFICIENT DATA
pathogen-classification-virus = VIRAL
pathogen-classification-bacteria = BACTERIAL
pathogen-classification-fungus = FUNGAL
pathogen-origin-natural = NATURAL
pathogen-origin-engineered = ENGINEERED
pathogen-diagnosis-incomplete =
    Analysis incomplete. A specimen from a second host is required.
pathogen-diagnosis-complete =
    Analysis complete. Cure production is unlocked.
pathogen-diagnosis-report =
    DIAGNOSTIC REPORT - {$designation}

    Classification ....... {$classification}
    Symptoms ............. {$symptoms}
    Incubation ........... {$incubation}
    Duration ............. {$duration}
    Transmissibility ..... {$transmissibility}
    Origin ............... {$origin}

    {$conclusion}
pathogen-viable-culture-designated-name = viable culture ({$designation})
