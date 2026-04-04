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
# Looking at this, you are probably thinking,
# Monsieur, have you lost your mind.
# But this is a temporary solution. Once multithreading in the engine is fixed, all of this will be reverted.
# How do you use it? Run the test, take the one that finished the fastest and decrease its weight, then increase the weight of the slowest one until they balance out.
WEIGHT_OVERRIDES = {
    "AbsorbentOnRefillableTest": 0.5,
    "AbsorbentOnSmallRefillableTest": 0.5,
    "AddListRemoveObjectiveTest": 0.25,
    "AddPlayerSessionLog": 0.25,
    "AirlockBlockTest": 0.5,
    "AllCommandsHaveDescriptions": 0.5,
    "AllComponentsOneToOneDeleteTest": 0.5,
    "ApcChargingTest": 0.5,
    "ApcNetTest": 2.0,
    "ArmBladeActivateDeactivateTest": 0.5,
    "BucklePullTest": 0.25,
    "BuckleInteractBuckleUnbuckleSelf": 0.5,
    "CancelRepeatedWeld": 0.25,
    "CancelTilePry": 0.5,
    "ChasmFallTest": 0.5,
    "ClientPrototypeSaveLoadSaveTest": 0.25,
    "CommsServerKeys": 0.25,
    "ConstructProtolathe": 0.25,
    "ConstructReinforcedWindow": 0.5,
    "ConstructionGraphEdgeValid": 0.25,
    "ConstructionGraphSpawnPrototypeValid": 0.5,
    "CraftRods": 0.5,
    "CreateSaveLoadSaveGrid": 0.25,
    "Date": 0.25,
    "DeconstructComputer": 0.25,
    "DeconstructTable": 0.25,
    "Delete_CacheUpdatesOnAtmosTick": 0.5,
    "DeonstructReinforcedWindow": 0.25,
    "DeserializeNullTest": 0.5,
    "DisciplineValidTierPrerequesitesTest": 0.5,
    "DispenseItemTest": 0.25,
    "DragDropOntoDrainTest": 0.5,
    "DuplicatePlayerIdDoesNotThrowTest": 0.5,
    "EmergencyEvacTest": 0.5,
    "EntityEntityTest": 2.0,
    "FillLevelSpritesExist": 0.25,
    "FloorConstructDeconstruct": 0.25,
    "FollowerMapDeleteTest": 0.5,
    "ForceUnbuckleBuckleTest": 0.5,
    "GasSpecificHeats_Agree": 0.5,
    "GasSpreading": 0.5,
    "GetAndReturnCup": 0.25,
    "HumanMoveOverTest": 0.5,
    "HungerThirstIncreaseDecreaseTest": 0.5,
    "InsertAndDispenseItemTest": 0.5,
    "InsertDumpableInsertableItemTest": 2.0,
    "InsertEjectBuiTest": 0.25,
    "InsideContainerInteractionBlockTest": 0.25,
    "InteractionOutOfRangeTest": 0.5,
    "JobPreferenceTest": 0.25,
    "JobWeightTest": 2.0,
    "KillAndReviveTest": 0.5,
    "LoadTickLoad": 0.5,
    "MagazineVisualsSpritesExist": 0.5,
    "MicrowaveRecipesFreezeTest": 0.5,
    "MouseMoveOverTest": 0.25,
    "MultiTile_Component_InitDataCorrect": 0.5,
    "MultiTile_Spawn_CacheUpdatesOnAtmosTick": 0.5,
    "NoCargoBountyArbitrageTest": 0.25,
    "NoCargoOrderArbitrage": 0.25,
    "NoSavedPostMapInitTest": 0.25,
    "NullOutTileAtmosphereGasMixture": 0.5,
    "PardonTest": 0.25,
    "ParseTestDocument": 2.0,
    "PlaceThenCutLattice": 2.0,
    "PoweredClosedAirlock_Pry_DoesNotOpen": 0.5,
    "PoweredOpenAirlock_Pry_DoesNotClose": 0.25,
    "ProcessingAbsoluteStandbyTest": 0.25,
    "ProcessingDeltaDamageTest": 0.25,
    "ProcessingListAutoJoinTest": 0.5,
    "PrototypesHaveKnownComponents": 2.0,
    "PryLattice": 0.25,
    "PullerIsConsideredInteractingTest": 2.0,
    "PullerSanityTest": 0.5,
    "QuerySingleLog": 0.5,
    "Relogin": 0.5,
    "RepairReinforcedWindow": 0.5,
    "ResettingEntitySystemResetTest": 0.25,
    "RestartTest": 0.5,
    "RestockTest": 0.5,
    "SelectionTest": 0.5,
    "SetWorkingState_AlreadyInState_NoChange": 0.5,
    "SpawnItemInSlotTest": 0.25,
    "Spawn_CacheUpdatesOnAtmosTick": 0.25,
    "Spawn_ReconstructedUpdatesImmediately": 0.5,
    "SpillCorner": 0.5,
    "StackPrice": 0.5,
    "StartRoundTest": 0.5,
    "StopHardCodingWidgetsJesusChristTest": 2.0,
    "StorageSizeArbitrageTest": 0.5,
    "TakeRoleAndReturn": 0.25,
    "Test": 2.0,
    "TestAb": 0.5,
    "TestAddRemoveHasRoles": 2.0,
    "TestBatteryRamp": 0.25,
    "TestBladeServerBoardHasValidBladeServer": 0.25,
    "TestClientStart": 0.25,
    "TestComputerBoardHasValidComputer": 0.25,
    "TestConnect": 0.5,
    "TestDamageSpecifierOperations": 0.5,
    "TestDeleteCharacter": 0.5,
    "TestDeleteThrownItem": 0.5,
    "TestDeleteVisiting": 0.5,
    "TestDisconnectWhileEmbedded": 0.5,
    "TestDockingConfig": 0.5,
    "TestDungeonRoomPackBounds": 0.5,
    "TestEntityDeadWhenGibbed": 0.25,
    "TestFinished": 0.25,
    "TestFullBattery": 0.25,
    "TestGhostsCanReconnect": 2.0,
    "TestGib": 0.5,
    "TestGridGhostOnQueueDelete": 0.5,
    "TestGridJoinAtmosphere": 0.5,
    "TestLatheRecipeIngredientsFitLathe": 0.5,
    "TestLayoutInheritance": 0.25,
    "TestLobbyPlayersValid": 0.25,
    "TestMindTransfersToOtherEntity": 0.5,
    "TestNoDemandRampdown": 0.5,
    "TestNoManualEntityLocStrings": 2.0,
    "TestOwningPlayerCanBeChanged": 0.25,
    "TestPickupDrop": 0.5,
    "TestPlayerCanGhost": 0.5,
    "TestPvsCommands": 2.0,
    "TestRestockBreaksOpen": 0.5,
    "TestRestockInventoryBounds": 2.0,
    "TestSimpleDeficit": 2.0,
    "TestStartReachesValidTarget": 0.5,
    "TestStationStartingPowerWindow": 0.25,
    "TestSufficientSpaceForEntityStorageFill": 0.25,
    "TestSufficientSpaceForFill": 0.5,
    "TestSuicide": 0.5,
    "TestSuicideWhileDamaged": 0.5,
    "TestSupplyRamp": 0.5,
    "TestTags": 0.5,
    "TestTerminalNodeGroups": 0.25,
    "TestThrownEggBreaks": 2.0,
    "TestUserDoesNotExist": 2.0,
    "TestVisitingReconnect": 2.0,
    "ThrowItemIntoDisposalUnitTest": 0.5,
    "TryAddTooMuchNonReactiveReagent": 0.25,
    "TryAddTwoNonReactiveReagent": 0.5,
    "TryAllTest": 0.5,
    "TryStopNukeOpsFromConstantlyFailing": 0.25,
    "UiInteractTest": 2.0,
    "ValidateJobPrototypes": 0.25,
    "ValidateMobThresholds": 0.25,
    "WeightlessStatusTest": 0.5,
    "WindowOnGrille": 0.5,
    "XenoArtifactResizeTest": 2.0,
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
