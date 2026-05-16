using Content.Server.Objectives.Systems;
using Content.Server._Starlight.Objectives.Systems;

namespace Content.Server._Starlight.Objectives.Components;

/// <summary>
/// Requires that the player dies to be complete.
/// </summary>
[RegisterComponent, Access(typeof(SuperDieConditionSystem))]
public sealed partial class SuperDieConditionComponent : Component
{
}
