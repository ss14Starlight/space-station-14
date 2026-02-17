entity-condition-guidebook-unknown-reagent = an unknown reagent

entity-condition-guidebook-blood-reagent-threshold =
    { $max ->
        [2147483648] the bloodstream has at least {NATURALFIXED($min, 2)}u of {$reagent}
        *[other] { $min ->
                    [0] the bloodstream has at most {NATURALFIXED($max, 2)}u of {$reagent}
                    *[other] the bloodstream has between {NATURALFIXED($min, 2)}u and {NATURALFIXED($max, 2)}u of {$reagent}
                 }
    }
