using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Shadekin;

public sealed class BrighteyeSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    private static readonly ProtoId<TagPrototype> _bowTag = "Bow";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BrighteyeComponent, ShotAttemptedEvent>(OnShootAttempt);
    }

    private void OnShootAttempt(Entity<BrighteyeComponent> ent, ref ShotAttemptedEvent args)
    {
        if (_tag.HasTag(args.Used.Owner, _bowTag))
            return;

        _popup.PopupClient(Loc.GetString("gun-disabled"), ent, ent);
        args.Cancel();
    }
}
