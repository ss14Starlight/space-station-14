using Robust.Shared.Serialization;

// ReSharper disable once CheckNamespace
namespace Content.Shared._Starlight.TurretController;

[Serializable, NetSerializable]
public sealed class DeployableTurretNeutralTargetingChangedMessage : BoundUserInterfaceMessage
{
    public bool Enabled;

    public DeployableTurretNeutralTargetingChangedMessage(bool enabled) => Enabled = enabled;
}
