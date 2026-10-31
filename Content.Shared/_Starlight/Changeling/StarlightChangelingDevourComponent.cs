using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Changeling;

/// <summary>
/// Opts into feeding on living and non-humanoid prey. The action and both do-afters
/// still belong to the common changeling devour system.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StarlightChangelingDevourComponent : Component
{
    [AutoNetworkedField]
    public ushort? DoAfterIndex;

    public float DamageProgress;

    public readonly HashSet<EntityUid> DevouredAnimals = new();

    public override bool SendOnlyToOwner => true;
}
