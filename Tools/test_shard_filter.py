#!/usr/bin/env python3

"""
Partitions test classes across shards for parallel CI execution.

Mode 1 - Generate all shard filters to files:
    dotnet test --list-tests ... | python3 test_shard_filter.py generate <total-shards> <output-dir>
    Writes <output-dir>/shard_0.filter .. shard_N.filter

Mode 2 - Read a pre-generated filter file:
    python3 test_shard_filter.py read <filter-file>
    Prints the filter to stdout (empty output if file is empty/missing)

Exit codes:
    0 - success
    1 - error (bad arguments or no tests discovered in generate mode)
"""

import sys
import os


# Weight multipliers for tests that are lighter than their test count suggests.
WEIGHT_OVERRIDES = {
    "AbsorbentOnRefillableTest": 0.5,
    "AbsorbentOnSmallRefillableTest": 0.5,
    "AddListRemoveObjectiveTest": 0.5,
    "AddPlayerSessionLog": 0.5,
    "AirlockBlockTest": 0.5,
    "AllCommandsHaveDescriptions": 0.5,
    "AllComponentsOneToOneDeleteTest": 0.5,
    "AllDeviceLinkSinksWorkTest": 0.5,
    "AllMapsTested": 0.5,
    "ApcChargingTest": 0.5,
    "ArmBladeActivateDeactivateTest": 0.5,
    "AssignJobsTest": 0.5,
    "BuckleInteractBuckleUnbuckleSelf": 0.5,
    "CancelTilePry": 0.5,
    "ChasmFallTest": 0.5,
    "ConstructComputer": 0.5,
    "ConstructReinforcedWindow": 0.5,
    "ConstructionGraphSpawnPrototypeValid": 0.5,
    "CraftRods": 0.5,
    "Date": 0.5,
    "DeconstructTable": 0.5,
    "Delete_CacheUpdatesOnAtmosTick": 0.5,
    "DeserializeNullTest": 0.5,
    "DisciplineValidTierPrerequesitesTest": 0.5,
    "DragDropOntoDrainTest": 0.5,
    "DuplicatePlayerIdDoesNotThrowTest": 0.5,
    "EmergencyEvacTest": 0.5,
    "FloorConstructDeconstruct": 0.5,
    "FollowerMapDeleteTest": 0.5,
    "ForceUnbuckleBuckleTest": 0.5,
    "GasSpecificHeats_Agree": 0.5,
    "GasSpreading": 0.5,
    "HumanMoveOverTest": 0.5,
    "HungerThirstIncreaseDecreaseTest": 0.5,
    "InsertAndDispenseItemTest": 0.5,
    "InteractionOutOfRangeTest": 0.5,
    "JobPreferenceTest": 0.5,
    "KillAndReviveTest": 0.5,
    "LoadTickLoad": 0.5,
    "MagazineVisualsSpritesExist": 0.5,
    "MapLoadingTest": 0.5,
    "MicrowaveRecipesFreezeTest": 0.5,
    "MultiTile_Component_InitDataCorrect": 0.5,
    "MultiTile_Spawn_CacheUpdatesOnAtmosTick": 0.5,
    "NoCargoOrderArbitrage": 0.5,
    "NoSuffocationTest": 0.5,
    "NullOutTileAtmosphereGasMixture": 0.5,
    "PoweredClosedAirlock_Pry_DoesNotOpen": 0.5,
    "ProcessingAbsoluteStandbyTest": 0.5,
    "ProcessingListAutoJoinTest": 0.5,
    "PullerSanityTest": 0.5,
    "QuerySingleLog": 0.5,
    "ReagentDataIsSerializable": 0.5,
    "Relogin": 0.5,
    "RepairReinforcedWindow": 0.5,
    "RestartTest": 0.5,
    "RestockTest": 0.5,
    "SelectionTest": 0.5,
    "ServerPrototypeSaveLoadSaveTest": 0.5,
    "SetWorkingState_AlreadyInState_NoChange": 0.5,
    "SpawnAndDeleteEntityCountTest": 0.5,
    "Spawn_ReconstructedUpdatesImmediately": 0.5,
    "SpillCorner": 0.5,
    "StackPrice": 0.5,
    "StartRoundTest": 0.5,
    "StorageSizeArbitrageTest": 0.5,
    "TakeRoleAndReturn": 0.5,
    "TestAb": 0.5,
    "TestComputerBoardHasValidComputer": 0.5,
    "TestConnect": 0.5,
    "TestDamageSpecifierOperations": 0.5,
    "TestDeleteCharacter": 0.5,
    "TestDeleteThrownItem": 0.5,
    "TestDeleteVisiting": 0.5,
    "TestDisconnectWhileEmbedded": 0.5,
    "TestDockingConfig": 0.5,
    "TestDungeonRoomPackBounds": 0.5,
    "TestFinished": 0.5,
    "TestFullBattery": 0.5,
    "TestGhostDoesNotInfiniteLoop": 0.5,
    "TestGib": 0.5,
    "TestGridGhostOnQueueDelete": 0.5,
    "TestGridJoinAtmosphere": 0.5,
    "TestLatheRecipeIngredientsFitLathe": 0.5,
    "TestLobbyPlayersValid": 0.5,
    "TestMindTransfersToOtherEntity": 0.5,
    "TestNoDemandRampdown": 0.5,
    "TestPickupDrop": 0.5,
    "TestPlayerCanGhost": 0.5,
    "TestRestockBreaksOpen": 0.5,
    "TestStartReachesValidTarget": 0.5,
    "TestSufficientSpaceForFill": 0.5,
    "TestSuicide": 0.5,
    "TestSuicideWhileDamaged": 0.5,
    "TestSupplyRamp": 0.5,
    "TestTags": 0.5,
    "ThrowItemIntoDisposalUnitTest": 0.5,
    "TryAddTwoNonReactiveReagent": 0.5,
    "TryAllTest": 0.5,
    "ValidateJobPrototypes": 0.5,
    "WeightlessStatusTest": 0.5,
    "WindowOnGrille": 0.5,
    "XenoArtifactGenerateSegmentsTest": 0.5,
}


def parse_tests(lines):
    """Parse test names from `dotnet test --list-tests` output."""
    tests = []
    in_list = False
    for line in lines:
        stripped = line.strip()
        if "The following Tests are available:" in stripped:
            in_list = True
            continue
        if in_list and stripped:
            tests.append(stripped)
    return tests


def extract_classes(tests):
    """Extract unique test fixture (class) names with test counts from FQN test names."""
    counts = {}
    for test in tests:
        name = test.split("(")[0].strip()
        dot = name.rfind(".")
        cls = name[:dot] if dot > 0 else name
        counts[cls] = counts.get(cls, 0) + 1
    return counts


def build_filter(classes):
    """Build a --filter expression from class names."""
    if not classes:
        return ""
    return " | ".join(f"FullyQualifiedName~{cls}" for cls in sorted(classes))


def cmd_generate():
    if len(sys.argv) != 4:
        print(f"Usage: {sys.argv[0]} generate <total-shards> <output-dir>", file=sys.stderr)
        sys.exit(1)

    total = int(sys.argv[2])
    output_dir = sys.argv[3]

    lines = sys.stdin.read().splitlines()
    tests = parse_tests(lines)

    if not tests:
        print("Error: no tests discovered from input", file=sys.stderr)
        sys.exit(1)

    class_counts = extract_classes(tests)
    print(f"Discovered {len(tests)} tests in {len(class_counts)} classes, distributing across {total} shards", file=sys.stderr)

    os.makedirs(output_dir, exist_ok=True)

    # Compute effective weight per class using overrides
    def class_weight(cls):
        short_name = cls.rsplit(".", 1)[-1]
        multiplier = WEIGHT_OVERRIDES.get(short_name, 1.0)
        return class_counts[cls] * multiplier

    # Greedy load-balancing: assign heaviest classes first to least-loaded shard
    shards = [[] for _ in range(total)]
    shard_loads = [0.0] * total
    for cls in sorted(class_counts, key=class_weight, reverse=True):
        lightest = min(range(total), key=lambda s: shard_loads[s])
        shards[lightest].append(cls)
        shard_loads[lightest] += class_weight(cls)

    for shard in range(total):
        my_classes = sorted(shards[shard])
        filter_expr = build_filter(my_classes)
        path = os.path.join(output_dir, f"shard_{shard}.filter")
        with open(path, "w") as f:
            f.write(filter_expr)
        print(f"  Shard {shard}: {len(my_classes)} classes, weight {shard_loads[shard]:.1f} ({sum(class_counts[c] for c in my_classes)} tests)", file=sys.stderr)
        for cls in my_classes:
            w = class_weight(cls)
            print(f"    - {cls} ({class_counts[cls]} tests, weight {w:.1f})", file=sys.stderr)


def cmd_read():
    if len(sys.argv) != 3:
        print(f"Usage: {sys.argv[0]} read <filter-file>", file=sys.stderr)
        sys.exit(1)

    path = sys.argv[2]
    if not os.path.exists(path):
        return
    with open(path) as f:
        content = f.read().strip()
    if content:
        # Print human-readable class list to stderr
        classes = [part.replace("FullyQualifiedName~", "").strip() for part in content.split("|")]
        print(f"Running {len(classes)} test classes:", file=sys.stderr)
        for cls in classes:
            print(f"  - {cls}", file=sys.stderr)
        print(content)


def main():
    if len(sys.argv) < 2:
        print(f"Usage: {sys.argv[0]} <generate|read> ...", file=sys.stderr)
        sys.exit(1)

    cmd = sys.argv[1]
    if cmd == "generate":
        cmd_generate()
    elif cmd == "read":
        cmd_read()
    else:
        print(f"Unknown command: {cmd}", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
