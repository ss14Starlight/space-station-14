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
    public EntityUid? CurrentTarget;

    public float DamageProgress;

    // Humanoids are already tracked and cleaned up by ChangelingIdentityComponent.
    public readonly HashSet<EntityUid> DevouredAnimals = new();

    public override bool SendOnlyToOwner => true;
}
