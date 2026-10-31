// ReSharper disable once CheckNamespace
namespace Content.Shared.Turrets;

public sealed partial class TurretTargetSettingsComponent
{
    [DataField, AutoNetworkedField]
    public bool TargetNeutralEntities;
}
