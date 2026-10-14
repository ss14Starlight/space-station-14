using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Computers.Recruitment;
[Serializable, NetSerializable]
public enum RecruitmentComputerUiKey : byte
{
    Key,
}
[Serializable, NetSerializable]
public sealed class RecruitmentChangeBuiMsg : BoundUserInterfaceMessage
{
    public required NetEntity Station { get; init; }
    public required ProtoId<JobPrototype> Job { get; init; }
    public required int Amount { get; init; }

}
