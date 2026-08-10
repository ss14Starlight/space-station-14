using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Radio.Systems;

/// <summary>
/// Easy workaround if you intend to override a job icon in the radio
/// </summary>

[RegisterComponent]
public sealed partial class JobIconOverrideComponent : Component
{
    [DataField] public ProtoId<JobIconPrototype> JobIconOverride = "JobIconAssistant";

    [DataField] public LocId JobTitleOverride = "job-name-assistant";

    public string LocalizedJobTitle =>
        field ??= Loc.GetString(JobTitleOverride);
}
