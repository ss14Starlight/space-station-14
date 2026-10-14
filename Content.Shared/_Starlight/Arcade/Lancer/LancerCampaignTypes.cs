using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Arcade.Lancer;

[Serializable, NetSerializable]
public enum LancerObjectiveKind : byte
{
    EliminateAll,
    DefendRelay,
    ReachCell,
    HoldCell
}

[Serializable, NetSerializable]
public enum LancerNarrativeBonusKind : byte
{
    FirstAttackAcc,
    FreeRepair,
    CoreCharge,
    ExtraMove,
    ExtraHex,
    FightAcc,
    TempStructure
}

[Serializable, NetSerializable]
public sealed class LancerMissionEntryState
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public string Description = string.Empty;
    public bool Locked;
    public bool Cleared;
}

[Serializable, NetSerializable]
public sealed class LancerMissionSelectState
{
    public int LicenseLevel;
    public int Hull;
    public int Agility;
    public int Engineering;
    public string ChassisSummary = string.Empty;
    public string LoadoutSummary = string.Empty;
    public bool CanResetCampaign;
    public LancerMissionEntryState[] Missions = Array.Empty<LancerMissionEntryState>();
}

[Serializable, NetSerializable]
public sealed class LancerUpgradeOptionState
{
    public string Id = string.Empty;
    public string Label = string.Empty;
    public string Description = string.Empty;
    public bool Selected;
    public bool Pickable = true;
}

[Serializable, NetSerializable]
public sealed class LancerSkillPickState
{
    public string Title = string.Empty;
    public int Hull;
    public int Agility;
    public int Engineering;
    public LancerUpgradeOptionState[] Options = Array.Empty<LancerUpgradeOptionState>();
    public bool CanConfirm;
}

[Serializable, NetSerializable]
public sealed class LancerLoadoutSelectState
{
    public string Title = string.Empty;
    public string MissionName = string.Empty;
    public LancerUpgradeOptionState[] Options = Array.Empty<LancerUpgradeOptionState>();
    public bool CanConfirm;
}

[Serializable, NetSerializable]
public sealed class LancerNarrativeCheckState
{
    public string Label = string.Empty;
    public string Description = string.Empty;
    public bool Selected;
    public bool Pickable = true;
}

[Serializable, NetSerializable]
public sealed class LancerPreFightState
{
    public int FightNumber;
    public int FightCount = 3;
    public string MissionName = string.Empty;
    public string FightDescription = string.Empty;
    public string ObjectiveText = string.Empty;
    public LancerNarrativeCheckState[] NarrativeChecks = Array.Empty<LancerNarrativeCheckState>();
    public bool CanDeploy;
}

[Serializable, NetSerializable]
public sealed class LancerIntermissionState
{
    public int FightNumber;
    public int FightCount = 3;
    public int Repairs;
    public int Structure;
    public int MaxStructure;
    public int Hp;
    public int MaxHp;
    public bool CanRepairStructure;
    public bool CanRepairReactor;
    public int RepairCap;
}

[Serializable, NetSerializable]
public sealed class LancerTutorialState
{
    public bool Active;
    public int StepIndex;
    public int StepCount;
    public string Title = string.Empty;
    public string Hint = string.Empty;
    public LancerPlayerAction[] AllowedActions = Array.Empty<LancerPlayerAction>();
    public LancerGridCoord[] AllowedCells = Array.Empty<LancerGridCoord>();
    public int[] AllowedTargetIds = Array.Empty<int>();
    public bool CanEndTurn;
}
