using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Server.Mobs;

/// <summary>
///     Mobs with this component will emote a deathgasp when they die.
/// </summary>
/// <see cref="DeathgaspSystem"/>
[RegisterComponent]
public sealed partial class DeathgaspComponent : Component
{
    /// <summary>
    ///     The emote prototype to use.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<EmotePrototype> Prototype = "DefaultDeathgasp";

    // Starlight
    [DataField]
    public ProtoId<DamageTypePrototype> DamageType = "Asphyxiation";
}
