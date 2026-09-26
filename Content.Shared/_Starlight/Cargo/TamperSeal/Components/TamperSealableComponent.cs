using Content.Shared.Tools;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Cargo.TamperSeal.Components;

/// <summary>
/// Marks an entity as being tamper-sealable.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TamperSealableComponent : Component
{
    /// <summary>
    /// The tool qualities that can be used to destroy the tamper seal.
    /// </summary>
    [DataField, AutoNetworkedField] public HashSet<ProtoId<ToolQualityPrototype>> DestroyToolQualities =
        new() { "Slicing", "Cutting" };
}
