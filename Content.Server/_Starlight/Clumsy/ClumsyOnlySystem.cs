using Content.Shared._Starlight.Clumsy;
using Content.Shared.Clumsy;
using Content.Shared.Hands;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Clumsy;

public sealed class ClumsyOnlySystem : EntitySystem
{
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ClumsyOnlyComponent, BeforeGettingEquippedHandEvent>(OnItemPickedUp);
    }

    /// <remarks>
    /// see remarks on LubedSystem as to why this is in Server, and not in Shared
    /// </remarks>
    private void OnItemPickedUp(EntityUid uid, ClumsyOnlyComponent comp, ref BeforeGettingEquippedHandEvent args)
    {
        if (HasComp<ClumsyComponent>(args.User) || args.Cancelled)
            return;

        args.Cancelled = true;

        _transform.SetCoordinates(uid, Transform(args.User).Coordinates);
        _transform.AttachToGridOrMap(uid);
        _throwing.TryThrow(uid, _random.NextVector2(), comp.SlipStrength);
        _popup.PopupEntity(Loc.GetString("lube-slip", ("target", Identity.Entity(uid, EntityManager))),
            args.User,
            args.User,
            PopupType.MediumCaution);
    }

}
