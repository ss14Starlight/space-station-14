using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Shared.Implants.Components;

/// <summary>
/// Component that automatically de-implant's (hopefully) it'self after the specified container is empty.
/// </summary>
[RegisterComponent]
public sealed partial class DeimplantOnStorageEmptyComponent : Component
{
    /// <summary>
    /// the ID of the container to check if empty
    /// </summary>
    [DataField]
    public string ContainerId = "storagebase";
}
