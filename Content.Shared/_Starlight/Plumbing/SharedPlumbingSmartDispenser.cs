using Content.Shared.Chemistry;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Plumbing;

[Serializable, NetSerializable]
public enum PlumbingSmartDispenserUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class PlumbingSmartDispenserSetDispenseAmountMessage : BoundUserInterfaceMessage
{
    public readonly ReagentDispenserDispenseAmount DispenseAmount;

    public PlumbingSmartDispenserSetDispenseAmountMessage(ReagentDispenserDispenseAmount dispenseAmount) => DispenseAmount = dispenseAmount;

    public PlumbingSmartDispenserSetDispenseAmountMessage(string value) => DispenseAmount = new ReagentDispenserSetDispenseAmountMessage(value).ReagentDispenserDispenseAmount;
}

[Serializable, NetSerializable]
public sealed class PlumbingSmartDispenserDispenseReagentMessage(string reagentId, FixedPoint2 amount, NetEntity targetEntity) : BoundUserInterfaceMessage
{
    public readonly string ReagentId = reagentId;

    public readonly FixedPoint2 Amount = amount;

    public readonly NetEntity TargetEntity = targetEntity;
}

[Serializable, NetSerializable]
public sealed class PlumbingSmartDispenserClearSolutionMessage(NetEntity targetEntity) : BoundUserInterfaceMessage
{
    public readonly NetEntity TargetEntity = targetEntity;
}

[Serializable, NetSerializable]
public sealed class PlumbingSmartDispenserSolutionChangedMessage : EntityEventArgs
{
}
