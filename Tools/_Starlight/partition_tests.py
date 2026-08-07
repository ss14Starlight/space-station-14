#!/usr/bin/env python3

"""
Partitions test classes across shards for parallel CI execution.

Mode 1 - Generate all shard filters to files:
    <test-app> --list-tests json | python3 partition_tests.py generate <total-shards> <output-dir> [timings-file]
    Writes <output-dir>/shard_0.filter .. shard_N.filter, plus a
    manifest.json recording how many tests were expected in total.

Mode 2 - Promote a merged CTRF report to the committed timings baseline:
    python3 partition_tests.py harvest <ctrf-in> <ctrf-out> [--manifest <path>] [--keep-stdout]
    The CI makes the merged report, we then parse it, then strip a bunch of un-needed stuff
    to reduce the file size (like 4mb down do 400kb). The test needed for a baseline
    has to be 1 clean run with no failures. Annoying, but required.

Exit codes:
    0 - success
    1 - error (bad arguments, nothing discovered, or a run unfit to be a baseline)
"""

import sys
import os
import json
import statistics

_SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))

# The baseline is a CTRF report so it stays readable by the same tooling that
# produced it.
CTRF_BASELINE_FILE = os.path.join(_SCRIPT_DIR, "test-timings.ctrf.json")

MANIFEST_NAME = "manifest.json"

EXPLICIT_TRAIT = "Explicit"

# Looking at this, you are probably thinking,
# Monsieur, have you lost your mind.
# But this is a permanent solution. The multithreading in the engine for tests will not be fixed anytime soon, all of this is here forever.
# See https://github.com/dotnet/runtime/issues/107197, who knows, maybe by time time you see this it will be fixed.
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
    "AllSalvageMapsLoadableTest": 5.0,
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
    "DynamicBudgetUpdateTest": 0.2,
    "DynamicMutuallyExclusiveRulesRejectionTest": 0.2,
    "DynamicRuleCooldownTest": 0.2,
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
    "HungerThirstIncreaseDecreaseTest": 3.0,
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
    "NoMaterialArbitrage": 15.0,
    "NoSavedPostMapInitTest": 30.0,
    "NoSliceableBountyArbitrageTest": 0.5,
    "NonGameMapsLoadableTest": 80.0,
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
    "ServerPrototypeSaveLoadSaveTest": 30.0,
    "SetWorkingState_AlreadyInState_NoChange": 0.5,
    "SetWorkingState_IdleToWorking_UpdatesLoad": 0.25,
    "ShuttlesLoadableTest": 70.0,
    "SpaceNoPuddleTest": 0.25,
    "SpawnAndDeleteAllEntitiesInTheSameSpot": 60.0,
    "SpawnAndDeleteAllEntitiesOnDifferentMaps": 100.0,
    "SpawnAndDeleteEntityCountTest": 115.0,
    "SpawnAndDirtyAllEntities": 240.0,
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
    "TestAllClientPrototypesAreSerializable": 35.0,
    "TestAllConcurrent": 0.25,
    "TestAllRestocksAreAvailableToBuy": 0.5,
    "TestAllServerPrototypesAreSerializable": 35.0,
    "TestApcLoad": 10.0,
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
    "ValidateRolePrototypes": 65.0,
    "WeightlessStatusTest": 0.25,
    "WindowOnGrille": 0.25,
    "WirelessNetworkDeviceSendAndReceive": 0.25,
    "WiresPanelScrewing": 0.25,
    "XenoArtifactBuildActiveNodesTest": 0.25,
    "XenoArtifactRemoveNodeTest": 0.5,
    "XenoArtifactResizeTest": 1.0,
}


def parse_tests_json(text):
    """Parse `--list-tests json` output into explicit tests runnable tests."""
    data = json.loads(text.lstrip("﻿ \t\r\n"))
    tests = []
    explicit = []
    for test in data.get("tests", []):
        uid = test.get("uid") or test.get("displayName")
        if not uid:
            continue
        traits = test.get("traits") or []
        if any(t.get("key") == EXPLICIT_TRAIT for t in traits):
            explicit.append(uid)
        else:
            tests.append(uid)
    return tests, explicit


def extract_classes(tests):
    """Group test uids by bare method name, with a count of cases per group."""
    counts = {}
    for test in tests:
        method = method_of(test)
        counts[method] = counts.get(method, 0) + 1
    return counts


def is_ctrf(data):
    return isinstance(data, dict) and data.get("reportFormat") == "CTRF"


def timings_from_ctrf(data):
    """Sum per-method seconds from a CTRF report, counting passes only."""
    timings = {}
    bad = 0
    for test in data.get("results", {}).get("tests", []):
        name = test.get("name")
        if not name:
            continue
        kind = classify(test)
        if kind != "passed":
            bad += kind == "bad"
            continue
        try:
            seconds = float(test.get("duration") or 0) / 1000.0
        except (TypeError, ValueError):
            continue
        if seconds > 0:
            method = method_of(name)
            timings[method] = timings.get(method, 0.0) + seconds
    if bad:
        print(f"Ignored {bad} failed result(s) when reading timings",
              file=sys.stderr)
    return timings


def load_timings(path):
    """Load {method: seconds} measured timings from a CTRF baseline, or None."""
    if not path or not os.path.exists(path):
        return None
    try:
        with open(path) as f:
            data = json.load(f)
    except (OSError, ValueError) as e:
        print(f"Warning: could not read timings file {path}: {e}", file=sys.stderr)
        return None

    if not is_ctrf(data):
        print(f"Warning: {path} is not a CTRF report", file=sys.stderr)
        return None

    return timings_from_ctrf(data) or None


def method_of(test_name):
    """Reduce a test uid or display name to its bare method name."""
    name = test_name.split("(")[0].strip()
    dot = name.rfind(".")
    return name[dot + 1:] if dot > 0 else name


def build_filter(methods, all_methods):
    """Build a Microsoft test-case filter from NUnit method names."""
    if not methods:
        return ""
    universe = sorted(all_methods)
    clauses = []
    for method in sorted(methods):
        terms = [f"Name~{method}"]
        terms += [f"Name!~{other}" for other in universe
                  if other != method and method in other]
        clauses.append("(" + "&".join(terms) + ")")
    return "|".join(clauses)


def cmd_generate():
    if len(sys.argv) not in (4, 5):
        print(f"Usage: {sys.argv[0]} generate <total-shards> <output-dir> [timings-file]", file=sys.stderr)
        sys.exit(1)

    total = int(sys.argv[2])
    output_dir = sys.argv[3]
    timings_path = sys.argv[4] if len(sys.argv) == 5 else CTRF_BASELINE_FILE

    tests, explicit = parse_tests_json(sys.stdin.read())

    if not tests:
        print("Error: no tests discovered from input", file=sys.stderr)
        sys.exit(1)

    explicit_methods = sorted({method_of(t) for t in explicit})
    if explicit:
        print(f"Excluded {len(explicit)} test(s) from [Explicit] method(s): "
              f"{', '.join(explicit_methods)}", file=sys.stderr)

    class_counts = extract_classes(tests)
    all_methods = set(class_counts) | set(explicit_methods)
    print(f"Discovered {len(tests)} tests in {len(class_counts)} classes, distributing across {total} shards", file=sys.stderr)

    timings = load_timings(timings_path)

    if timings:
        # Estimate unknown methods from the median per-test duration.
        rates = sorted(timings[c] / class_counts[c] for c in class_counts if c in timings)
        median_per_test = statistics.median(rates) if rates else 1.0
        print(f"Using measured timings from {timings_path}: "
              f"{len(rates)}/{len(class_counts)} classes have data, "
              f"fallback = {median_per_test:.3f}s/test (median)", file=sys.stderr)

        def class_weight(cls):
            if cls in timings:
                return timings[cls]
            return class_counts[cls] * median_per_test
    else:
        # no timings file, weight by count * manual override.
        print(f"No timings file at {timings_path}; using WEIGHT_OVERRIDES fallback", file=sys.stderr)

        def class_weight(cls):
            multiplier = WEIGHT_OVERRIDES.get(cls, 1.0)
            return class_counts[cls] * multiplier

    os.makedirs(output_dir, exist_ok=True)

    # Greedy load-balancing: assign heaviest classes first to least-loaded shard
    shards = [[] for _ in range(total)]
    shard_loads = [0.0] * total
    for cls in sorted(class_counts, key=class_weight, reverse=True):
        lightest = min(range(total), key=lambda s: shard_loads[s])
        shards[lightest].append(cls)
        shard_loads[lightest] += class_weight(cls)

    filters = []
    for shard in range(total):
        my_classes = sorted(shards[shard])
        filter_expr = build_filter(my_classes, all_methods)
        filters.append(filter_expr)
        print(f"  Shard {shard}: {len(my_classes)} classes, weight {shard_loads[shard]:.1f} ({sum(class_counts[c] for c in my_classes)} tests)", file=sys.stderr)
        for cls in my_classes:
            w = class_weight(cls)
            print(f"    - {cls} ({class_counts[cls]} tests, weight {w:.1f})", file=sys.stderr)

        filter_path = os.path.join(output_dir, f"shard_{shard}.filter")
        with open(filter_path, "w", newline="\n") as f:
            f.write(filter_expr)
            f.write("\n")

    # Record what a complete run looks like, so harvest can tell a full pass
    # from one that was cut short and refuse to bless the latter.
    manifest = {
        "total": len(tests),
        "methods": len(class_counts),
        "shards": {str(s): sum(class_counts[c] for c in shards[s]) for s in range(total)},
        "filters": filters,
    }
    with open(os.path.join(output_dir, MANIFEST_NAME), "w", newline="\n") as f:
        json.dump(manifest, f, indent=2, sort_keys=True)
        f.write("\n")
    print(f"Wrote manifest: {manifest['total']} tests expected across {total} shards",
          file=sys.stderr)

def summarize_ctrf(data):
    """Return (counts-by-status, total-tests) for a CTRF report."""
    counts = {}
    tests = data.get("results", {}).get("tests", [])
    for test in tests:
        status = test.get("status", "other")
        counts[status] = counts.get(status, 0) + 1
    return counts, len(tests)


def classify(test):
    """Bucket a CTRF result as 'passed', 'notrun' or 'bad'."""
    status = test.get("status", "other")
    if status == "passed":
        return "passed"
    if status == "skipped":
        return "notrun"
    if status == "other" and test.get("rawStatus") == "NotExecuted":
        return "notrun"
    return "bad"


def baseline_refusals(data, total, manifest):
    """List the reasons this run must not become a baseline."""
    reasons = []

    tests = data.get("results", {}).get("tests", [])
    bad = [t for t in tests if classify(t) == "bad"]
    if bad:
        names = sorted({t.get("name", "?") for t in bad})
        reasons.append(f"{len(bad)} test(s) did not pass and were not skipped: "
                       + ", ".join(names[:5]) + (" ..." if len(names) > 5 else ""))

    if not any(classify(t) == "passed" for t in tests):
        reasons.append("no tests passed")

    if manifest is not None:
        expected = manifest.get("total")
        if isinstance(expected, int) and total != expected:
            reasons.append(f"ran {total} tests but the manifest expected {expected} "
                           f"({expected - total} missing)")

    return reasons


def strip_test(test, keep_stdout):
    """Strip the CTRF down to a more reasonable size."""
    if keep_stdout:
        return test
    return {k: v for k, v in test.items()
            if k not in ("stdout", "stderr", "trace", "message", "filePath")}


def cmd_harvest():
    args = sys.argv[2:]
    keep_stdout = "--keep-stdout" in args
    args = [a for a in args if a != "--keep-stdout"]

    manifest_path = None
    if "--manifest" in args:
        i = args.index("--manifest")
        if i + 1 >= len(args):
            print("Error: --manifest needs a path", file=sys.stderr)
            sys.exit(1)
        manifest_path = args[i + 1]
        del args[i:i + 2]

    if len(args) != 2:
        print(f"Usage: {sys.argv[0]} harvest <ctrf-in> <ctrf-out> "
              f"[--manifest <path>] [--keep-stdout]", file=sys.stderr)
        sys.exit(1)

    ctrf_in, ctrf_out = args

    try:
        with open(ctrf_in) as f:
            data = json.load(f)
    except (OSError, ValueError) as e:
        print(f"Error: could not read CTRF report {ctrf_in}: {e}", file=sys.stderr)
        sys.exit(1)

    if not is_ctrf(data):
        print(f"Error: {ctrf_in} is not a CTRF report", file=sys.stderr)
        sys.exit(1)

    manifest = None
    if manifest_path:
        try:
            with open(manifest_path) as f:
                manifest = json.load(f)
        except (OSError, ValueError) as e:
            print(f"Error: could not read manifest {manifest_path}: {e}", file=sys.stderr)
            sys.exit(1)

    counts, total = summarize_ctrf(data)
    breakdown = ", ".join(f"{n} {s}" for s, n in sorted(counts.items())) or "nothing"
    print(f"CTRF report {ctrf_in}: {total} results ({breakdown})", file=sys.stderr)

    reasons = baseline_refusals(data, total, manifest)
    if reasons:
        print("Refusing to write a timings baseline:", file=sys.stderr)
        for reason in reasons:
            print(f"  - {reason}", file=sys.stderr)
        print("Only a complete, fully passing run may become a baseline.",
              file=sys.stderr)
        sys.exit(1)

    data["results"]["tests"] = [strip_test(t, keep_stdout)
                                for t in data["results"]["tests"]]

    with open(ctrf_out, "w", newline="\n") as f:
        json.dump(data, f, indent=2, sort_keys=True)
        f.write("\n")

    timings = timings_from_ctrf(data)
    print(f"Baseline written: {len(timings)} methods, "
          f"{sum(timings.values()):.1f}s total -> {ctrf_out} "
          f"({os.path.getsize(ctrf_out) / 1024:.0f} KiB)", file=sys.stderr)


def main():
    if len(sys.argv) < 2:
        print(f"Usage: {sys.argv[0]} <generate|harvest> ...",
              file=sys.stderr)
        sys.exit(1)

    cmd = sys.argv[1]
    if cmd == "generate":
        cmd_generate()
    elif cmd == "harvest":
        cmd_harvest()
    else:
        print(f"Unknown command: {cmd}", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()

