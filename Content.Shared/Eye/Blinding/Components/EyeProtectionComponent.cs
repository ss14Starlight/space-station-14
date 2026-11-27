using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Shared.Eye.Blinding.Components;

/// <summary>
/// For welding masks, sunglasses, etc.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState] // Starlight
public sealed partial class EyeProtectionComponent : Component
{
    /// <summary>
    /// How many seconds to subtract from the status effect. If it's greater than the source
    /// of blindness, do not blind.
    /// </summary>
    [DataField("protectionTime", customTypeSerializer: typeof(TimespanSerializer)), AutoNetworkedField] // Starlight
    public TimeSpan ProtectionTime = TimeSpan.FromSeconds(10);
}
