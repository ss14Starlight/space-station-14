using Content.Shared._NullLink;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Roles.Ranks;

[Prototype]
public sealed partial class RankPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Name { get; private set; } = string.Empty;

    [DataField(required: true)]
    public RoleRequirementPrototype Requirement { get; private set; } = default!;

    [DataField]
    public Priority Priority = Priority.VeryLow;

    [DataField(required: true)]
    public JobIconPrototype Icon { get; private set; } = default!;
}

public enum Priority
{
    Top,
    VeryHigh,
    High,
    Medium,
    Low,
    VeryLow,
    Bottom
}
