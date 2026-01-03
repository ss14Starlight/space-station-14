using Robust.Shared.GameStates;
using Robust.Shared.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Clothing;

/// <summary>
/// When worn, applies a shader outline to the wearer's sprite and drains power from an integrated battery.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OutlineShieldClothingComponent : Component
{
    /// <summary>
    /// The shader prototype ID to apply to the wearer's sprite
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public string ShaderPrototype = string.Empty;

    /// <summary>
    /// Whether the shield is currently active
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Active = false;

    /// <summary>
    /// The entity currently wearing this clothing (if any)
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Wearer;

    /// <summary>
    /// Power draw rate per second when shield is active (in watts)
    /// </summary>
    [DataField]
    public float PowerDrawRate = 5f;
}
