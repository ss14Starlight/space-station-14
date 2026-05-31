using Robust.Shared.GameStates;

namespace Content.Shared.VentCrawl.Components;

/// <summary>
/// A component indicating that the entity is in the process of moving through the venting process
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BeingVentCrawlComponent : Component
{
    /// <summary>
    /// Gets or sets up a holder entity
    /// </summary>
    [DataField("holder"), AutoNetworkedField]
    public EntityUid Holder
    {
        get;
        set
        {
            if (field == value)
                return;

            if (value == default)
                throw new ArgumentException("Holder cannot be default EntityUid");

            field = value;
        }
    }
}
