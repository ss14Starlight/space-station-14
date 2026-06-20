using Content.Server.Actions;
using Content.Server.Administration.Systems;
using Content.Server.Hands.Systems;
using Content.Shared._Starlight;
using Robust.Server.Audio;
using Robust.Server.Containers;

namespace Content.Server._Starlight.Medical.Limbs;
public sealed partial class CyberLimbSystem : EntitySystem
{
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private StarlightEntitySystem _slEnt = default!;
    [Dependency] private HandsSystem _hands = default!;
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private LimbSystem _limb = default!;
    [Dependency] private AudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        InitializeLimbWithItems();
        InitializeToggleable();
    }
}
