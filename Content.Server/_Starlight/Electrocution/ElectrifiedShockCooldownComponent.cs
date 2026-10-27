// ReSharper disable CheckNamespace
namespace Content.Server.Electrocution;

/// <summary>
/// Shared by marked electrified sources so a victim cannot be stunned repeatedly by changing sources.
/// Deliberate attacks bypass the cooldown.
/// </summary>
[RegisterComponent]
[Access(typeof(ElectrocutionSystem))]
public sealed partial class ElectrifiedShockCooldownComponent : Component
{
    public TimeSpan NextShockAt;
}
