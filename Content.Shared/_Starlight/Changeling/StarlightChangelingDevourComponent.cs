using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Changeling;

/// <summary>
/// Opts into feeding on living and non-humanoid prey. The action and both do-afters
/// still belong to the common changeling devour system.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StarlightChangelingDevourComponent : Component
{
    /// <summary>
    /// Reserves the prey across windup and consumption; null means another devour can begin.
    /// </summary>
    [AutoNetworkedField]
    public EntityUid? CurrentTarget;

    /// <summary>
    /// Server-side consumption fraction already processed, preventing every-tick callbacks from repeating damage.
    /// </summary>
    public float DamageProgress;

    /// <summary>
    /// Non-humanoid prey whose DevouredBy sets reference this body. These reverse links let role removal
    /// clean up animals that are absent from ChangelingIdentityComponent's humanoid identity records.
    /// </summary>
    public readonly HashSet<EntityUid> DevouredAnimals = new();

    public override bool SendOnlyToOwner => true;
}
