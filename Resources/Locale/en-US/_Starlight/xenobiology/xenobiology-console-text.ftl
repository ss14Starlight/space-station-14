# The selectors in the case of 1 just don't work for some reason.
# Guess we're always going for plural?

xenobiology-console-monkey-cube-inserted = Thanks for inserting a monkey cube! The console now has {$cubes} {$cubes ->
    [1] cube
    *[other] cubes
    }.

xenobiology-console-mutation-potion-inserted = Thanks for inserting a mutation potion! The console now has {$potions} {$potions ->
    [1] potion
    *[other] potions
    }.

xenobiology-console-stabilizer-potion-inserted = Thanks for inserting a stabilizer potion! The console now has {$potions} {$potions ->
    [1] potion
    *[other] potions
    }.

xenobiology-console-slime-picked-up = Picked up {$name}.
xenobiology-console-slime-picked-up-fail-full = Could not pick up {$name}. Try dropping some slimes.
xenobiology-console-slime-picked-up-fail-none-found = No slimes found. Try moving closer to one.

xenobiology-console-slime-placed-down = Placed down {$name}.
xenobiology-console-slime-placed-down-fail-none-stored = No slimes stored. Try picking up one.

xenobiology-console-monkey-placed = Placed down a monkey. You now have {$cubes} {$cubes ->
    [1] cube
    *[other] cubes
    }.
xenobiology-console-monkey-placed-fail-empty = Not enough monkey cubes stored ({$cubes}). Try inserting one, or recycling some already eaten monkeys.

xenobiology-console-monkey-recycled = Recycled {$monkeys} {$monkeys ->
    [1] monkey
    *[other] monkeys
    }. You now have {$cubes} {$cubes ->
    [1] cube
    *[other] cubes
    }.
xenobiology-console-monkey-recycled-failed-none = No monkeys were found to recycle. Try getting closer or making sure they are damaged enough.

xenobiology-console-mutation-potion-applied = Applied a mutation potion to {$name}. It now has a mutation chance of {$chance}.
xenobiology-console-mutation-potion-applied-failed-empty = No mutation potions stored. Try inserting one.

xenobiology-console-stabilizer-potion-applied = Applied a stabilizer potion to {$name}. It now has a mutation chance of {$chance}.
xenobiology-console-stabilizer-potion-applied-failed-empty = No stabilizer potions stored. Try inserting one.
