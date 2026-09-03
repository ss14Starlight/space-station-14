using Content.Server._Starlight.Arcade.Systems;
using Content.Shared._Starlight.Arcade.Lancer;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Arcade.Lancer;

public sealed partial class LancerGame
{
    private string? _selectedMissionId;
    private int _fightIndex;
    private LancerEncounterPrototype? _encounter;
    private int _narrativeChoiceIndex = -1;
    private bool _narrativeCheckFailed;
    private int _holdTurnsCompleted;
    private int _fightAccBonus;
    private int _bonusHexCharges;
    private int _bonusMoveFirstTurn;
    private int _tempStructureBonus;
    private string _missionCompleteText = string.Empty;
    private bool _pendingCampaignWin;

    private void ResetCampaignRun()
    {
        _selectedMissionId = null;
        _fightIndex = 0;
        _encounter = null;
        _narrativeChoiceIndex = -1;
        _narrativeCheckFailed = false;
        _holdTurnsCompleted = 0;
        _fightAccBonus = 0;
        _bonusHexCharges = 0;
        _bonusMoveFirstTurn = 0;
        _tempStructureBonus = 0;
        _missionCompleteText = string.Empty;
        _pendingChoiceIndex = -1;
        _selectedLoadoutId = null;
        _pendingCampaignWin = false;
        ClearTutorialState();
    }

    private void ResetRunMechState()
    {
        ApplyLoadoutAndSkills(GetArcade());
        _playerHp = _playerMaxHp;
        _structure = _playerMaxStructure;
        _stress = PlayerMaxStress;
        _heat = 0;
        _repairs = _playerRepairCap;
        _hexCharges = _playerHexCap;
        _corePower = 0;
        _coreActive = false;
        _disengaged = false;
        _holdAndLock = false;
        _playerImpaired = false;
        _playerExposed = false;
        _exposedTurnsRemaining = 0;
        _nuclearCavalierUsedThisTurn = false;
        _externalBatteriesDestroyed = false;
        _deepWellHeatResistThisTurn = false;
        _overchargeHeatStep = 0;
        _overwatchUsesThisRound = 0;
        _braceUsedThisRound = false;
        _reactionUsedThisActivation = false;
        _braceReactionLockout = false;
        _braceRestrictedPending = false;
        _braceRestrictedTurn = false;
        _braceDefenseActive = false;
        _reactorMeltdownPending = false;
        _hyperReflexImmobilizeUnitId = -1;
        ResetWeaponLoadedState();
    }

    private LancerArcadeComponent? GetArcade()
    {
        return _entityManager.TryGetComponent(_owner, out LancerArcadeComponent? comp) ? comp : null;
    }

    private void NotifyArcadeResult(ArcadeGameResult result)
    {
        var player = GetArcade()?.Player;
        switch (result)
        {
            case ArcadeGameResult.Win:
                _arcade.WinGame(player, _owner);
                break;
            case ArcadeGameResult.Loss:
                _arcade.LoseGame(player, _owner);
                break;
        }
    }

    /// <summary>
    /// Soft session reset: unload the current run/combat and return to mission select.
    /// Preserves cabinet campaign progress (skills, cleared/unlocked missions).
    /// </summary>
    /// <param name="clearLog">When true, wipe the combat log.</param>
    public void ReturnToMissionSelect(bool clearLog = false)
    {
        if (clearLog)
            _log.Clear();

        if (_phase == LancerGamePhase.MissionSelect && !_tutorialActive && _pendingResolution == null && !_reactionPromptActive)
            return;

        EnterMissionSelect();
    }

    /// <summary>
    /// Soft session reset to the opening credit / disclaimer screen.
    /// Preserves cabinet campaign progress (skills, cleared/unlocked missions).
    /// </summary>
    public void ReturnToIntro()
    {
        CancelInFlightSession();
        ResetMission();
        BroadcastState();
    }

    private void EnterMissionSelect()
    {
        CancelInFlightSession();
        ResetCampaignRun();
        ClearCombatState();
        _phase = LancerGamePhase.MissionSelect;
        AddLog(Loc.GetString("lancer-arcade-log-mission-select"));
        BroadcastState();
    }

    /// <summary>
    /// Cancels mid-combat timers/prompts so a soft reset cannot leave dangling AI or dice work.
    /// </summary>
    private void CancelInFlightSession()
    {
        _pendingResolution = null;
        _pendingReactionContinue = null;
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
        _aiQueue.Clear();
    }

    private void ResetCampaignProgress()
    {
        var arcade = GetArcade();
        arcade?.ResetCampaign();
        EnterMissionSelect();
        AddLog(Loc.GetString("lancer-arcade-log-campaign-reset"));
    }

    private void ClearCombatState()
    {
        _units.Clear();
        _combatStarted = false;
        _spotBonus = false;
        _spotFailed = false;
        _spotRoll = 0;
        _spotRolled = false;
        _firstPlayerAttackBonus = false;
        ClearTurnEconomy();
        ClearTransientCombatSelections();
        InitEmptyGrid();
    }

    /// <summary>
    /// Clears selection/AI state that must not leak out of an ended fight
    /// (e.g. a barrage queue surviving into the next fight would grant free attacks).
    /// </summary>
    private void ClearTransientCombatSelections()
    {
        _selectionMode = LancerSelectionMode.None;
        _attackIntent = LancerAttackIntent.None;
        _selectedWeaponIndex = -1;
        _barragePickedWeapons.Clear();
        _barrageWeaponQueue.Clear();
        _barrageCurrentTargetId = -1;
        _aiQueue.Clear();
        _processingAi = false;
        Array.Clear(_weaponUsedThisTurn, 0, _weaponUsedThisTurn.Length);
        ClearHighlights();
    }

    private void InitEmptyGrid()
    {
        _cells = new LancerCellState[GridSize][];
        for (var y = 0; y < GridSize; y++)
        {
            _cells[y] = new LancerCellState[GridSize];
            for (var x = 0; x < GridSize; x++)
            {
                _cells[y][x] = new LancerCellState
                {
                    Terrain = LancerTerrainType.Open,
                    Highlight = LancerCellHighlight.None
                };
            }
        }
    }

    private void SelectMission(string missionId)
    {
        if (!_prototypes.TryIndex(missionId, out LancerMissionPrototype? mission))
            return;

        if (missionId == MissionTutorial)
        {
            StartTutorial();
            return;
        }

        var arcade = GetArcade();
        if (arcade == null || !arcade.UnlockedMissionIds.Contains(missionId))
            return;

        if (mission.Encounters.Count == 0)
            return;

        if (!MissionLoadoutPairs.ContainsKey(missionId))
            return;

        _selectedMissionId = missionId;
        _fightIndex = 0;
        _pendingChoiceIndex = -1;
        _selectedLoadoutId = null;
        _phase = LancerGamePhase.LoadoutSelect;
        AddLog(Loc.GetString("lancer-arcade-log-mission-start", ("name", Loc.GetString(mission.Name))));
        BroadcastState();
    }

    private void SelectLoadout(int index)
    {
        if (_phase != LancerGamePhase.LoadoutSelect || _selectedMissionId == null)
            return;

        var options = GetMissionLoadoutOptions(_selectedMissionId);
        if (index < 0 || index >= options.Length)
            return;

        _pendingChoiceIndex = index;
        BroadcastState();
    }

    private void ConfirmLoadout()
    {
        if (_phase != LancerGamePhase.LoadoutSelect || _selectedMissionId == null)
            return;

        var options = GetMissionLoadoutOptions(_selectedMissionId);
        if (_pendingChoiceIndex < 0 || _pendingChoiceIndex >= options.Length)
            return;

        if (!_prototypes.TryIndex(_selectedMissionId, out LancerMissionPrototype? mission))
            return;

        _selectedLoadoutId = options[_pendingChoiceIndex];
        _pendingChoiceIndex = -1;
        ResetRunMechState();
        LoadEncounter(GetMissionEncounterId(mission, _selectedLoadoutId, 0));
        EnterPreFight();
    }

    private static string[] GetMissionLoadoutOptions(string missionId) =>
        MissionLoadoutPairs.TryGetValue(missionId, out var pair) ? pair : [];

    /// <summary>Resolves the encounter list for the active loadout (mech-scaled variants).</summary>
    private static List<ProtoId<LancerEncounterPrototype>> GetMissionEncounters(
        LancerMissionPrototype mission,
        string? loadoutId)
    {
        if (loadoutId != null
            && mission.LoadoutEncounters.TryGetValue(loadoutId, out var overrideList)
            && overrideList.Count > 0)
            return overrideList;

        return mission.Encounters;
    }

    private static ProtoId<LancerEncounterPrototype> GetMissionEncounterId(
        LancerMissionPrototype mission,
        string? loadoutId,
        int fightIndex)
    {
        var list = GetMissionEncounters(mission, loadoutId);
        return list[Math.Clamp(fightIndex, 0, list.Count - 1)];
    }

    private void LoadEncounter(ProtoId<LancerEncounterPrototype> encounterId)
    {
        _encounter = _prototypes.Index(encounterId);
        _holdTurnsCompleted = 0;
        _narrativeChoiceIndex = -1;
        _narrativeCheckFailed = false;
        _spotRolled = false;
        _spotRoll = 0;
        _spotBonus = false;
        _spotFailed = false;
        _fightAccBonus = 0;
        _bonusHexCharges = 0;
        _bonusMoveFirstTurn = 0;
        _tempStructureBonus = 0;
        _firstPlayerAttackBonus = false;
    }

    private void LoadEncounterTerrain(LancerEncounterPrototype enc)
    {
        InitEmptyGrid();

        foreach (var entry in enc.Terrains)
            SetTerrain(new LancerGridCoord(entry.X, entry.Y), entry.Terrain);

        if (enc.HasRelay)
            SetTerrain(new LancerGridCoord(enc.RelayX, enc.RelayY), LancerTerrainType.Relay);
    }

    private void EnterPreFight()
    {
        if (_encounter == null || _selectedMissionId == null)
            return;

        _phase = LancerGamePhase.PreFight;
        BroadcastState();
    }

    private void SelectNarrativeCheck(int index)
    {
        if (_phase != LancerGamePhase.PreFight || _encounter == null || _spotRolled)
            return;

        if (index < 0 || index >= _encounter.NarrativeChecks.Count)
            return;

        var check = _encounter.NarrativeChecks[index];
        if (!IsNarrativeBonusUseful(check))
            return;

        _narrativeChoiceIndex = index;
        BroadcastState();
    }

    private void DeployFromPreFight()
    {
        if (_phase != LancerGamePhase.PreFight || _narrativeChoiceIndex < 0 || _spotRolled || _pendingResolution != null)
            return;

        RollNarrativeCheck(StartCombat);
    }

    private void EnterIntermission()
    {
        ClearTransientCombatSelections();
        _fightIndex++;
        _phase = LancerGamePhase.Intermission;
        var total = GetCurrentMissionFightCount();
        AddLog(Loc.GetString("lancer-arcade-log-intermission", ("fight", _fightIndex), ("total", total)));
        PlayArcadeSound("win");
        BroadcastState();
    }

    private int GetCurrentMissionFightCount()
    {
        if (_selectedMissionId == null || !_prototypes.TryIndex(_selectedMissionId, out LancerMissionPrototype? mission))
            return 3;

        return GetMissionEncounters(mission, _selectedLoadoutId).Count;
    }

    private void ContinueFromIntermission()
    {
        if (_phase != LancerGamePhase.Intermission || _selectedMissionId == null)
            return;

        if (!_prototypes.TryIndex(_selectedMissionId, out LancerMissionPrototype? mission))
            return;

        var encounters = GetMissionEncounters(mission, _selectedLoadoutId);
        if (_fightIndex >= encounters.Count)
            return;

        LoadEncounter(encounters[_fightIndex]);
        EnterPreFight();
    }

    private void CompleteMission()
    {
        ClearTransientCombatSelections();

        if (_selectedMissionId == null)
            return;

        var arcade = GetArcade();
        if (arcade == null)
            return;

        if (!_prototypes.TryIndex(_selectedMissionId, out LancerMissionPrototype? mission))
            return;

        var firstClear = arcade.ClearedMissionIds.Add(_selectedMissionId);

        if (firstClear)
        {
            if (mission.UnlocksMission != null)
                arcade.UnlockedMissionIds.Add(mission.UnlocksMission);

            arcade.LicenseLevel = Math.Min(3, arcade.ClearedMissionIds.Count);
        }

        AddLog(Loc.GetString("lancer-arcade-log-mission-complete", ("name", Loc.GetString(mission.Name))));

        _pendingCampaignWin = firstClear
                              && _selectedMissionId == "crown-signal"
                              && !arcade.CampaignCompleted;

        _pendingChoiceIndex = -1;
        _phase = LancerGamePhase.SkillPick;
        PlayArcadeSound("win");
        BroadcastState();
    }

    private void SelectSkill(int index)
    {
        if (_phase != LancerGamePhase.SkillPick)
            return;

        var options = BuildSkillOptions();
        if (index < 0 || index >= options.Count)
            return;

        _pendingChoiceIndex = index;
        BroadcastState();
    }

    private void ConfirmSkill()
    {
        if (_phase != LancerGamePhase.SkillPick)
            return;

        var arcade = GetArcade();
        if (arcade == null)
            return;

        var options = BuildSkillOptions();
        if (_pendingChoiceIndex < 0 || _pendingChoiceIndex >= options.Count)
            return;

        var choice = options[_pendingChoiceIndex];
        switch (choice.Id)
        {
            case SkillHull:
                arcade.Hull++;
                break;
            case SkillAgility:
                arcade.Agility++;
                break;
            case SkillEngineering:
                arcade.Engineering++;
                break;
        }

        AddLog(Loc.GetString("lancer-arcade-log-skill-chosen", ("name", Loc.GetString(choice.LabelLoc))));

        if (_pendingCampaignWin)
        {
            _pendingCampaignWin = false;
            arcade.CampaignCompleted = true;
            _missionCompleteText = Loc.GetString("lancer-arcade-campaign-complete-body");
            _phase = LancerGamePhase.CampaignComplete;
            PlayArcadeSound("win");
            BroadcastState();
            NotifyArcadeResult(ArcadeGameResult.Win);
            return;
        }

        EnterMissionSelect();
    }

    private void ContinueFromMissionComplete()
    {
        if (_phase is LancerGamePhase.MissionComplete or LancerGamePhase.CampaignComplete)
            EnterMissionSelect();
    }

    private sealed class ChoiceOptionDef
    {
        public required string Id;
        public required string LabelLoc;
        public required string DescriptionLoc;
    }

    private static List<ChoiceOptionDef> BuildSkillOptions() =>
    [
        new() { Id = SkillHull, LabelLoc = "lancer-skill-hull-label", DescriptionLoc = "lancer-skill-hull-desc" },
        new() { Id = SkillAgility, LabelLoc = "lancer-skill-agility-label", DescriptionLoc = "lancer-skill-agility-desc" },
        new() { Id = SkillEngineering, LabelLoc = "lancer-skill-engineering-label", DescriptionLoc = "lancer-skill-engineering-desc" },
    ];

    private LancerSkillPickState? BuildSkillPickState()
    {
        var arcade = GetArcade();
        var options = BuildSkillOptions();
        var states = new LancerUpgradeOptionState[options.Count];
        for (var i = 0; i < options.Count; i++)
        {
            var opt = options[i];
            states[i] = new LancerUpgradeOptionState
            {
                Id = opt.Id,
                Label = Loc.GetString(opt.LabelLoc),
                Description = Loc.GetString(opt.DescriptionLoc),
                Selected = _pendingChoiceIndex == i,
                Pickable = _pendingChoiceIndex < 0 || _pendingChoiceIndex == i
            };
        }

        return new LancerSkillPickState
        {
            Title = Loc.GetString("lancer-arcade-skillpick-title"),
            Hull = arcade?.Hull ?? 0,
            Agility = arcade?.Agility ?? 0,
            Engineering = arcade?.Engineering ?? 0,
            Options = states,
            CanConfirm = _pendingChoiceIndex >= 0
        };
    }

    private LancerLoadoutSelectState? BuildLoadoutSelectState()
    {
        if (_selectedMissionId == null || !_prototypes.TryIndex(_selectedMissionId, out LancerMissionPrototype? mission))
            return null;

        var optionIds = GetMissionLoadoutOptions(_selectedMissionId);
        var states = new LancerUpgradeOptionState[optionIds.Length];
        for (var i = 0; i < optionIds.Length; i++)
        {
            if (!MissionLoadouts.TryGetValue(optionIds[i], out var loadout))
                continue;

            states[i] = new LancerUpgradeOptionState
            {
                Id = loadout.Id,
                Label = Loc.GetString(loadout.NameLoc),
                Description = Loc.GetString(loadout.DescriptionLoc),
                Selected = _pendingChoiceIndex == i,
                Pickable = _pendingChoiceIndex < 0 || _pendingChoiceIndex == i
            };
        }

        return new LancerLoadoutSelectState
        {
            Title = Loc.GetString("lancer-arcade-loadout-select-title"),
            MissionName = Loc.GetString(mission.Name),
            Options = states,
            CanConfirm = _pendingChoiceIndex >= 0
        };
    }

    private void IntermissionRepairStructure()
    {
        if (_phase != LancerGamePhase.Intermission || _repairs <= 0 || _structure >= _playerMaxStructure)
            return;

        _repairs--;
        _structure = Math.Min(_playerMaxStructure, _structure + 1);
        AddLog(Loc.GetString("lancer-arcade-log-intermission-structure"));
        BroadcastState();
    }

    private void IntermissionRepairReactor()
    {
        if (_phase != LancerGamePhase.Intermission || _repairs <= 0 || _playerHp >= _playerMaxHp)
            return;

        _repairs--;
        _playerHp = _playerMaxHp;
        AddLog(Loc.GetString("lancer-arcade-log-intermission-reactor"));
        BroadcastState();
    }

    private void ApplyNarrativeBonus(LancerNarrativeCheckEntry check)
    {
        switch (check.Bonus)
        {
            case LancerNarrativeBonusKind.FirstAttackAcc:
                _firstPlayerAttackBonus = true;
                _spotBonus = true;
                break;
            case LancerNarrativeBonusKind.FreeRepair:
                _repairs = Math.Min(_playerRepairCap, _repairs + check.BonusValue);
                break;
            case LancerNarrativeBonusKind.CoreCharge:
                _corePower += check.BonusValue;
                break;
            case LancerNarrativeBonusKind.ExtraMove:
                _bonusMoveFirstTurn += check.BonusValue;
                break;
            case LancerNarrativeBonusKind.ExtraHex:
                // Fight-only bonus; applied on top of the base pool and not capped at PlayerHexCharges.
                _bonusHexCharges += check.BonusValue;
                _hexCharges += check.BonusValue;
                break;
            case LancerNarrativeBonusKind.FightAcc:
                _fightAccBonus += check.BonusValue;
                break;
            case LancerNarrativeBonusKind.TempStructure:
                _tempStructureBonus += check.BonusValue;
                _structure = Math.Min(_playerMaxStructure, _structure + check.BonusValue);
                break;
        }
    }

    private void PrepareFightResources()
    {
        _heat = 0;
        _hexCharges = _playerHexCap + _bonusHexCharges;
        _disengaged = false;
        _holdAndLock = false;
        _coreActive = false;
        _playerImpaired = false;
        _playerExposed = false;
        _exposedTurnsRemaining = 0;
        _nuclearCavalierUsedThisTurn = false;
        // External Batteries persist destroyed across fights in a mission until full repair / new loadout.
        _deepWellHeatResistThisTurn = false;
        _overchargeHeatStep = 0;
        _overwatchUsesThisRound = 0;
        _braceUsedThisRound = false;
        _reactionUsedThisActivation = false;
        _braceReactionLockout = false;
        _braceRestrictedPending = false;
        _braceRestrictedTurn = false;
        _braceDefenseActive = false;
        _reactorMeltdownPending = false;
        _hyperReflexImmobilizeUnitId = -1;
        ResetWeaponLoadedState();
        ClearTurnEconomy();
        ClearTransientCombatSelections();
    }

    private void OnFightLost()
    {
        if (_tutorialActive)
        {
            CompleteTutorial(aborted: true);
            return;
        }

        AddLog(Loc.GetString("lancer-arcade-log-fight-defeat"));
        PlayArcadeSound("gameover");
        NotifyArcadeResult(ArcadeGameResult.Loss);
        EnterMissionSelect();
    }

    private void OnFightWon()
    {
        if (_tutorialActive)
        {
            NotifyTutorialEvent(TutorialAdvanceEvent.AllHostilesDestroyed);
            return;
        }

        var totalFights = GetCurrentMissionFightCount();
        if (_fightIndex < totalFights - 1)
            EnterIntermission();
        else
            CompleteMission();
    }

    private bool IsObjectiveWinMet()
    {
        // Mid-tutorial lessons often destroy the demo enemy; only the Finish step may win.
        if (_tutorialActive && CurrentTutorialStep().Kind != TutorialStepKind.Finish)
            return false;

        if (_encounter == null)
            return AllEnemiesGone();

        return _encounter.Objective switch
        {
            LancerObjectiveKind.EliminateAll => AllEnemiesGone(),
            LancerObjectiveKind.DefendRelay => AllEnemiesGone(),
            LancerObjectiveKind.ReachCell => IsReachObjectiveMet(),
            LancerObjectiveKind.HoldCell => _holdTurnsCompleted >= _encounter.HoldTurnsRequired,
            _ => AllEnemiesGone()
        };
    }

    private bool IsReachObjectiveMet()
    {
        var player = GetPlayerUnit();
        if (player == null || _encounter == null)
            return false;

        return player.Position.X == _encounter.ObjectiveX && player.Position.Y == _encounter.ObjectiveY;
    }

    private void TrackHoldObjective()
    {
        if (_encounter?.Objective != LancerObjectiveKind.HoldCell)
            return;

        var player = GetPlayerUnit();
        if (player == null)
            return;

        if (player.Position.X == _encounter.ObjectiveX && player.Position.Y == _encounter.ObjectiveY)
            _holdTurnsCompleted++;
        else
            _holdTurnsCompleted = 0;
    }

    private LancerMissionSelectState BuildMissionSelectState()
    {
        var arcade = GetArcade();
        var entries = new List<LancerMissionEntryState>();

        foreach (var mission in _prototypes.EnumeratePrototypes<LancerMissionPrototype>())
        {
            var isTutorial = mission.ID == MissionTutorial;
            var locked = !isTutorial
                         && mission.UnlocksAfter != null
                         && (arcade == null || !arcade.ClearedMissionIds.Contains(mission.UnlocksAfter));
            if (!isTutorial && arcade != null && !arcade.UnlockedMissionIds.Contains(mission.ID))
                locked = true;

            var cleared = !isTutorial && (arcade?.ClearedMissionIds.Contains(mission.ID) ?? false);

            entries.Add(new LancerMissionEntryState
            {
                Id = mission.ID,
                Name = Loc.GetString(mission.Name),
                Description = Loc.GetString(mission.Description),
                Locked = locked,
                Cleared = cleared
            });
        }

        // Tutorial first, then campaign unlock order (mission 1 → 2 → 3).
        entries.Sort((a, b) => GetMissionCampaignOrder(a.Id).CompareTo(GetMissionCampaignOrder(b.Id)));
        return new LancerMissionSelectState
        {
            LicenseLevel = arcade?.LicenseLevel ?? 0,
            Hull = arcade?.Hull ?? 0,
            Agility = arcade?.Agility ?? 0,
            Engineering = arcade?.Engineering ?? 0,
            ChassisSummary = GetCampaignSummary(arcade),
            LoadoutSummary = Loc.GetString("lancer-arcade-skills-summary",
                ("hull", arcade?.Hull ?? 0),
                ("agility", arcade?.Agility ?? 0),
                ("engineering", arcade?.Engineering ?? 0)),
            CanResetCampaign = arcade != null && (arcade.LicenseLevel > 0
                                                  || arcade.ClearedMissionIds.Count > 0
                                                  || arcade.Hull > 0
                                                  || arcade.Agility > 0
                                                  || arcade.Engineering > 0),
            Missions = entries.ToArray()
        };
    }

    /// <summary>Depth in the UnlocksAfter chain (-1 = tutorial, 0 = starting mission).</summary>
    private int GetMissionCampaignOrder(string missionId)
    {
        if (missionId == MissionTutorial)
            return -1;

        if (!_prototypes.TryIndex(missionId, out LancerMissionPrototype? mission))
            return int.MaxValue;

        var depth = 0;
        var guard = 0;
        while (mission.UnlocksAfter != null && guard++ < 16)
        {
            depth++;
            if (!_prototypes.TryIndex(mission.UnlocksAfter, out mission))
                break;
        }

        return depth;
    }

    private LancerPreFightState? BuildPreFightState()
    {
        if (_encounter == null || _selectedMissionId == null)
            return null;

        if (!_prototypes.TryIndex(_selectedMissionId, out LancerMissionPrototype? mission))
            return null;

        var checks = new LancerNarrativeCheckState[_encounter.NarrativeChecks.Count];
        for (var i = 0; i < _encounter.NarrativeChecks.Count; i++)
        {
            var check = _encounter.NarrativeChecks[i];
            var useful = IsNarrativeBonusUseful(check);
            checks[i] = new LancerNarrativeCheckState
            {
                Label = Loc.GetString(check.Label),
                Description = Loc.GetString(check.Description),
                Selected = _narrativeChoiceIndex == i,
                Pickable = useful
                    && (_narrativeChoiceIndex < 0 || _narrativeChoiceIndex == i)
                    && !_spotRolled
                    && _pendingResolution == null
            };
        }

        return new LancerPreFightState
        {
            FightNumber = _fightIndex + 1,
            FightCount = GetMissionEncounters(mission, _selectedLoadoutId).Count,
            MissionName = Loc.GetString(mission.Name),
            FightDescription = Loc.GetString(_encounter.Description),
            ObjectiveText = Loc.GetString(_encounter.ObjectiveText),
            NarrativeChecks = checks,
            CanDeploy = _narrativeChoiceIndex >= 0 && !_spotRolled && _pendingResolution == null
        };
    }

    private bool IsNarrativeBonusUseful(LancerNarrativeCheckEntry check) =>
        check.Bonus switch
        {
            LancerNarrativeBonusKind.FreeRepair => _repairs < _playerRepairCap,
            LancerNarrativeBonusKind.TempStructure => _structure < _playerMaxStructure,
            _ => true
        };

    private LancerIntermissionState? BuildIntermissionState()
    {
        if (_selectedMissionId == null || !_prototypes.TryIndex(_selectedMissionId, out LancerMissionPrototype? mission))
            return null;

        return new LancerIntermissionState
        {
            FightNumber = _fightIndex,
            FightCount = GetMissionEncounters(mission, _selectedLoadoutId).Count,
            Repairs = _repairs,
            Structure = _structure,
            MaxStructure = _playerMaxStructure,
            Hp = _playerHp,
            MaxHp = _playerMaxHp,
            CanRepairStructure = _repairs > 0 && _structure < _playerMaxStructure,
            CanRepairReactor = _repairs > 0 && _playerHp < _playerMaxHp,
            RepairCap = _playerRepairCap
        };
    }
}
