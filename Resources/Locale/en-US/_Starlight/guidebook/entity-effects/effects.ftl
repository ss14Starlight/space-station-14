entity-effect-guidebook-modify-solution-reagent =
    { $chance ->
        [1] { $deltasign ->
                [1] Adds
                *[-1] Removes
            }
        *[other]
            { $deltasign ->
                [1] add
                *[-1] remove
            }
    } {NATURALFIXED($amount, 2)}u of {$reagent} { $deltasign ->
        [1] to
        *[-1] from
    } the {$solution} solution
