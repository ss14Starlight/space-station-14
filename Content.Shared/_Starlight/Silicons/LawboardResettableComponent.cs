using Content.Shared.Silicons.Laws;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Silicons;

[RegisterComponent, NetworkedComponent]
public sealed partial class LawboardResettableComponent : Component
{
    /// <summary>
    /// The amount of time it takes to upload a lawboard.
    /// </summary>
    [DataField]
    public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(10.0f);

    /// <summary>
    /// The default lawset for the attached entity.
    /// If laws are reset to this ID, it will remove the subverted silicon role.
    /// Other laws will ensure that role.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<SiliconLawsetPrototype> DefaultLaws { get; set; }
}
