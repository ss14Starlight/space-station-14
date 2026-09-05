using Content.Shared._Starlight.Arcade.Lancer;
using Content.Shared._Starlight.Arcade.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using System.Linq;

namespace Content.Server._Starlight.Arcade.Lancer;

public sealed partial class LancerGame
{
    public void UpdateNewPlayerUi(EntityUid actor)
    {
        foreach (var line in _log)
            SendLog(line, actor);

        SendState(actor);
    }

    private void BroadcastState()
    {
        SendState();
    }

    private const int MaxLogLines = 100;

    private void AddLog(string line)
    {
        _log.Add(line);
        if (_log.Count > MaxLogLines)
            _log.RemoveAt(0);

        SendLog(line);
        SendState();
    }

    private void SendLog(string line, EntityUid? actor = null)
    {
        var msg = new LancerArcadeMessages.LancerLogMessage(line);
        if (actor != null)
            _uiSystem.ServerSendUiMessage(_owner, LancerArcadeUiKey.Key, msg, actor.Value);
        else
            _uiSystem.ServerSendUiMessage(_owner, LancerArcadeUiKey.Key, msg);
    }

    private void SendState(EntityUid? actor = null)
    {
        var snapshot = BuildSnapshot();
        var msg = new LancerArcadeMessages.LancerGameStateMessage(snapshot);
        if (actor != null)
            _uiSystem.ServerSendUiMessage(_owner, LancerArcadeUiKey.Key, msg, actor.Value);
        else
            _uiSystem.ServerSendUiMessage(_owner, LancerArcadeUiKey.Key, msg);
    }

    private void SendReactionPrompt(LancerReactionType reaction, float timeout, int pendingDamage = 0)
    {
        _uiSystem.ServerSendUiMessage(_owner, LancerArcadeUiKey.Key,
            new LancerArcadeMessages.LancerReactionPromptMessage(reaction, timeout, pendingDamage));
    }

    private void SendDiceRoll(LancerArcadeMessages.LancerDiceRollMessage msg)
    {
        _uiSystem.ServerSendUiMessage(_owner, LancerArcadeUiKey.Key, msg);
    }

    private void SendAttackEffect(LancerGridCoord cell, LancerAttackEffectKind kind, LancerGridCoord? from = null)
    {
        _uiSystem.ServerSendUiMessage(_owner, LancerArcadeUiKey.Key,
            new LancerArcadeMessages.LancerAttackEffectMessage(cell, kind, from));
    }

    private void PlayArcadeSound(string kind)
    {
        var path = kind switch
        {
            "newgame" => "/Audio/Effects/Arcade/newgame.ogg",
            "attack" => "/Audio/Effects/Arcade/player_attack.ogg",
            "win" => "/Audio/Effects/Arcade/win.ogg",
            "gameover" => "/Audio/Effects/Arcade/gameover.ogg",
            _ => null
        };

        if (path == null)
            return;

        _audio.PlayPvs(new SoundPathSpecifier(path),
            _owner,
            AudioParams.Default.WithVolume(SharedArcadeSystem.ArcadeSoundVolumeDb));
    }

    private LancerGameStateSnapshot BuildSnapshot()
    {
        var cells = new LancerCellState[GridSize][];
        for (var y = 0; y < GridSize; y++)
        {
            cells[y] = new LancerCellState[GridSize];
            for (var x = 0; x < GridSize; x++)
            {
                cells[y][x] = new LancerCellState
                {
                    Terrain = _cells[y][x].Terrain,
                    Highlight = _cells[y][x].Highlight
                };
            }
        }

        var units = _units
            .Where(u => !u.Destroyed && !u.Fleeing)
            .Select(u => new LancerUnitState
            {
                Id = u.Id,
                Kind = u.Kind,
                Position = new LancerGridCoord(u.Position.X, u.Position.Y),
                Hp = u.Kind == LancerUnitKind.PlayerMech ? _playerHp : u.Hp,
                MaxHp = u.Kind == LancerUnitKind.PlayerMech ? _playerMaxHp : u.MaxHp,
                Armor = u.Kind == LancerUnitKind.PlayerMech ? _playerArmor : u.Armor,
                Destroyed = u.Destroyed,
                LockedOn = u.LockedOn,
                Shredded = u.Shredded,
                Impaired = u.Kind == LancerUnitKind.PlayerMech ? _playerImpaired : u.Impaired,
                SpriteState = !string.IsNullOrEmpty(u.SpriteState)
                    ? u.SpriteState
                    : GetUnitSprite(u.Kind, _playerSprite)
            })
            .ToArray();

        return new LancerGameStateSnapshot
        {
            Phase = _phase,
            Cells = cells,
            Units = units,
            MechPanel = BuildMechPanel(),
            ActionEconomy = new LancerActionEconomyState
            {
                MoveRemaining = _moveRemaining,
                MoveMax = _playerSpeed,
                QuickActionsUsed = _quickActionsUsed,
                QuickActionsMax = MaxQuickActionsThisTurn(),
                FullUsed = _fullUsed,
                OverchargeUsed = _overchargeUsed,
                OverchargeQuickAvailable = _overchargeUsed && !_overchargeQuickUsed,
                FreeBoostAvailable = _coreKind == LancerCoreKind.Raijin && _coreActive && !_freeBoostUsed && !_braceRestrictedTurn,
                CanSpendQuick = CanSpendQuick(),
                CanSpendFull = !_braceRestrictedTurn && !_fullUsed && _quickActionsUsed == 0,
                ReactionAvailable = !_braceReactionLockout && (CanOfferOverwatch() || CanOfferBrace()),
                SkirmishUsed = _usedActions.Contains(LancerPlayerAction.Skirmish),
                LockOnUsed = _usedActions.Contains(LancerPlayerAction.LockOn),
                HexUsed = _usedActions.Contains(LancerPlayerAction.UseSystem),
                BoostUsed = _usedActions.Contains(LancerPlayerAction.SelectBoost)
            },
            SelectionMode = _selectionMode,
            AttackIntent = _attackIntent,
            SelectedWeaponIndex = _selectedWeaponIndex,
            BarragePickedWeapons = _barragePickedWeapons.ToArray(),
            BarrageWeaponsRequired = _attackIntent == LancerAttackIntent.Barrage ? 2 : 0,
            Prompt = new LancerPromptState
            {
                Active = _reactionPromptActive,
                Reaction = _pendingReaction,
                TimeRemaining = _reactionTimer,
                PendingDamage = _pendingBraceDamage
            },
            PlayerTurn = _playerTurn,
            SpotBonus = _spotBonus,
            SpotFailed = _spotFailed,
            SpotRoll = _spotRoll,
            BriefingStep = _briefingStep,
            BarrageWeaponQueueCount = _barrageWeaponQueue.Count,
            LogLineCount = _log.Count,
            MissionSelect = _phase == LancerGamePhase.MissionSelect ? BuildMissionSelectState() : null,
            LoadoutSelect = _phase == LancerGamePhase.LoadoutSelect ? BuildLoadoutSelectState() : null,
            PreFight = _phase == LancerGamePhase.PreFight ? BuildPreFightState() : null,
            Intermission = _phase == LancerGamePhase.Intermission ? BuildIntermissionState() : null,
            SkillPick = _phase == LancerGamePhase.SkillPick ? BuildSkillPickState() : null,
            MissionCompleteText = _missionCompleteText,
            CampaignSummary = GetCampaignSummary(GetArcade()),
            Tutorial = BuildTutorialState(),
        };
    }

    private LancerMechPanelState BuildMechPanel()
    {
        var weapons = new LancerWeaponState[WeaponSlotCount];
        for (var i = 0; i < WeaponSlotCount; i++)
        {
            var def = GetWeaponDef(i);
            if (def == null)
            {
                // Empty mount — client hides blank slots.
                weapons[i] = new LancerWeaponState { Name = string.Empty, Pickable = false };
                continue;
            }

            var blockReason = IsOrdnanceWeapon(i)
                ? GetOrdnanceBlockReason(i)
                : IsLoadingWeapon(i) && !_weaponLoaded[i]
                    ? LancerWeaponBlockReason.Empty
                    : LancerWeaponBlockReason.None;

            weapons[i] = new LancerWeaponState
            {
                Name = Loc.GetString(def.NameLoc),
                Loaded = !IsLoadingWeapon(i) || _weaponLoaded[i],
                UsedThisTurn = _weaponUsedThisTurn[i],
                Range = GetWeaponRange(i),
                Pickable = IsWeaponAvailableForPick(i),
                BlockReason = blockReason
            };
        }

        return new LancerMechPanelState
        {
            Hp = _playerHp,
            MaxHp = _playerMaxHp,
            Structure = _structure,
            MaxStructure = _playerMaxStructure,
            Stress = _stress,
            MaxStress = PlayerMaxStress,
            Heat = _heat,
            HeatCap = _playerHeatCap,
            Repairs = _repairs,
            RepairCap = _playerRepairCap,
            HexCharges = _hexCharges,
            CorePower = _corePower,
            CoreActive = _coreActive,
            AmrLoaded = _weaponLoaded[0],
            Disengaged = _disengaged,
            HoldAndLock = _holdAndLock,
            Impaired = _playerImpaired,
            Exposed = _playerExposed,
            DangerZone = IsInDangerZone(),
            NuclearCavalier = _hasNuclearCavalier,
            ExternalBatteries = _hasExternalBatteries && !_externalBatteriesDestroyed,
            ExternalBatteriesDestroyed = _externalBatteriesDestroyed,
            DeepWellHeatSink = _hasDeepWellHeatSink,
            DeepWellActive = _deepWellHeatResistThisTurn,
            Grit = _playerGrit,
            OverchargeHeatStep = _overchargeHeatStep,
            Weapons = weapons
        };
    }
}
