using Content.Shared.Polymorph;

namespace Content.Server._Starlight.Administration.Systems.Commands;

/// <summary>
/// This is a component to facilitate storing a polymorph config so
/// that it can be set up via chaining together toolshed commands.
/// </summary>
[RegisterComponent]
public sealed partial class PolymorphSetupComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)] public readonly PolymorphConfiguration Config = new();
}