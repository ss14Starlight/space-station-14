namespace Content.Server._PV.Terraforming;

/// <summary>
/// Marks atmosphere barriers spawned by the terraforming system.
/// This lets the system remove/rebuild only its own barriers and avoid duplicates.
/// </summary>
[RegisterComponent]
public sealed partial class TerraformerBarrierComponent : Component
{
}
