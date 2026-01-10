using Content.Server.Objectives.Systems;

namespace Content.Server.Objectives.Components;

[RegisterComponent, Access(typeof(TargetObjectiveSystem))]
public sealed partial class TargetObjectiveComponent : Component
{
    /// <summary>
    /// Locale id for the objective title.
    /// It is passed "targetName" and "job" arguments.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)] //Starlight can be optional for Document theft objectives
    public string Title = string.Empty;

    /// <summary>
    /// Mind entity id of the target.
    /// This must be set by another system using <see cref="TargetObjectiveSystem.SetTarget"/>.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? Target;
}
