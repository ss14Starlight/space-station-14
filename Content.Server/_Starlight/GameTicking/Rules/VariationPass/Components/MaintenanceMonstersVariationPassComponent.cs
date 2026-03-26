using Content.Shared.EntityTable.EntitySelectors;
namespace Content.Server.GameTicking.Rules.VariationPass.Components;

[RegisterComponent]
public sealed partial class MaintenanceMonstersVariationPassComponent : Component
{
    [DataField(required: true)]
    public EntityTableSelector SpawnTable = default!;

    [DataField]
    public float PerLockerProbability = 0.25f;
}
