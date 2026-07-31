using System;
using System.Linq;
using Content.Shared._Starlight.Arcade.Lancer;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Arcade.Lancer;

public sealed partial class LancerGame
{
    private enum TutorialStepKind : byte
    {
        Move,
        EndTurn,
        Skirmish,
        Cover,
        Boost,
        LockOn,
        LockedAttack,
        Barrage,
        Overcharge,
        Stabilize,
        Overwatch,
        Brace,
        Hex,
        Disengage,
        Finish
    }

    private enum TutorialAdvanceEvent : byte
    {
        MovedToCell,
        EndedTurn,
        AttackResolved,
        LockOnApplied,
        BarrageResolved,
        Overcharged,
        StabilizedClearHeat,
        OverwatchResolved,
        BraceResolved,
        HexResolved,
        Disengaged,
        AllHostilesDestroyed
    }

    private sealed class TutorialStepDef
    {
        public required TutorialStepKind Kind;
        public required string HintLoc;
        public required LancerPlayerAction[] AllowedActions;
        public bool CanEndTurn;
        public required TutorialAdvanceEvent AdvanceOn;
    }

    private static readonly TutorialStepDef[] TutorialSteps =
    [
        new()
        {
            Kind = TutorialStepKind.Move,
            HintLoc = "lancer-arcade-tutorial-step-1-hint",
            AllowedActions = [LancerPlayerAction.SelectMove, LancerPlayerAction.ConfirmCell, LancerPlayerAction.Cancel],
            AdvanceOn = TutorialAdvanceEvent.MovedToCell
        },
        new()
        {
            Kind = TutorialStepKind.EndTurn,
            HintLoc = "lancer-arcade-tutorial-step-2-hint",
            AllowedActions = [LancerPlayerAction.EndTurn],
            CanEndTurn = true,
            AdvanceOn = TutorialAdvanceEvent.EndedTurn
        },
        new()
        {
            Kind = TutorialStepKind.Skirmish,
            HintLoc = "lancer-arcade-tutorial-step-3-hint",
            AllowedActions =
            [
                LancerPlayerAction.Skirmish, LancerPlayerAction.SelectWeapon, LancerPlayerAction.ConfirmTarget,
                LancerPlayerAction.Cancel
            ],
            AdvanceOn = TutorialAdvanceEvent.AttackResolved
        },
        new()
        {
            Kind = TutorialStepKind.Cover,
            HintLoc = "lancer-arcade-tutorial-step-4-hint",
            AllowedActions = [LancerPlayerAction.SelectMove, LancerPlayerAction.ConfirmCell, LancerPlayerAction.Cancel],
            AdvanceOn = TutorialAdvanceEvent.MovedToCell
        },
        new()
        {
            Kind = TutorialStepKind.Boost,
            HintLoc = "lancer-arcade-tutorial-step-5-hint",
            AllowedActions = [LancerPlayerAction.SelectBoost, LancerPlayerAction.ConfirmCell, LancerPlayerAction.Cancel],
            AdvanceOn = TutorialAdvanceEvent.MovedToCell
        },
        new()
        {
            Kind = TutorialStepKind.LockOn,
            HintLoc = "lancer-arcade-tutorial-step-6-hint",
            AllowedActions =
            [
                LancerPlayerAction.LockOn, LancerPlayerAction.ConfirmTarget, LancerPlayerAction.Cancel
            ],
            AdvanceOn = TutorialAdvanceEvent.LockOnApplied
        },
        new()
        {
            Kind = TutorialStepKind.LockedAttack,
            HintLoc = "lancer-arcade-tutorial-step-7-hint",
            AllowedActions =
            [
                LancerPlayerAction.Skirmish, LancerPlayerAction.SelectWeapon, LancerPlayerAction.ConfirmTarget,
                LancerPlayerAction.Cancel
            ],
            AdvanceOn = TutorialAdvanceEvent.AttackResolved
        },
        new()
        {
            Kind = TutorialStepKind.Barrage,
            HintLoc = "lancer-arcade-tutorial-step-8-hint",
            AllowedActions =
            [
                LancerPlayerAction.Barrage, LancerPlayerAction.SelectWeapon, LancerPlayerAction.ConfirmTarget,
                LancerPlayerAction.Cancel
            ],
            AdvanceOn = TutorialAdvanceEvent.BarrageResolved
        },
        new()
        {
            Kind = TutorialStepKind.Overcharge,
            HintLoc = "lancer-arcade-tutorial-step-9-hint",
            AllowedActions = [LancerPlayerAction.Overcharge],
            AdvanceOn = TutorialAdvanceEvent.Overcharged
        },
        new()
        {
            Kind = TutorialStepKind.Stabilize,
            HintLoc = "lancer-arcade-tutorial-step-10-hint",
            AllowedActions = [LancerPlayerAction.Stabilize, LancerPlayerAction.Cancel],
            AdvanceOn = TutorialAdvanceEvent.StabilizedClearHeat
        },
        new()
        {
            Kind = TutorialStepKind.Overwatch,
            HintLoc = "lancer-arcade-tutorial-step-11-hint",
            AllowedActions =
            [
                LancerPlayerAction.EndTurn, LancerPlayerAction.ReactionAccept, LancerPlayerAction.ReactionDecline
            ],
            CanEndTurn = true,
            AdvanceOn = TutorialAdvanceEvent.OverwatchResolved
        },
        new()
        {
            Kind = TutorialStepKind.Brace,
            HintLoc = "lancer-arcade-tutorial-step-12-hint",
            AllowedActions = [LancerPlayerAction.ReactionAccept, LancerPlayerAction.ReactionDecline],
            AdvanceOn = TutorialAdvanceEvent.BraceResolved
        },
        new()
        {
            Kind = TutorialStepKind.Hex,
            HintLoc = "lancer-arcade-tutorial-step-13-hint",
            AllowedActions =
            [
                LancerPlayerAction.UseSystem, LancerPlayerAction.ConfirmCell, LancerPlayerAction.Cancel
            ],
            AdvanceOn = TutorialAdvanceEvent.HexResolved
        },
        new()
        {
            Kind = TutorialStepKind.Disengage,
            HintLoc = "lancer-arcade-tutorial-step-14-hint",
            AllowedActions = [LancerPlayerAction.Disengage],
            AdvanceOn = TutorialAdvanceEvent.Disengaged
        },
        new()
        {
            Kind = TutorialStepKind.Finish,
            HintLoc = "lancer-arcade-tutorial-step-15-hint",
            AllowedActions =
            [
                LancerPlayerAction.Skirmish, LancerPlayerAction.SelectWeapon, LancerPlayerAction.ConfirmTarget,
                LancerPlayerAction.EndTurn, LancerPlayerAction.Cancel
            ],
            CanEndTurn = true,
            AdvanceOn = TutorialAdvanceEvent.AllHostilesDestroyed
        }
    ];

    private bool _tutorialActive;
    private int _tutorialStep;
    private LancerGridCoord? _tutorialGoalCell;
    private int _tutorialFocusUnitId = -1;
    private bool _tutorialForceBraceHit;
    private bool _tutorialBarragePending;

    private void ClearTutorialState()
    {
        _tutorialActive = false;
        _tutorialStep = 0;
        _tutorialGoalCell = null;
        _tutorialFocusUnitId = -1;
        _tutorialForceBraceHit = false;
        _tutorialBarragePending = false;
    }

    private void StartTutorial()
    {
        ClearTutorialState();
        ResetCampaignRun();
        ClearCombatState();

        _selectedMissionId = MissionTutorial;
        _selectedLoadoutId = LoadoutRaijinStrike;
        _fightIndex = 0;
        _tutorialActive = true;
        _tutorialStep = 0;

        // Fixed starter loadout; ignore campaign pilot skills so the lesson stays consistent.
        ApplyLoadoutAndSkills(arcade: null, LoadoutRaijinStrike);
        _playerHp = _playerMaxHp;
        _structure = _playerMaxStructure;
        _stress = PlayerMaxStress;
        _heat = 0;
        _repairs = _playerRepairCap;
        _hexCharges = _playerHexCap;
        _corePower = 1;
        _coreActive = false;
        _disengaged = false;
        _holdAndLock = false;
        _playerImpaired = false;
        _overchargeHeatStep = 0;
        ResetWeaponLoadedState();

        var arenaId = new ProtoId<LancerEncounterPrototype>("tutorial-arena");
        if (_prototypes.TryIndex(arenaId, out LancerEncounterPrototype? enc))
            _encounter = enc;

        _phase = LancerGamePhase.Combat;
        _combatStarted = true;
        _spotRolled = true;
        PrepareFightResources();
        _corePower = Math.Max(_corePower, 1);

        AddLog(Loc.GetString("lancer-arcade-log-tutorial-start"));
        SetupTutorialStep(0);
        PlayArcadeSound("newgame");
        BroadcastState();
    }

    private void CompleteTutorial(bool aborted = false)
    {
        AddLog(Loc.GetString(aborted
            ? "lancer-arcade-log-tutorial-abort"
            : "lancer-arcade-log-tutorial-complete"));
        if (!aborted)
            AddLog(Loc.GetString("lancer-arcade-tutorial-complete"));

        ClearTutorialState();
        EnterMissionSelect();
    }

    private TutorialStepDef CurrentTutorialStep() =>
        TutorialSteps[Math.Clamp(_tutorialStep, 0, TutorialSteps.Length - 1)];

    private void SetupTutorialStep(int stepIndex)
    {
        _tutorialStep = Math.Clamp(stepIndex, 0, TutorialSteps.Length - 1);
        _tutorialGoalCell = null;
        _tutorialFocusUnitId = -1;
        _tutorialForceBraceHit = false;
        _tutorialBarragePending = false;
        _pendingResolution = null;
        _reactionPromptActive = false;
        _pendingReactionContinue = null;
        _aiQueue.Clear();
        _processingAi = false;

        ClearTransientCombatSelections();
        InitEmptyGrid();
        _units.Clear();
        _nextUnitId = 1;

        // Refresh mech resources between lessons so economy gates stay teachable.
        _playerHp = _playerMaxHp;
        _heat = 0;
        _hexCharges = Math.Max(1, _playerHexCap);
        _disengaged = false;
        _playerImpaired = false;
        _coreActive = false;
        _corePower = 1;
        _overchargeHeatStep = 0;
        _overwatchUsesThisRound = 0;
        _braceUsedThisRound = false;
        _reactionUsedThisActivation = false;
        _braceReactionLockout = false;
        _braceRestrictedPending = false;
        _braceRestrictedTurn = false;
        _braceDefenseActive = false;
        _hyperReflexImmobilizeUnitId = -1;
        ResetWeaponLoadedState();

        var step = CurrentTutorialStep();
        switch (step.Kind)
        {
            case TutorialStepKind.Move:
                SetupMoveLesson();
                break;
            case TutorialStepKind.EndTurn:
                SetupEndTurnLesson();
                break;
            case TutorialStepKind.Skirmish:
                SetupSkirmishLesson();
                break;
            case TutorialStepKind.Cover:
                SetupCoverLesson();
                break;
            case TutorialStepKind.Boost:
                SetupBoostLesson();
                break;
            case TutorialStepKind.LockOn:
                SetupLockOnLesson();
                break;
            case TutorialStepKind.LockedAttack:
                SetupLockedAttackLesson();
                break;
            case TutorialStepKind.Barrage:
                SetupBarrageLesson();
                break;
            case TutorialStepKind.Overcharge:
                SetupOverchargeLesson();
                break;
            case TutorialStepKind.Stabilize:
                SetupStabilizeLesson();
                break;
            case TutorialStepKind.Overwatch:
                SetupOverwatchLesson();
                break;
            case TutorialStepKind.Brace:
                SetupBraceLesson();
                break;
            case TutorialStepKind.Hex:
                SetupHexLesson();
                break;
            case TutorialStepKind.Disengage:
                SetupDisengageLesson();
                break;
            case TutorialStepKind.Finish:
                SetupFinishLesson();
                break;
        }

        var hint = Loc.GetString(step.HintLoc);
        AddLog(Loc.GetString("lancer-arcade-log-tutorial-step",
            ("step", _tutorialStep + 1),
            ("hint", hint)));

        if (step.Kind == TutorialStepKind.Brace)
        {
            // Enemy fires immediately; player only answers the Brace prompt.
            _playerTurn = false;
            QueueTutorialScriptedAi();
            BroadcastState();
            return;
        }

        BeginPlayerTurn();
        // BeginPlayerTurn already broadcasts; re-apply goal highlights after economy reset.
        ApplyTutorialGoalHighlights();
        BroadcastState();
    }

    private void SpawnTutorialPlayer(LancerGridCoord pos)
    {
        AddUnit(LancerUnitKind.PlayerMech, pos, _playerMaxHp, _playerArmor, _playerEvasion,
            spriteState: _playerSprite);
        _playerHp = _playerMaxHp;
        SyncPlayerUnitHp();
    }

    private UnitRecord SpawnTutorialGrunt(LancerGridCoord pos, int tier = 0)
    {
        var stats = GetEnemyStats(LancerUnitKind.Grunt, tier);
        return AddUnit(LancerUnitKind.Grunt, pos, stats.Hp, stats.Armor, stats.Evasion,
            tier: tier, weaponId: stats.WeaponId);
    }

    private UnitRecord SpawnTutorialCutlass(LancerGridCoord pos)
    {
        var stats = GetEnemyStats(LancerUnitKind.Cutlass, 0);
        return AddUnit(LancerUnitKind.Cutlass, pos, stats.Hp, stats.Armor, stats.Evasion,
            weaponId: stats.WeaponId);
    }

    private void SetupMoveLesson()
    {
        var playerPos = new LancerGridCoord(5, 9);
        var goal = new LancerGridCoord(5, 6);
        SpawnTutorialPlayer(playerPos);
        SpawnTutorialGrunt(new LancerGridCoord(5, 1));
        _tutorialGoalCell = goal;
    }

    private void SetupEndTurnLesson()
    {
        var playerPos = new LancerGridCoord(5, 6);
        SpawnTutorialPlayer(playerPos);
        SpawnTutorialGrunt(new LancerGridCoord(5, 1));
    }

    private void SetupSkirmishLesson()
    {
        var playerPos = new LancerGridCoord(5, 7);
        var enemy = SpawnTutorialGrunt(new LancerGridCoord(5, 3));
        SpawnTutorialPlayer(playerPos);
        _tutorialFocusUnitId = enemy.Id;
    }

    private void SetupCoverLesson()
    {
        var playerPos = new LancerGridCoord(4, 8);
        var cover = new LancerGridCoord(5, 6);
        SetTerrain(new LancerGridCoord(5, 5), LancerTerrainType.RubbleSoft);
        SetTerrain(cover, LancerTerrainType.RubbleSoft);
        SpawnTutorialPlayer(playerPos);
        SpawnTutorialGrunt(new LancerGridCoord(5, 2));
        _tutorialGoalCell = cover;
    }

    private void SetupBoostLesson()
    {
        var playerPos = new LancerGridCoord(3, 7);
        var goal = new LancerGridCoord(7, 7);
        // Wall of hard rubble blocking normal Move between player and goal.
        for (var x = 4; x <= 6; x++)
            SetTerrain(new LancerGridCoord(x, 7), LancerTerrainType.RubbleHard);
        SetTerrain(new LancerGridCoord(5, 6), LancerTerrainType.RubbleHard);
        SetTerrain(new LancerGridCoord(5, 8), LancerTerrainType.RubbleHard);
        SpawnTutorialPlayer(playerPos);
        SpawnTutorialGrunt(new LancerGridCoord(9, 2));
        _tutorialGoalCell = goal;
    }

    private void SetupLockOnLesson()
    {
        var playerPos = new LancerGridCoord(5, 8);
        var enemy = SpawnTutorialGrunt(new LancerGridCoord(5, 2), tier: 1);
        SpawnTutorialPlayer(playerPos);
        _tutorialFocusUnitId = enemy.Id;
    }

    private void SetupLockedAttackLesson()
    {
        var playerPos = new LancerGridCoord(5, 8);
        var enemy = SpawnTutorialGrunt(new LancerGridCoord(5, 2), tier: 1);
        enemy.LockedOn = true;
        SpawnTutorialPlayer(playerPos);
        _tutorialFocusUnitId = enemy.Id;
    }

    private void SetupBarrageLesson()
    {
        var playerPos = new LancerGridCoord(5, 7);
        // Durable target so both barrage shots have something to hit.
        var enemy = SpawnTutorialCutlass(new LancerGridCoord(5, 3));
        SpawnTutorialPlayer(playerPos);
        _tutorialFocusUnitId = enemy.Id;
        _tutorialBarragePending = true;
    }

    private void SetupOverchargeLesson()
    {
        SpawnTutorialPlayer(new LancerGridCoord(5, 7));
        SpawnTutorialGrunt(new LancerGridCoord(5, 1));
    }

    private void SetupStabilizeLesson()
    {
        SpawnTutorialPlayer(new LancerGridCoord(5, 7));
        SpawnTutorialGrunt(new LancerGridCoord(5, 1));
        _heat = Math.Max(4, _playerHeatCap / 2);
    }

    private void SetupOverwatchLesson()
    {
        // Enemy starts adjacent (threat 1) and will step away — triggers Overwatch.
        var playerPos = new LancerGridCoord(5, 6);
        var enemy = SpawnTutorialGrunt(new LancerGridCoord(5, 5));
        SpawnTutorialPlayer(playerPos);
        _tutorialFocusUnitId = enemy.Id;
    }

    private void SetupBraceLesson()
    {
        var playerPos = new LancerGridCoord(5, 7);
        var enemy = SpawnTutorialCutlass(new LancerGridCoord(5, 4));
        SpawnTutorialPlayer(playerPos);
        _tutorialFocusUnitId = enemy.Id;
        _tutorialForceBraceHit = true;
        _overwatchUsesThisRound = 0;
        _braceUsedThisRound = false;
        _reactionUsedThisActivation = false;
        _braceReactionLockout = false;
        _braceRestrictedPending = false;
        _braceRestrictedTurn = false;
        _braceDefenseActive = false;
        _hyperReflexImmobilizeUnitId = -1;
    }

    private void SetupHexLesson()
    {
        var playerPos = new LancerGridCoord(5, 7);
        var blast = new LancerGridCoord(5, 4);
        var enemy = SpawnTutorialGrunt(blast);
        SpawnTutorialPlayer(playerPos);
        _tutorialFocusUnitId = enemy.Id;
        _tutorialGoalCell = blast;
        _hexCharges = Math.Max(1, _hexCharges);
    }

    private void SetupDisengageLesson()
    {
        var playerPos = new LancerGridCoord(5, 6);
        var enemy = SpawnTutorialGrunt(new LancerGridCoord(5, 5), tier: 1);
        SpawnTutorialPlayer(playerPos);
        _tutorialFocusUnitId = enemy.Id;
    }

    private void SetupFinishLesson()
    {
        SpawnTutorialPlayer(new LancerGridCoord(5, 7));
        SpawnTutorialGrunt(new LancerGridCoord(4, 3));
        SpawnTutorialGrunt(new LancerGridCoord(6, 3));
        // No focus unit: AllowedTargetIds empty means any hostile is valid.
        // Focusing one grunt made the other untargetable (looked invincible) and blocked completion.
        _tutorialFocusUnitId = -1;
    }

    private void ApplyTutorialGoalHighlights()
    {
        if (!_tutorialActive || _tutorialGoalCell is not { } goal || !LancerHex.InBounds(goal))
            return;

        _cells[goal.Y][goal.X].Highlight = LancerCellHighlight.Reachable;
    }

    private void FilterTutorialHighlights()
    {
        if (!_tutorialActive)
            return;

        var step = CurrentTutorialStep();
        var allowedCells = GetTutorialAllowedCells();
        var allowedTargets = GetTutorialAllowedTargetIds();

        if (_selectionMode is LancerSelectionMode.Move or LancerSelectionMode.Boost or LancerSelectionMode.HexTarget)
        {
            if (allowedCells.Length == 0)
                return;

            var allow = allowedCells.ToHashSet();
            for (var y = 0; y < GridSize; y++)
            for (var x = 0; x < GridSize; x++)
            {
                if (_cells[y][x].Highlight == LancerCellHighlight.None)
                    continue;
                if (!allow.Contains(new LancerGridCoord(x, y)))
                    _cells[y][x].Highlight = LancerCellHighlight.None;
            }

            return;
        }

        if (_selectionMode is LancerSelectionMode.Target or LancerSelectionMode.LockOnTarget)
        {
            if (allowedTargets.Length == 0)
                return;

            var allow = allowedTargets.ToHashSet();
            foreach (var unit in _units)
            {
                if (unit.Destroyed)
                    continue;
                var cell = _cells[unit.Position.Y][unit.Position.X];
                if (cell.Highlight == LancerCellHighlight.Target && !allow.Contains(unit.Id))
                    _cells[unit.Position.Y][unit.Position.X].Highlight = LancerCellHighlight.None;
            }
        }

        // Keep goal cell marked when not in a selection mode.
        if (_selectionMode == LancerSelectionMode.None
            && step.Kind is TutorialStepKind.Move or TutorialStepKind.Cover or TutorialStepKind.Boost or TutorialStepKind.Hex)
            ApplyTutorialGoalHighlights();
    }

    private LancerGridCoord[] GetTutorialAllowedCells()
    {
        if (_tutorialGoalCell is { } goal)
            return [goal];
        return Array.Empty<LancerGridCoord>();
    }

    private int[] GetTutorialAllowedTargetIds()
    {
        if (_tutorialFocusUnitId >= 0)
            return [_tutorialFocusUnitId];
        return Array.Empty<int>();
    }

    private LancerTutorialState? BuildTutorialState()
    {
        if (!_tutorialActive)
            return null;

        var step = CurrentTutorialStep();
        return new LancerTutorialState
        {
            Active = true,
            StepIndex = _tutorialStep,
            StepCount = TutorialSteps.Length,
            Title = Loc.GetString("lancer-arcade-tutorial-title",
                ("step", _tutorialStep + 1),
                ("total", TutorialSteps.Length)),
            Hint = Loc.GetString(step.HintLoc),
            AllowedActions = step.AllowedActions,
            AllowedCells = GetTutorialAllowedCells(),
            AllowedTargetIds = GetTutorialAllowedTargetIds(),
            CanEndTurn = step.CanEndTurn
        };
    }

    private bool IsTutorialActionAllowed(
        LancerPlayerAction action,
        LancerGridCoord? cell,
        int weaponIndex,
        int targetUnitId,
        LancerStabilizeOption stabilizeOption)
    {
        if (!_tutorialActive)
            return true;

        var step = CurrentTutorialStep();
        var allowed = step.AllowedActions;

        if (action == LancerPlayerAction.Cancel)
            return true;

        if (action is LancerPlayerAction.ReactionAccept or LancerPlayerAction.ReactionDecline)
            return allowed.Contains(action);

        if (action == LancerPlayerAction.EndTurn)
            return step.CanEndTurn;

        if (action == LancerPlayerAction.ConfirmCell)
        {
            if (!allowed.Contains(LancerPlayerAction.ConfirmCell)
                && !allowed.Contains(LancerPlayerAction.SelectMove)
                && !allowed.Contains(LancerPlayerAction.SelectBoost)
                && !allowed.Contains(LancerPlayerAction.UseSystem))
                return false;

            if (cell == null)
                return false;

            var cells = GetTutorialAllowedCells();
            return cells.Length == 0 || cells.Any(c => c.Equals(cell));
        }

        if (action == LancerPlayerAction.ConfirmTarget)
        {
            if (!allowed.Contains(LancerPlayerAction.ConfirmTarget)
                && !allowed.Contains(LancerPlayerAction.Skirmish)
                && !allowed.Contains(LancerPlayerAction.Barrage)
                && !allowed.Contains(LancerPlayerAction.LockOn))
                return false;

            var targets = GetTutorialAllowedTargetIds();
            return targets.Length == 0 || targets.Contains(targetUnitId);
        }

        if (action == LancerPlayerAction.SelectWeapon)
        {
            return allowed.Contains(LancerPlayerAction.Skirmish)
                   || allowed.Contains(LancerPlayerAction.Barrage)
                   || allowed.Contains(LancerPlayerAction.SelectWeapon);
        }

        if (action == LancerPlayerAction.Stabilize)
        {
            if (!allowed.Contains(LancerPlayerAction.Stabilize))
                return false;

            // Entering stabilize mode is fine; committing only allows Clear Heat.
            if (_selectionMode == LancerSelectionMode.Stabilize
                && stabilizeOption != LancerStabilizeOption.ClearHeat)
                return false;

            return true;
        }

        return allowed.Contains(action);
    }

    private void NotifyTutorialEvent(TutorialAdvanceEvent evt)
    {
        if (!_tutorialActive)
            return;

        var step = CurrentTutorialStep();
        if (step.AdvanceOn != evt)
            return;

        if (_tutorialStep >= TutorialSteps.Length - 1)
        {
            CompleteTutorial();
            return;
        }

        SetupTutorialStep(_tutorialStep + 1);
    }

    private void OnTutorialPlayerMoved(LancerGridCoord cell)
    {
        if (!_tutorialActive || _tutorialGoalCell is not { } goal)
            return;

        if (cell.Equals(goal))
            NotifyTutorialEvent(TutorialAdvanceEvent.MovedToCell);
    }

    private void OnTutorialEndTurn()
    {
        if (!_tutorialActive)
            return;

        var step = CurrentTutorialStep();
        if (step.Kind == TutorialStepKind.EndTurn)
        {
            NotifyTutorialEvent(TutorialAdvanceEvent.EndedTurn);
            return;
        }

        if (step.Kind == TutorialStepKind.Overwatch)
        {
            QueueTutorialScriptedAi();
            return;
        }

        if (step.Kind == TutorialStepKind.Finish)
        {
            // Inert enemies — return initiative immediately.
            _aiQueue.Clear();
            _processingAi = false;
            BeginPlayerTurn();
        }
    }

    private void QueueTutorialScriptedAi()
    {
        _aiQueue.Clear();
        var step = CurrentTutorialStep();
        var enemy = _tutorialFocusUnitId >= 0
            ? GetUnit(_tutorialFocusUnitId)
            : _units.FirstOrDefault(u => !u.Destroyed && IsEnemyKind(u.Kind));
        var player = GetPlayerUnit();

        if (enemy == null || player == null)
        {
            _processingAi = false;
            if (step.Kind != TutorialStepKind.Brace)
                BeginPlayerTurn();
            return;
        }

        if (step.Kind == TutorialStepKind.Overwatch)
        {
            // Step away from the player while starting adjacent → Overwatch prompt.
            var dest = new LancerGridCoord(enemy.Position.X, Math.Max(0, enemy.Position.Y - 1));
            if (dest.Equals(enemy.Position) || IsOccupied(dest))
                dest = new LancerGridCoord(Math.Clamp(enemy.Position.X + 1, 0, GridSize - 1), enemy.Position.Y);

            _aiQueue.Add(new AiStep
            {
                UnitId = enemy.Id,
                Kind = AiStepKind.Move,
                TargetCell = dest
            });
        }
        else if (step.Kind == TutorialStepKind.Brace)
        {
            _aiQueue.Add(new AiStep
            {
                UnitId = enemy.Id,
                Kind = AiStepKind.Attack,
                TargetUnitId = player.Id
            });
        }

        if (_aiQueue.Count > 0)
        {
            _processingAi = true;
            _aiStepTimer = AiStepDelay;
        }
        else
        {
            _processingAi = false;
            BeginPlayerTurn();
        }
    }

    private bool ShouldSkipNormalEnemyAi() =>
        _tutorialActive && CurrentTutorialStep().Kind is TutorialStepKind.Overwatch
            or TutorialStepKind.Brace
            or TutorialStepKind.EndTurn
            or TutorialStepKind.Finish;
}
