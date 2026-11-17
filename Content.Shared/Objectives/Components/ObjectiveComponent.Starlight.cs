using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.Objectives.Components;

public sealed partial class ObjectiveComponent
{
    /// <summary>
    /// Antagonist this objective belongs to. Achieve-objectives conditions only count objectives
    /// with the same antagonist. Leave unset for story, species, and other unrelated objectives.
    /// </summary>
    [DataField]
    public ProtoId<AntagPrototype>? Antag;

    /// <summary>
    /// Show the hidden preview text in the picker and defer random-person targets until confirmation.
    /// </summary>
    [DataField]
    public bool Hidden;

    /// <summary>
    /// Hidden objective picker title. Required when Hidden is true, ignored otherwise.
    /// </summary>
    [DataField]
    public LocId? HiddenTitle;

    /// <summary>
    /// Hidden objective picker description. Required when Hidden is true, ignored otherwise.
    /// </summary>
    [DataField]
    public LocId HiddenDescription = "objective-picker-hidden-target-description";
}
