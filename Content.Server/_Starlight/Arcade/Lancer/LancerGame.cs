using Content.Server._Starlight.Arcade.Systems;
using Content.Shared._Starlight.Arcade.Lancer;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Arcade.Lancer;

public sealed partial class LancerGame
{
    private readonly IEntityManager _entityManager;
    private readonly IRobustRandom _random;
    private readonly IPrototypeManager _prototypes;
    private readonly UserInterfaceSystem _uiSystem;
    private readonly ArcadeSystem _arcade;
    private readonly SharedAudioSystem _audio;

    public LancerGame(
        EntityUid owner,
        IEntityManager entityManager,
        IRobustRandom random,
        IPrototypeManager prototypes,
        UserInterfaceSystem uiSystem,
        ArcadeSystem arcade,
        SharedAudioSystem audio)
    {
        _entityManager = entityManager;
        _random = random;
        _prototypes = prototypes;
        _uiSystem = uiSystem;
        _arcade = arcade;
        _audio = audio;
        _owner = owner;
        ResetMission();
    }

    public void ResetMission()
    {
        ResetCampaignRun();
        _phase = LancerGamePhase.Briefing;
        _log.Clear();
        _units.Clear();
        _aiQueue.Clear();
        _usedActions.Clear();
        _nextUnitId = 1;
        _playerTurn = true;
        _spotBonus = false;
        _spotFailed = false;
        _spotRoll = 0;
        _spotRolled = false;
        _combatStarted = false;
        _firstPlayerAttackBonus = false;
        ResetRunMechState();
        _selectionMode = LancerSelectionMode.None;
        _attackIntent = LancerAttackIntent.None;
        _selectedWeaponIndex = -1;
        _barragePickedWeapons.Clear();
        _barrageWeaponQueue.Clear();
        _barrageCurrentTargetId = -1;
        _briefingStep = 0;
        _reactionPromptActive = false;
        _reactionTimer = 0;
        _reactionContext = PendingReactionContext.None;
        _reactionUnitId = -1;
        _pendingBraceDamage = 0;
        _pendingBraceRoll = 0;
        _pendingBraceAcc = 0;
        _pendingBraceDiff = 0;
        _pendingBraceHit = false;
        _pendingBraceCrit = false;
        _pendingBraceAttacker = null;
        _pendingBraceTarget = null;
        _aiStepTimer = 0;
        _processingAi = false;
        _pendingResolution = null;
        InitEmptyGrid();
    }

    public void StartCombat()
    {
        if (_phase != LancerGamePhase.PreFight || _pendingResolution != null)
            return;

        if (!_spotRolled || _encounter == null)
            return;

        _phase = LancerGamePhase.Combat;
        _combatStarted = true;
        PrepareFightResources();
        _corePower = Math.Max(_corePower, 1);
        LoadEncounterTerrain(_encounter);
        SpawnCombatUnits();
        BeginPlayerTurn();
        AddLog(Loc.GetString("lancer-arcade-log-combat-begin"));
        PlayArcadeSound("newgame");
        BroadcastState();
    }

    public void ProcessAction(LancerPlayerAction action, LancerGridCoord? cell, int weaponIndex, int targetUnitId, LancerStabilizeOption stabilizeOption, string contextId = "")
    {
        // Soft reset is always available mid-session, even during reactions / AI / dice.
        if (action == LancerPlayerAction.ReturnToMenu)
        {
            if (_phase is not (LancerGamePhase.Briefing or LancerGamePhase.MissionSelect))
                ReturnToMissionSelect();
            return;
        }

        if (_reactionPromptActive)
        {
            if (action is LancerPlayerAction.ReactionAccept or LancerPlayerAction.ReactionDecline)
            {
                if (!IsTutorialActionAllowed(action, cell, weaponIndex, targetUnitId, stabilizeOption))
                    return;

                ResolveReaction(action == LancerPlayerAction.ReactionAccept);
                return;
            }

            return;
        }

        if (_pendingResolution != null || _processingAi)
            return;

        switch (_phase)
        {
            case LancerGamePhase.Briefing:
                if (action == LancerPlayerAction.NewGame)
                {
                    ResetMission();
                    BroadcastState();
                }
                else if (action == LancerPlayerAction.StartGame)
                {
                    EnterMissionSelect();
                }
                break;

            case LancerGamePhase.MissionSelect:
                if (action == LancerPlayerAction.SelectMission && !string.IsNullOrEmpty(contextId))
                    SelectMission(contextId);
                else if (action == LancerPlayerAction.CampaignReset)
                    ResetCampaignProgress();
                break;

            case LancerGamePhase.LoadoutSelect:
                switch (action)
                {
                    case LancerPlayerAction.SelectLoadout:
                        SelectLoadout(weaponIndex);
                        break;
                    case LancerPlayerAction.ConfirmLoadout:
                        ConfirmLoadout();
                        break;
                }
                break;

            case LancerGamePhase.PreFight:
                switch (action)
                {
                    case LancerPlayerAction.SelectNarrativeCheck:
                        SelectNarrativeCheck(weaponIndex);
                        break;
                    case LancerPlayerAction.BeginCombat:
                        DeployFromPreFight();
                        break;
                }
                break;

            case LancerGamePhase.Intermission:
                switch (action)
                {
                    case LancerPlayerAction.IntermissionRepairStructure:
                        IntermissionRepairStructure();
                        break;
                    case LancerPlayerAction.IntermissionRepairReactor:
                        IntermissionRepairReactor();
                        break;
                    case LancerPlayerAction.ContinueIntermission:
                        ContinueFromIntermission();
                        break;
                }
                break;

            case LancerGamePhase.MissionComplete:
            case LancerGamePhase.CampaignComplete:
                if (action == LancerPlayerAction.ContinueIntermission)
                    ContinueFromMissionComplete();
                break;

            case LancerGamePhase.Victory:
            case LancerGamePhase.Defeat:
                if (action == LancerPlayerAction.NewGame)
                {
                    ResetMission();
                    BroadcastState();
                }
                break;

            case LancerGamePhase.SkillPick:
                switch (action)
                {
                    case LancerPlayerAction.SelectUpgrade:
                        SelectSkill(weaponIndex);
                        break;
                    case LancerPlayerAction.ConfirmUpgrade:
                        ConfirmSkill();
                        break;
                }
                break;

            case LancerGamePhase.Combat when _playerTurn:
                ProcessPlayerCombatAction(action, cell, weaponIndex, targetUnitId, stabilizeOption);
                break;
        }
    }

    public void Tick(float frameTime)
    {
        if (_pendingResolution != null)
        {
            _pendingResolution.Timer -= frameTime;
            if (_pendingResolution.Timer <= 0)
            {
                var apply = _pendingResolution.Apply;
                _pendingResolution = null;
                apply?.Invoke();
                // Resume AI pacing after the roll display finishes.
                if (_processingAi && !_reactionPromptActive && _pendingResolution == null)
                    _aiStepTimer = AiStepDelay;

                TryFireNextBarrageWeapon();
            }

            return;
        }

        if (_reactionPromptActive)
        {
            _reactionTimer -= frameTime;
            if (_reactionTimer <= 0)
                ResolveReaction(false);
            return;
        }

        if (!_processingAi || _phase != LancerGamePhase.Combat)
            return;

        _aiStepTimer -= frameTime;
        if (_aiStepTimer > 0)
            return;

        if (_aiQueue.Count == 0)
        {
            _processingAi = false;
            BeginPlayerTurn();
            return;
        }

        var step = _aiQueue[0];
        _aiQueue.RemoveAt(0);
        ExecuteAiStep(step);
        // If the step queued a dice resolution, Tick will resume AI once it clears.
        if (_pendingResolution == null && !_reactionPromptActive)
            _aiStepTimer = AiStepDelay;

        CheckEndConditions();
        if (_pendingResolution == null)
            BroadcastState();
    }

    private void ProcessPlayerCombatAction(
        LancerPlayerAction action,
        LancerGridCoord? cell,
        int weaponIndex,
        int targetUnitId,
        LancerStabilizeOption stabilizeOption)
    {
        if (!IsTutorialActionAllowed(action, cell, weaponIndex, targetUnitId, stabilizeOption))
            return;

        switch (action)
        {
            case LancerPlayerAction.SelectMove:
                if (_braceRestrictedTurn || _moveRemaining <= 0)
                    return;
                _selectionMode = LancerSelectionMode.Move;
                HighlightReachable(GetPlayerUnit()!.Position, _moveRemaining, ignoreTerrain: false);
                BroadcastState();
                break;

            case LancerPlayerAction.SelectBoost:
                if (_braceRestrictedTurn)
                    return;
                var freeBoost = _coreKind == LancerCoreKind.Raijin && _coreActive && !_freeBoostUsed;
                if (_usedActions.Contains(LancerPlayerAction.SelectBoost) && !freeBoost)
                    return;
                if (!CanSpendQuick() && !freeBoost)
                    return;
                _selectionMode = LancerSelectionMode.Boost;
                HighlightReachable(GetPlayerUnit()!.Position, _playerSpeed, ignoreTerrain: true);
                BroadcastState();
                break;

            case LancerPlayerAction.ConfirmCell when cell != null:
                ConfirmCell(cell);
                break;

            case LancerPlayerAction.Skirmish:
                if (_usedActions.Contains(LancerPlayerAction.Skirmish) || !CanSpendQuick())
                    return;
                _attackIntent = LancerAttackIntent.Skirmish;
                _selectionMode = LancerSelectionMode.WeaponPick;
                _barragePickedWeapons.Clear();
                _selectedWeaponIndex = -1;
                ClearHighlights();
                BroadcastState();
                break;

            case LancerPlayerAction.SelectWeapon:
                if (_selectionMode != LancerSelectionMode.WeaponPick)
                    return;

                HandleWeaponPick(weaponIndex);
                break;

            case LancerPlayerAction.Barrage:
                if (_braceRestrictedTurn || _fullUsed || _quickActionsUsed > 0)
                    return;
                _attackIntent = LancerAttackIntent.Barrage;
                _selectionMode = LancerSelectionMode.WeaponPick;
                _barragePickedWeapons.Clear();
                _selectedWeaponIndex = -1;
                ClearHighlights();
                BroadcastState();
                break;

            case LancerPlayerAction.ConfirmTarget:
                if (_selectionMode == LancerSelectionMode.LockOnTarget)
                {
                    ResolveLockOn(targetUnitId);
                    break;
                }

                if (_barrageWeaponQueue.Count > 0)
                {
                    _barrageCurrentTargetId = targetUnitId;
                    TryFireNextBarrageWeapon();
                    break;
                }

                if (_barragePickedWeapons.Count == 2)
                {
                    StartBarrageSequence(targetUnitId, _barragePickedWeapons[0], _barragePickedWeapons[1]);
                    break;
                }

                if (_selectedWeaponIndex >= 0)
                {
                    if (IsAuxWeapon(_selectedWeaponIndex))
                    {
                        if (!TrySpendQuick(LancerPlayerAction.Skirmish))
                            return;
                        StartAuxSkirmishSequence(targetUnitId);
                    }
                    else
                    {
                        ResolvePlayerWeaponAttack(_selectedWeaponIndex, targetUnitId);
                    }
                }
                break;

            case LancerPlayerAction.LockOn:
                if (_usedActions.Contains(LancerPlayerAction.LockOn) || !CanSpendQuick())
                    return;
                _selectionMode = LancerSelectionMode.LockOnTarget;
                _attackIntent = LancerAttackIntent.None;
                _selectedWeaponIndex = -1;
                _barragePickedWeapons.Clear();
                HighlightLockOnTargets();
                BroadcastState();
                break;

            case LancerPlayerAction.UseSystem when weaponIndex == 0 && _hexCharges > 0:
                // Quick action is spent on confirm (ResolveHex) so cancelling doesn't eat it.
                if (_usedActions.Contains(LancerPlayerAction.UseSystem) || !CanSpendQuick())
                    return;
                _selectionMode = LancerSelectionMode.HexTarget;
                HighlightHexTargets();
                BroadcastState();
                break;

            case LancerPlayerAction.Stabilize:
                if (_selectionMode != LancerSelectionMode.Stabilize)
                {
                    if (_braceRestrictedTurn || _fullUsed || _quickActionsUsed > 0)
                        return;
                    _selectionMode = LancerSelectionMode.Stabilize;
                    ClearHighlights();
                    BroadcastState();
                    return;
                }

                if (stabilizeOption == LancerStabilizeOption.Repair && _repairs <= 0)
                    return;

                if (!TrySpendFull(LancerPlayerAction.Stabilize))
                    return;
                ApplyStabilize(stabilizeOption);
                break;

            case LancerPlayerAction.Disengage:
                if (_braceRestrictedTurn)
                    return;
                if (!TrySpendFull(LancerPlayerAction.Disengage))
                    return;
                _disengaged = true;
                AddLog(Loc.GetString("lancer-arcade-log-disengage"));
                BroadcastState();
                NotifyTutorialEvent(TutorialAdvanceEvent.Disengaged);
                break;

            case LancerPlayerAction.Overcharge:
                if (_braceRestrictedTurn || _overchargeUsed)
                    return;
                _overchargeUsed = true;
                var heatGain = RollOverchargeHeat();
                ApplyHeat(heatGain);
                AddLog(Loc.GetString("lancer-arcade-log-overcharge", ("heat", heatGain)));
                _overchargeHeatStep++;
                BroadcastState();
                NotifyTutorialEvent(TutorialAdvanceEvent.Overcharged);
                break;

            case LancerPlayerAction.ActivateCore when _corePower > 0 && !_coreActive:
                _corePower--;
                _coreActive = true;
                if (_coreKind == LancerCoreKind.MonarchDivine)
                {
                    AddLog(Loc.GetString("lancer-arcade-log-core"));
                    ResolveDivinePunishment();
                }
                else if (_coreKind == LancerCoreKind.TokugawaRadiance)
                {
                    ActivateTokugawaRadiance();
                }
                else if (_coreKind == LancerCoreKind.TortugaSentinel)
                {
                    AddLog(Loc.GetString("lancer-arcade-log-tortuga-sentinel"));
                }
                else
                {
                    // Raijin / Everest Power Up.
                    AddLog(Loc.GetString("lancer-arcade-log-core"));
                }

                BroadcastState();
                break;

            case LancerPlayerAction.EndTurn:
                EndPlayerTurn();
                break;

            case LancerPlayerAction.Cancel:
                _selectionMode = LancerSelectionMode.None;
                _attackIntent = LancerAttackIntent.None;
                _selectedWeaponIndex = -1;
                _barragePickedWeapons.Clear();
                _barrageWeaponQueue.Clear();
                _barrageCurrentTargetId = -1;
                ClearHighlights();
                BroadcastState();
                break;
        }
    }

    private void ConfirmCell(LancerGridCoord cell)
    {
        // Cell comes straight from the client; never index the grid with it unchecked.
        if (!LancerHex.InBounds(cell))
            return;

        var player = GetPlayerUnit();
        if (player == null)
            return;

        if (_selectionMode == LancerSelectionMode.Move || _selectionMode == LancerSelectionMode.Boost)
        {
            var maxDist = _selectionMode == LancerSelectionMode.Boost ? _playerSpeed : _moveRemaining;
            var ignoreTerrain = _selectionMode == LancerSelectionMode.Boost;
            if (!IsReachable(player.Position, cell, maxDist, ignoreTerrain))
                return;

            var from = new LancerGridCoord(player.Position.X, player.Position.Y);
            var distance = LancerHex.Distance(from, cell);
            player.Position = cell;

            if (_selectionMode == LancerSelectionMode.Move)
            {
                _moveRemaining -= distance;
                MarkActionUsed(LancerPlayerAction.SelectMove);
            }
            else
            {
                if (_coreKind == LancerCoreKind.Raijin && _coreActive && !_freeBoostUsed)
                {
                    _freeBoostUsed = true;
                    MarkActionUsed(LancerPlayerAction.SelectBoost);
                }
                else if (!TrySpendQuick(LancerPlayerAction.SelectBoost))
                {
                    // Restore position if spend failed (shouldn't happen after precheck).
                    player.Position = from;
                    return;
                }
            }

            _selectionMode = LancerSelectionMode.None;
            ClearHighlights();
            AddLog(Loc.GetString("lancer-arcade-log-move", ("coord", FormatCoord(cell))));
            BroadcastState();
            CheckEndConditions();
            OnTutorialPlayerMoved(cell);
            return;
        }

        if (_selectionMode == LancerSelectionMode.HexTarget)
        {
            if (!IsValidHexTarget(cell))
                return;

            ResolveHex(cell);
        }
    }

    private void EndPlayerTurn()
    {
        TrackHoldObjective();
        _playerTurn = false;
        _selectionMode = LancerSelectionMode.None;
        _attackIntent = LancerAttackIntent.None;
        _barragePickedWeapons.Clear();
        _barrageWeaponQueue.Clear();
        _barrageCurrentTargetId = -1;
        ClearHighlights();
        _holdAndLock = false;
        Array.Clear(_weaponUsedThisTurn, 0, _weaponUsedThisTurn.Length);

        // Brace lasting effects end at the end of your next turn (the restricted turn).
        if (_braceRestrictedTurn)
        {
            _braceRestrictedTurn = false;
            _braceReactionLockout = false;
            _braceDefenseActive = false;
            AddLog(Loc.GetString("lancer-arcade-log-brace-end"));
        }

        if (_tutorialActive)
        {
            AddLog(Loc.GetString("lancer-arcade-log-enemy-turn"));
            var tutorialKind = CurrentTutorialStep().Kind;
            OnTutorialEndTurn();
            if (!_tutorialActive)
                return;

            // EndTurn lesson advances immediately. Overwatch/Finish use scripted or empty AI.
            if (tutorialKind == TutorialStepKind.EndTurn || ShouldSkipNormalEnemyAi())
            {
                BroadcastState();
                return;
            }
        }

        QueueEnemyTurns();
        AddLog(Loc.GetString("lancer-arcade-log-enemy-turn"));
        BroadcastState();
    }

    private void BeginPlayerTurn()
    {
        if (_phase is LancerGamePhase.MissionComplete or LancerGamePhase.CampaignComplete or LancerGamePhase.MissionSelect or LancerGamePhase.LoadoutSelect or LancerGamePhase.PreFight or LancerGamePhase.SkillPick or LancerGamePhase.Intermission)
            return;

        _playerTurn = true;

        // Brace aftermath: this turn is one-quick only; clear lockout/defense at end of this turn.
        _braceRestrictedTurn = _braceRestrictedPending;
        _braceRestrictedPending = false;

        ClearTurnEconomy();
        if (_bonusMoveFirstTurn > 0 && !_braceRestrictedTurn)
        {
            _moveRemaining += _bonusMoveFirstTurn;
            _bonusMoveFirstTurn = 0;
        }

        _disengaged = false;
        _freeBoostUsed = false;
        _overwatchUsesThisRound = 0;
        _braceUsedThisRound = false;
        _reactionUsedThisActivation = false;
        _nuclearCavalierUsedThisTurn = false;
        _deepWellHeatResistThisTurn = false;

        if (_braceRestrictedTurn)
            AddLog(Loc.GetString("lancer-arcade-log-brace-restricted"));

        TickTokugawaExposed();
        foreach (var unit in _units)
            unit.Hunkered = false;

        if (_hasDeepWellHeatSink && IsInDangerZone())
        {
            _deepWellHeatResistThisTurn = true;
            AddLog(Loc.GetString("lancer-arcade-log-deep-well"));
        }

        if (_playerImpaired)
            AddLog(Loc.GetString("lancer-arcade-log-impaired"));
        if (_playerExposed)
            AddLog(Loc.GetString("lancer-arcade-log-exposed"));
        if (_hasNuclearCavalier && IsInDangerZone())
            AddLog(Loc.GetString("lancer-arcade-log-nuclear-cavalier-ready"));

        if (_reactorMeltdownPending)
        {
            TriggerReactorMeltdown();
            return;
        }

        AddLog(Loc.GetString("lancer-arcade-log-player-turn"));
        BroadcastState();
    }

    private void ClearTurnEconomy()
    {
        _moveRemaining = _holdAndLock || _braceRestrictedTurn ? 0 : _playerSpeed;
        _quickActionsUsed = 0;
        _fullUsed = false;
        _overchargeUsed = false;
        _overchargeQuickUsed = false;
        _usedActions.Clear();
    }

    private void CheckEndConditions()
    {
        if (_phase != LancerGamePhase.Combat)
            return;

        if (!_tutorialActive && _encounter?.HasRelay != false)
        {
            var relay = GetRelay();
            if (relay is { Destroyed: false, Hp: <= 0 } || relay is { Destroyed: true })
            {
                OnFightLost();
                return;
            }
        }

        if (_playerHp <= 0 && _structure <= 0)
        {
            OnFightLost();
            return;
        }

        if (IsObjectiveWinMet())
            OnFightWon();
    }
}
