using Content.Shared._Starlight.Antags.BloodBound.Components;
using Content.Shared._Starlight.Antags.BloodBound.Systems;
using Content.Shared.Antag;
using Content.Shared.StatusIcon.Components;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Antags.BloodBound.Systems;

public sealed partial class BloodBoundSystem : SharedBloodBoundSystem
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodBoundComponent, GetStatusIconsEvent>(OnBloodBoundGetIcons);
    }

    private void OnBloodBoundGetIcons(Entity<BloodBoundComponent> entity, ref GetStatusIconsEvent args)
    {
        if (_playerManager.LocalSession?.AttachedEntity is { } playerEntity)
        {
            if (!HasComp<ShowAntagIconsComponent>(playerEntity) &&
                entity.Owner != playerEntity &&
                entity.Comp.Partner != playerEntity)
                return;
        }

        if (_prototypeManager.TryIndex(entity.Comp.BloodBoundIcon, out var iconPrototype))
            args.StatusIcons.Add(iconPrototype);
    }
}
