using Content.Shared.Actions;
using Content.Shared.Body.Part;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared._Starlight.Medical.Surgery;
using Content.Shared._Starlight.Medical.Surgery.Components;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Actions.Events;

[Virtual]
public partial class SurgeryActionEvent : InstantActionEvent
{
    [DataField]
    public EntProtoId<SurgeryComponent>[] Surgeries = [];

    [DataField]
    public List<BodyPartType> BodyPartTypes = [];

    [DataField]
    public List<BodyPartSymmetry> BodyPartSymmetries = [];

    [DataField]
    public TimeSpan InitialDuration = TimeSpan.FromSeconds(5);

    [DataField]
    public SoundSpecifier? Sound = default;
}
