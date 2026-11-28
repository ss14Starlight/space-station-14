// ReSharper disable once CheckNamespace
namespace Content.Shared.Vehicle;

public sealed partial class ContainerVehicleExitEvent
{
    public readonly NetEntity Operator;

    public ContainerVehicleExitEvent(NetEntity operatorUid) => Operator = operatorUid;
}
