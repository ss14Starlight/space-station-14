using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Chat;

[RegisterComponent]
public sealed partial class ChatForceJobIconComponent : Component
{
    [DataField] public ProtoId<JobIconPrototype> JobIcon = "JobIconUnknown";
    [DataField] private string? _jobTitle;
    [DataField] public LocId? JobTitle;
    public string? LocalizedJobTitle { set => _jobTitle = value; get => _jobTitle ?? Loc.GetString(JobTitle ?? string.Empty); }
}