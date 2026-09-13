using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Arcade.Lancer;

[Serializable, NetSerializable]
public enum LancerPlayerAction : byte
{
    NewGame,
    StartGame,
    SelectMission,
    SelectNarrativeCheck,
    RollSpot,
    BeginCombat,
    IntermissionRepairStructure,
    IntermissionRepairReactor,
    ContinueIntermission,
    SelectMove,
    SelectBoost,
    ConfirmCell,
    Skirmish,
    Barrage,
    SelectWeapon,
    ConfirmTarget,
    LockOn,
    Stabilize,
    Disengage,
    Overcharge,
    UseSystem,
    ActivateCore,
    EndTurn,
    ReactionAccept,
    ReactionDecline,
    SelectUpgrade,
    ConfirmUpgrade,
    SelectLoadout,
    ConfirmLoadout,
    CampaignReset,
    ReturnToMenu,
    Cancel
}
