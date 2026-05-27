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
    "AbsorbentOnRefillableTest": 0.125,
    "AbsorbentOnSmallRefillableTest": 0.125,
    "AddListRemoveObjectiveTest": 0.125,
    "AddPlayerSessionLog": 0.25,
    "AdjustJobsTest": 0.5,
    "AgeRequirementsTest": 0.5,
    "AirConsistencyTest": 0.5,
    "AirlockBlockTest": 0.5,
    "AllCommandsHaveDescriptions": 0.5,
    "AllComponentsOneToOneDeleteTest": 0.5,
    "AllItemsHaveSpritesTest": 0.25,
    "AllMapsTested": 0.5,
    "AllSalvageMapsLoadableTest": 0.25,
    "AndTest": 0.5,
    "ApcChargingTest": 0.5,
    "ApcNetTest": 1.0,
    "ArmBladeActivateDeactivateTest": 0.5,
    "AutoRecordReplayTest": 0.25,
    "BananaSlipTest": 0.5,
    "BucklePullTest": 0.25,
    "BuckleInteractBuckleUnbuckleSelf": 0.5,
    "BuckleUnbuckleCooldownRangeTest": 0.25,
    "BulkAddLogs": 0.25,
    "CancelRepeatedWeld": 0.25,
    "CancelTilePry": 0.5,
    "CancelWallConstruct": 0.5,
    "ChairTest": 0.25,
    "ChasmFallTest": 0.5,
    "ChasmGrappleTest": 0.25,
    "ClientPrototypeSaveLoadSaveTest": 0.125,
    "CommsServerKeys": 0.25,
    "Component_InitDataCorrect": 0.25,
    "ConstructProtolathe": 0.25,
    "ConstructReinforcedWindow": 0.5,
    "ConstructionGraphEdgeValid": 0.25,
    "ConstructionGraphSpawnPrototypeValid": 0.5,
    "CraftGrenade": 0.25,
    "CraftRods": 0.5,
    "CreateDeleteCreateTest": 0.25,
    "CreateSaveLoadSaveGrid": 0.25,
    "Date": 0.0625,
    "DeconstructComputer": 0.25,
    "DeconstructTable": 0.0625,
    "DeconstructWall": 0.25,
    "DeconstructWindow": 0.5,
    "Delete_CacheUpdatesOnAtmosTick": 0.25,
    "DeonstructReinforcedWindow": 0.25,
    "DeserializeNullDefinitionTest": 0.5,
    "DeserializeNullTest": 0.5,
    "DisciplineValidTierPrerequesitesTest": 0.5,
    "DispenseItemTest": 0.125,
    "DragDropOntoDrainTest": 0.125,
    "DragDropOpensStrip": 0.5,
    "DuplicatePlayerIdDoesNotThrowTest": 0.5,
    "EORPluralizationTest": 0.5,
    "EmergencyEvacTest": 0.5,
    "EnsureNoEdgeClobbering": 0.5,
    "EntityEntityTest": 1.0,
    "EntityShowDepartmentsAndJobs": 0.25,
    "FillLevelSpritesExist": 0.0625,
    "FireSpreading": 0.25,
    "FloorConstructDeconstruct": 0.25,
    "FollowerMapDeleteTest": 0.125,
    "ForceUnbuckleBuckleTest": 0.5,
    "GasSpecificHeats_Agree": 0.5,
    "GasSpreading": 0.5,
    "GetAndReturnCup": 0.25,
    "HeadsetKeys": 0.25,
    "HeatScaleCVar_Replicates_Agree": 0.25,
    "HumanMoveOverTest": 0.125,
    "HungerThirstIncreaseDecreaseTest": 0.25,
    "IgnoredComponentsExistInTheCorrectPlaces": 0.5,
    "InsertAndDispenseItemTest": 0.125,
    "InsertDumpableInsertableItemTest": 0.5,
    "InsertEjectBuiTest": 0.0625,
    "InsideContainerInteractionBlockTest": 0.25,
    "InteractUITest": 0.25,
    "InteractionOutOfRangeTest": 0.5,
    "InteractionTest": 0.25,
    "JobPreferenceTest": 0.25,
    "JobWeightTest": 1.0,
    "KillAndReviveTest": 0.5,
    "LoadSaveTicksSave": 0.5,
    "LoadTickLoad": 0.5,
    "MagazineVisualsSpritesExist": 0.125,
    "MicrowaveRecipesFreezeTest": 0.125,
    "MouseMoveOverTest": 0.25,
    "MultiTile_Component_InitDataCorrect": 0.25,
    "MultiTile_Delete_CacheUpdatesOnAtmosTick": 0.25,
    "MultiTile_Spawn_CacheUpdatesOnAtmosTick": 0.125,
    "NoCargoBountyArbitrageTest": 0.25,
    "NoCargoOrderArbitrage": 0.25,
    "NoMaterialArbitrage": 0.25,
    "NoSavedPostMapInitTest": 0.25,
    "NoSliceableBountyArbitrageTest": 0.5,
    "NullOutTileAtmosphereGasMixture": 0.5,
    "PardonTest": 0.25,
    "ParseTestDocument": 2.0,
    "PlaceThenCutLattice": 2.0,
    "PoweredClosedAirlock_Pry_DoesNotOpen": 0.25,
    "PoweredOpenAirlock_Pry_DoesNotClose": 0.25,
    "PreRoundAddAndGetSingle": 0.5,
    "ProcessingAbsoluteDamageTest": 0.25,
    "ProcessingAbsoluteStandbyTest": 0.25,
    "ProcessingDeltaDamageTest": 0.125,
    "ProcessingListAutoJoinTest": 0.5,
    "PrototypesHaveKnownComponents": 2.0,
    "PryLattice": 0.25,
    "PullerIsConsideredInteractingTest": 2.0,
    "PullerSanityTest": 0.5,
    "QuerySingleLog": 0.5,
    "RejuvenateDeadTest": 0.25,
    "Relogin": 0.5,
    "RepairReinforcedWindow": 0.5,
    "ResettingEntitySystemResetTest": 0.25,
    "RestartRoundAfterStart": 0.5,
    "RestartTest": 0.5,
    "RestockTest": 0.5,
    "SelectionTest": 0.5,
    "ServerPrototypeSaveLoadSaveTest": 0.5,
    "SetWorkingState_AlreadyInState_NoChange": 0.5,
    "SetWorkingState_IdleToWorking_UpdatesLoad": 0.25,
    "SpaceNoPuddleTest": 0.25,
    "SpawnAndDeleteAllEntitiesOnDifferentMaps": 0.5,
    "SpawnItemInSlotTest": 0.25,
    "Spawn_CacheUpdatesOnAtmosTick": 0.125,
    "Spawn_ReconstructedUpdatesImmediately": 0.5,
    "SpillCorner": 0.5,
    "StackPrice": 0.5,
    "StartRoundTest": 0.5,
    "StopHardCodingWidgetsJesusChristTest": 2.0,
    "StorageSizeArbitrageTest": 0.25,
    "TakeRoleAndReturn": 0.125,
    "TestAb": 0.5,
    "TestAddRemoveHasRoles": 2.0,
    "TestAlarmThreshold": 0.5,
    "TestAllConcurrent": 0.25,
    "TestAllRestocksAreAvailableToBuy": 0.5,
    "TestBatteriesProportional": 0.5,
    "TestBatteryRamp": 0.25,
    "TestBladeServerBoardHasValidBladeServer": 0.25,
    "TestClientStart": 0.25,
    "TestCombatActionsAdded": 0.5,
    "TestComputerBoardHasValidComputer": 0.25,
    "TestConnect": 0.5,
    "TestDamageSpecifierOperations": 0.5,
    "TestDeleteCharacter": 0.5,
    "TestDeleteThrownItem": 0.5,
    "TestDeleteVisiting": 0.5,
    "TestDeletedCanReconnect": 0.25,
    "TestDisconnectWhileEmbedded": 0.5,
    "TestDockingConfig": 0.5,
    "TestDungeonPresets": 0.25,
    "TestDungeonRoomPackBounds": 0.25,
    "TestDuplicatePrevention": 0.25,
    "TestEntityDeadWhenGibbed": 0.0625,
    "TestFinished": 0.25,
    "TestFullBattery": 0.0625,
    "TestGasArrayDeserialization": 0.5,
    "TestGhostDoesNotInfiniteLoop": 0.5,
    "TestGhostGridNotTerminating": 0.5,
    "TestGhostsCanReconnect": 1.0,
    "TestGib": 0.25,
    "TestGridGhostOnQueueDelete": 0.5,
    "TestGridJoinAtmosphere": 0.125,
    "TestInternalsAutoActivateInSpaceForEntitySpawn": 0.5,
    "TestLatheRecipeIngredientsFitLathe": 0.5,
    "TestLayoutInheritance": 0.25,
    "TestLobbyPlayersValid": 0.25,
    "TestLogErrorCausesTestFailure": 0.5,
    "TestMindTransfersToOtherEntity": 0.5,
    "TestNoDemandRampdown": 0.5,
    "TestNoManualEntityLocStrings": 0.5,
    "TestOriginalDeletedWhileGhostingKeepsGhost": 0.25,
    "TestOwningPlayerCanBeChanged": 0.25,
    "TestPickupDrop": 0.5,
    "TestPlayerCanGhost": 0.5,
    "TestPvsCommands": 2.0,
    "TestReplaceMind": 0.5,
    "TestRestockBreaksOpen": 0.5,
    "TestRestockInventoryBounds": 2.0,
    "TestSerializable": 0.25,
    "TestSimpleBatteryChargeDeficit": 0.25,
    "TestSimpleDeficit": 0.5,
    "TestStartIsValid": 0.25,
    "TestStartReachesValidTarget": 0.125,
    "TestStartingGearStorage": 0.5,
    "TestStaticAnchorPrototypes": 0.25,
    "TestStationStartingPowerWindow": 0.125,
    "TestStorageFillPrototypes": 0.25,
    "TestSufficientSpaceForEntityStorageFill": 0.0625,
    "TestSufficientSpaceForFill": 0.5,
    "TestSuicide": 0.5,
    "TestSuicideByHeldItemSpreadDamage": 0.5,
    "TestSuicideWhileDamaged": 0.5,
    "TestSupplyPrioritized": 0.5,
    "TestSupplyRamp": 0.125,
    "TestTags": 0.5,
    "TestTargetIsValid": 0.5,
    "TestTemperatureCalculations": 0.25,
    "TestTerminalNodeGroups": 0.25,
    "TestThrownEggBreaks": 2.0,
    "TestUserDoesNotExist": 2.0,
    "TestVisitingReconnect": 0.5,
    "ThrowItemIntoDisposalUnitTest": 0.125,
    "TryAddTooMuchNonReactiveReagent": 0.25,
    "TryAddTwoNonReactiveReagent": 0.25,
    "TryAllTest": 0.5,
    "TryMixAndOverflowTooMuchReagent": 0.5,
    "TryStopNukeOpsFromConstantlyFailing": 0.125,
    "UiInteractTest": 2.0,
    "UnpoweredOpenAirlock_Pry_Closes": 0.5,
    "ValidateJobPrototypes": 0.125,
    "ValidateMobThresholds": 0.125,
    "ValidatePrototypeContents": 0.5,
    "WeightlessStatusTest": 0.25,
    "WindowOnGrille": 0.25,
    "WirelessNetworkDeviceSendAndReceive": 0.25,
    "WiresPanelScrewing": 0.25,
    "XenoArtifactBuildActiveNodesTest": 0.25,
    "XenoArtifactRemoveNodeTest": 0.5,
    "XenoArtifactResizeTest": 1.0,
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
    """Extract unique test method groups with test counts from display names.

    --list-tests outputs display names:
      - Windows:  MethodName  or  MethodName(params)
      - Linux:    FixtureName.MethodName  or  FixtureName.MethodName(params)

    We always extract the METHOD name as the group key so behaviour is
    consistent across platforms and the Name~ filter works everywhere.
    """
    counts = {}
    for test in tests:
        name = test.split("(")[0].strip()
        # If format is "Fixture.Method", take just the method part
        dot = name.rfind(".")
        method = name[dot + 1:] if dot > 0 else name
        counts[method] = counts.get(method, 0) + 1
    return counts


def build_filter(methods):
    """Build a NUnit.Where expression from method names.

    Uses NUnit Test Selection Language with exact method name matching.
    This avoids substring issues (e.g. 'Test' matching 'TestConnect')
    that plague VSTest Name~ filters.
    """
    if not methods:
        return ""
    return "||".join(f"method=='{m}'" for m in sorted(methods))


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
        multiplier = WEIGHT_OVERRIDES.get(cls, 1.0)
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
        methods = [part.replace("method==", "").strip("' ") for part in content.split("||")]
        print(f"Running {len(methods)} test groups:", file=sys.stderr)
        for m in methods:
            print(f"  - {m}", file=sys.stderr)
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
