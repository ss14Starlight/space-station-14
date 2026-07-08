using Robust.Shared.Audio;

namespace Content.Shared._Starlight.Implants.Components;
﻿
/// <summary>
/// Component for Mind Control implants.
/// </summary>
[RegisterComponent]
public sealed partial class MindControlImplantComponent : Component
{
    /// <summary>
    /// Implants owner
    /// </summary>
    [DataField] public EntityUid Master;

    /// <summary>
    /// The text that is sent when a user is implanted
    /// </summary>
    [DataField] public LocId BriefingText = "mind-control-user-briefing";

    /// <summary>
    /// The text that is sent when a user is freed from the implant
    /// </summary>
    [DataField] public LocId DebriefingText = "mind-control-user-freed";


    /// <summary>
    /// Briefing sound when a user is implanted
    /// </summary>
    [DataField] public SoundSpecifier BriefingSound = new SoundPathSpecifier("/Audio/Ambience/Antag/traitor_start.ogg");

}
