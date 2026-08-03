# Suit-sensor detector
pathogen-detector-window-title = Virology Detection
pathogen-detector-infections-heading = Detected infections
pathogen-detector-contamination-heading = Local contamination
pathogen-detector-no-infections = No qualifying suit sensor reports an infection.
pathogen-detector-entry = {$name}: {$detection}
pathogen-detector-unidentified = Unidentified pathogen detected
pathogen-detector-identified = {$designation} detected
pathogen-detector-contamination-none = No active contamination source detected.
pathogen-detector-contamination = {$type} source: {$distance} m {$direction}.
pathogen-detector-contamination-located = {$type} source near {$beacon}: {$distance} m {$direction}.

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
