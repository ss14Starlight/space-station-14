using Content.Shared.Access;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Access;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class IdClothingBlockerComponent : Component
{
    [DataField("isBlocked")] [AutoNetworkedField]
    public bool IsBlocked = false;

    [DataField("allowedJobs")]
    public List<ProtoId<AccessLevelPrototype>>? AllowedJobs = new();

    [DataField("beepSound")]
    public SoundSpecifier BeepSound = new SoundPathSpecifier("/Audio/Effects/beep1.ogg");

    [DataField]
    public bool FreezeUser = true;
} 