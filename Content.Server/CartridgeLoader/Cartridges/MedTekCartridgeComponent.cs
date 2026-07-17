using Content.Shared.Damage.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

namespace Content.Server.CartridgeLoader.Cartridges;

[RegisterComponent]
public sealed partial class MedTekCartridgeComponent : Component
{
    // Sol-start: Optional scan whitelist applied to the PDA HealthAnalyzer when this cartridge is installed.
    /// <summary>
    /// If set, the MedTek health analyzer may only scan entities whose damage container is in this list.
    /// Null means unrestricted (default MedTek behavior).
    /// </summary>
    [DataField("damageContainers", customTypeSerializer: typeof(PrototypeIdListSerializer<DamageContainerPrototype>))]
    public List<string>? DamageContainers;
    // Sol-end
}
