using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

namespace Content.Shared._Starlight.CartridgeLoader.Cartridges;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MedTekAnalyzerComponent : Component
{
    // Sol-start: Mirrored from MedTekCartridge so the Analyze Patient verb can filter client-side.
    /// <summary>
    /// If set, the Analyze Patient verb is only offered for entities with these damage containers.
    /// </summary>
    [DataField("damageContainers", customTypeSerializer: typeof(PrototypeIdListSerializer<DamageContainerPrototype>))]
    [AutoNetworkedField]
    public List<string>? DamageContainers;
    // Sol-end
}
