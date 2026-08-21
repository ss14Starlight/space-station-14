entity-effect-guidebook-modify-reagent-from-metabolites =
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
        } {$amount}u of {$reagent} { $deltasign ->
            [1] to
            *[-1] from
        } the metabolites solution