using Content.Shared.NPC.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Starlight.Xenoarchaeology.Artifact.XAE.Components;

/// <summary>
/// Part of an artifact while a turret node has turned it into a turret
/// Kept on the artifact so multiple turret nodes share one timer
/// </summary>
[RegisterComponent, Access(typeof(XAETurretSystem)), AutoGenerateComponentPause]
public sealed partial class XAETurretActiveComponent : Component
{
    /// <summary>
    /// When the artifact should revert back from being a turret
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan ActiveUntil;

    /// <summary>
    /// Faction that was applied, so it can be removed on revert
    /// </summary>
    [DataField]
    public ProtoId<NpcFactionPrototype> Faction;
}
