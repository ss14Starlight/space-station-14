contraband-tier-1 = restricted
contraband-tier-2 = heavily regulated
contraband-tier-3 = serious
contraband-tier-4 = highly illegal
contraband-tier-5 = extremely dangerous

contraband-type-syndicate = Syndicate
contraband-type-magical = magical
contraband-type-ussp = USSP
contraband-type-tsf = Trans-Solar Federation
contraband-type-cybernetic = cybernetic
contraband-type-medtak = MedTak
contraband-type-casino = Gamorrah Casino
contraband-type-cosmic = cosmic cult
contraband-type-central-command = Central Command

contraband-examine-text-tier =
    { $itemType ->
        *[item] [color={$color}]This is a piece of {$descriptor} tier {$tier} contraband.[/color]
        [reagent] [color={$color}]This is a {$descriptor} tier {$tier} contraband reagent.[/color]
    }

contraband-examine-text-tier-typed =
    { $itemType ->
        *[item] [color={$color}]This is a piece of {$descriptor} tier {$tier} {$contrabandType} contraband.[/color]
        [reagent] [color={$color}]This is a {$descriptor} tier {$tier} {$contrabandType} contraband reagent.[/color]
    }

contraband-examine-text-restricted =
    { $itemType ->
        *[item] [color=yellow]This item is restricted to {$authorizations}.[/color]
        [reagent] [color=yellow]This reagent is restricted to {$authorizations}.[/color]
    }

contraband-authorization-prescription-patients = patients with a valid prescription

contraband-examine-text-avoid-carrying-around = [color=red][italic]You probably want to avoid visibly carrying this around without a good reason.[/italic][/color]
contraband-examine-text-in-the-clear = [color=green][italic]You should be in the clear to visibly carry this around.[/italic][/color]

contraband-examinable-verb-text = Legality
contraband-examinable-verb-message = Check legality of this item.

contraband-department-plural = {$department}
contraband-job-plural = {MAKEPLURAL($job)}
