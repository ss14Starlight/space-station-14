using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Movement;

/// <summary>
/// Barefoot sound for every tile whose footstep collection matches this prototype's ID,
/// so tiles that keep the default BarestepHard still sound like their surface.
/// </summary>
[Prototype]
public sealed partial class BarestepSoundPrototype : IPrototype
{
    /// <summary>
    /// The footstep sound collection of the surface, e.g. FootstepFloor.
    /// </summary>
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public SoundSpecifier Sound = default!;
}
