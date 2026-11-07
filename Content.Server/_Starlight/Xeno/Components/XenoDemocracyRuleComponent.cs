using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Starlight.Xeno.Components;

/// <summary>
/// Component for the XenoDemocracyRuleSystem that should store gameplay info.
/// </summary>
[RegisterComponent, Access(typeof(XenoDemocracyRuleSystem))]
[AutoGenerateComponentPause]
public sealed partial class XenoDemocracyRuleComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? VoiceVoteTimer;

    [DataField]
    public TimeSpan VoiceVoteDelay = TimeSpan.FromMinutes(30);

    [DataField]
    public bool HasVoice = false;
}
