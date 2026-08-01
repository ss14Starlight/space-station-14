using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Arcade.Lancer;

[Serializable, NetSerializable]
public enum LancerGamePhase : byte
{
    Briefing,
    MissionSelect,
    LoadoutSelect,
    PreFight,
    Scene1,
    Combat,
    Intermission,
    MissionComplete,
    SkillPick,
    CampaignComplete,
    Victory,
    Defeat
}

[Serializable, NetSerializable]
public enum LancerUnitKind : byte
{
    PlayerMech,
    /// <summary>Grunt-template Assault (1 HP chaff). Sprite: urbie.</summary>
    Grunt,
    /// <summary>Elite Assault with Hunker Down. Sprite: kerberos_archer.</summary>
    Cutlass,
    /// <summary>Bombard artillery. Sprite: kerberos_bombard.</summary>
    Bombard,
    Relay,
    /// <summary>Alias for Grunt (same stats/sprite); prefer Grunt in new content.</summary>
    Urbie,
    /// <summary>Core Assault striker (mid-tier rifle). Sprite: kerberos_grunt.</summary>
    Assault,
    /// <summary>Core Sniper artillery (long-range Loading AMR). Sprite: kerberos_sniper.</summary>
    Sniper,
}

[Serializable, NetSerializable]
public enum LancerTerrainType : byte
{
    Open,
    Relay,
    RubbleSoft,
    RubbleHard
}

[Serializable, NetSerializable]
public enum LancerCellHighlight : byte
{
    None,
    Reachable,
    Target,
    Blast
}

[Serializable, NetSerializable]
public enum LancerAttackEffectKind : byte
{
    None,
    RifleFlash,
    AmrImpact,
    KnifeSlash,
    HexBlast,
    RocketImpact,
    MissileBlast
}

[Serializable, NetSerializable]
public enum LancerSelectionMode : byte
{
    None,
    Move,
    Boost,
    WeaponPick,
    Target,
    HexTarget,
    LockOnTarget,
    Stabilize
}

[Serializable, NetSerializable]
public enum LancerAttackIntent : byte
{
    None,
    Skirmish,
    Barrage
}

[Serializable, NetSerializable]
public enum LancerReactionType : byte
{
    Overwatch,
    Brace
}

[Serializable, NetSerializable]
public enum LancerStabilizeOption : byte
{
    ClearHeat,
    Repair,
    Reload
}

[Serializable, NetSerializable]
public enum LancerRollKind : byte
{
    Spot,
    Attack,
    Save,
    Damage,
    StructureCheck,
    OverheatCheck
}

[Serializable, NetSerializable]
public sealed class LancerGridCoord : IEquatable<LancerGridCoord>
{
    public int X;
    public int Y;

    public LancerGridCoord()
    {
    }

    public LancerGridCoord(int x, int y)
    {
        X = x;
        Y = y;
    }

    public bool Equals(LancerGridCoord? other)
    {
        if (other is null)
            return false;

        return X == other.X && Y == other.Y;
    }

    public override bool Equals(object? obj) => obj is LancerGridCoord other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y);
}

[Serializable, NetSerializable]
public sealed class LancerCellState
{
    public LancerTerrainType Terrain;
    public LancerCellHighlight Highlight;
}

[Serializable, NetSerializable]
public sealed class LancerUnitState
{
    public int Id;
    public LancerUnitKind Kind;
    public LancerGridCoord Position = new();
    public int Hp;
    public int MaxHp;
    public int Armor;
    public bool Destroyed;
    public bool LockedOn;
    public bool Shredded;
    public bool Impaired;
    public string SpriteState = string.Empty;
}

[Serializable, NetSerializable]
public enum LancerWeaponBlockReason : byte
{
    None,
    Empty,
    Moved,
    Boosted,
    Engaged
}

[Serializable, NetSerializable]
public sealed class LancerWeaponState
{
    public string Name = string.Empty;
    public bool Loaded = true;
    public bool UsedThisTurn;
    public int Range;
    public bool Pickable;
    public LancerWeaponBlockReason BlockReason;
}

[Serializable, NetSerializable]
public sealed class LancerMechPanelState
{
    public int Hp;
    public int MaxHp;
    public int Structure;
    public int MaxStructure;
    public int Stress;
    public int MaxStress;
    public int Heat;
    public int HeatCap;
    public int Repairs;
    public int RepairCap;
    public int HexCharges;
    public int CorePower;
    public bool CoreActive;
    public bool AmrLoaded = true;
    public bool Disengaged;
    public bool HoldAndLock;
    public bool Impaired;
    public bool Exposed;
    public bool DangerZone;
    public bool NuclearCavalier;
    public bool ExternalBatteries;
    public bool ExternalBatteriesDestroyed;
    public bool DeepWellHeatSink;
    public bool DeepWellActive;
    public int Grit;
    public int OverchargeHeatStep;
    public LancerWeaponState[] Weapons = Array.Empty<LancerWeaponState>();
}

[Serializable, NetSerializable]
public sealed class LancerActionEconomyState
{
    public int MoveRemaining;
    public int MoveMax;
    public int QuickActionsUsed;
    public int QuickActionsMax;
    public bool FullUsed;
    public bool OverchargeUsed;
    /// <summary>The bonus quick action granted by overcharging is still unspent.</summary>
    public bool OverchargeQuickAvailable;
    public bool FreeBoostAvailable;
    public bool CanSpendQuick;
    public bool CanSpendFull;
    public bool ReactionAvailable;
    public bool SkirmishUsed;
    public bool LockOnUsed;
    public bool HexUsed;
    public bool BoostUsed;
}

[Serializable, NetSerializable]
public sealed class LancerPromptState
{
    public bool Active;
    public LancerReactionType Reaction;
    public float TimeRemaining;
    public int PendingDamage;
}

[Serializable, NetSerializable]
public sealed class LancerGameStateSnapshot
{
    public LancerGamePhase Phase;
    public LancerCellState[][] Cells = Array.Empty<LancerCellState[]>();
    public LancerUnitState[] Units = Array.Empty<LancerUnitState>();
    public LancerMechPanelState MechPanel = new();
    public LancerActionEconomyState ActionEconomy = new();
    public LancerSelectionMode SelectionMode;
    public LancerAttackIntent AttackIntent;
    public int SelectedWeaponIndex = -1;
    public int[] BarragePickedWeapons = Array.Empty<int>();
    public int BarrageWeaponsRequired;
    public LancerPromptState Prompt = new();
    public bool PlayerTurn;
    public bool SpotBonus;
    public bool SpotFailed;
    public int SpotRoll;
    public int BriefingStep;
    public int BarrageWeaponQueueCount;
    public int LogLineCount;
    public LancerMissionSelectState? MissionSelect;
    public LancerLoadoutSelectState? LoadoutSelect;
    public LancerPreFightState? PreFight;
    public LancerIntermissionState? Intermission;
    public string MissionCompleteText = string.Empty;
    public string Scene1Description = string.Empty;
    public string Scene1RollLabel = string.Empty;
    public LancerSkillPickState? SkillPick;
    public string CampaignSummary = string.Empty;
    public LancerTutorialState? Tutorial;
}

public static class LancerArcadeMessages
{
    [Serializable, NetSerializable]
    public sealed class LancerPlayerActionMessage : BoundUserInterfaceMessage
    {
        public readonly LancerPlayerAction Action;
        public readonly LancerGridCoord? Cell;
        public readonly int WeaponIndex;
        public readonly int TargetUnitId;
        public readonly LancerStabilizeOption StabilizeOption;
        public readonly string ContextId;

        public LancerPlayerActionMessage(
            LancerPlayerAction action,
            LancerGridCoord? cell = null,
            int weaponIndex = -1,
            int targetUnitId = -1,
            LancerStabilizeOption stabilizeOption = LancerStabilizeOption.ClearHeat,
            string contextId = "")
        {
            Action = action;
            Cell = cell;
            WeaponIndex = weaponIndex;
            TargetUnitId = targetUnitId;
            StabilizeOption = stabilizeOption;
            ContextId = contextId;
        }
    }

    [Serializable, NetSerializable]
    public sealed class LancerGameStateMessage : BoundUserInterfaceMessage
    {
        public readonly LancerGameStateSnapshot Snapshot;

        public LancerGameStateMessage(LancerGameStateSnapshot snapshot)
        {
            Snapshot = snapshot;
        }
    }

    [Serializable, NetSerializable]
    public sealed class LancerLogMessage : BoundUserInterfaceMessage
    {
        public readonly string Line;

        public LancerLogMessage(string line)
        {
            Line = line;
        }
    }

    [Serializable, NetSerializable]
    public sealed class LancerReactionPromptMessage : BoundUserInterfaceMessage
    {
        public readonly LancerReactionType Reaction;
        public readonly float TimeoutSeconds;
        public readonly int PendingDamage;

        public LancerReactionPromptMessage(LancerReactionType reaction, float timeoutSeconds, int pendingDamage = 0)
        {
            Reaction = reaction;
            TimeoutSeconds = timeoutSeconds;
            PendingDamage = pendingDamage;
        }
    }

    [Serializable, NetSerializable]
    public sealed class LancerUserStatusMessage : BoundUserInterfaceMessage
    {
        public readonly bool IsPlayer;

        public LancerUserStatusMessage(bool isPlayer)
        {
            IsPlayer = isPlayer;
        }
    }

    [Serializable, NetSerializable]
    public sealed class LancerDiceRollMessage : BoundUserInterfaceMessage
    {
        public readonly LancerRollKind Kind;
        public readonly string SourceLabel;
        public readonly int D20;
        public readonly int[] AccDice;
        public readonly int[] DiffDice;
        public readonly int Modifier;
        public readonly int Total;
        public readonly int TargetNumber;
        public readonly int[] DamageDice;
        public readonly bool Hit;
        public readonly bool Crit;
        public readonly bool IsPlayerRoll;

        public LancerDiceRollMessage(
            LancerRollKind kind,
            string sourceLabel,
            int d20,
            int[] accDice,
            int[] diffDice,
            int modifier,
            int total,
            int targetNumber,
            int[] damageDice,
            bool hit,
            bool crit,
            bool isPlayerRoll)
        {
            Kind = kind;
            SourceLabel = sourceLabel;
            D20 = d20;
            AccDice = accDice;
            DiffDice = diffDice;
            Modifier = modifier;
            Total = total;
            TargetNumber = targetNumber;
            DamageDice = damageDice;
            Hit = hit;
            Crit = crit;
            IsPlayerRoll = isPlayerRoll;
        }
    }

    [Serializable, NetSerializable]
    public sealed class LancerAttackEffectMessage : BoundUserInterfaceMessage
    {
        public readonly LancerGridCoord Cell;
        public readonly LancerAttackEffectKind Kind;
        public readonly LancerGridCoord? FromCell;

        public LancerAttackEffectMessage(LancerGridCoord cell, LancerAttackEffectKind kind, LancerGridCoord? fromCell = null)
        {
            Cell = cell;
            Kind = kind;
            FromCell = fromCell;
        }
    }
}
