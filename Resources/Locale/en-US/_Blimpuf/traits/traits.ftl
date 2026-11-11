trait-dipsomania-name = Dipsomania
trait-dipsomania-desc = Your body relies on alcohol for sustenance, while drinking water makes you vomit.

blimpuf-trait-neuroaversion-name = Neuroaversion
blimpuf-trait-neuroaversion-desc = Mindshield implants interfere with your nervous system, causing recurring migraines and occasional seizures.
blimpuf-neuroaversion-migraine-self = A sharp ache spreads through your head around the mindshield implant.
blimpuf-neuroaversion-migraine-others = {CAPITALIZE(THE($target))} presses a hand to {POSS-ADJ($target)} temple in discomfort.
blimpuf-seizure-prodrome-self = Your balance falters as an unsettling sensation builds.
blimpuf-seizure-prodrome-others = {CAPITALIZE(THE($target))} sways unsteadily.
blimpuf-seizure-self = Your muscles seize up and you lose control of your body!
blimpuf-seizure-others = {CAPITALIZE(THE($target))} collapses into convulsions!
blimpuf-seizure-end-self = The convulsions subside, leaving you weak and unsteady.
blimpuf-seizure-end-others = {CAPITALIZE(THE($target))}'s convulsions ease.
blimpuf-reagent-effect-reduce-seizure-build = { $chance ->
    [1] Reduces
    *[other] May reduce
    } seizure buildup associated with neuroaversion.
