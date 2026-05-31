using Content.Server.StationEvents.Events;
using Robust.Shared.Audio;

namespace Content.Server._Starlight.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(PsychicScreachRule))]
public sealed partial class PsychicScreachRuleComponent : Component
{
    public EntityUid? chosenStation;

    [DataField]
    public SoundSpecifier Scream = new SoundPathSpecifier("/Audio/_Starlight/StationEvents/Meatzone_Howl.ogg");

    [DataField]
    public SoundSpecifier Atmosphere1 = new SoundPathSpecifier("/Audio/_Starlight/Ambience/Station_SpookyAtmosphere2.ogg");

    [DataField]
    public SoundSpecifier Atmosphere2 = new SoundPathSpecifier("/Audio/_Starlight/Ambience/Station_SpookyAtmosphere1.ogg");
}
