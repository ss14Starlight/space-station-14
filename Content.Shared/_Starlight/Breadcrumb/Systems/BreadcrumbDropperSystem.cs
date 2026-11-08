using System.Linq;
using Content.Shared._Starlight.Abstract.Extensions;
using Content.Shared._Starlight.Breadcrumb.Components;
using Content.Shared._Starlight.Breadcrumb.UI;
using Content.Shared._Starlight.Light.Components;
using Content.Shared._Starlight.Light.EntitySystems;
using Content.Shared.Charges.Systems;
using Content.Shared.IgnitionSource;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Breadcrumb.Systems;

public sealed partial class BreadcrumbDropperSystem : EntitySystem
{
    [Dependency] private SharedChargesSystem _charges = null!;
    [Dependency] private SharedPopupSystem _popup = null!;
    [Dependency] private IGameTiming _timing = null!;
    [Dependency] private EntityLookupSystem _lookup = null!;
    [Dependency] private SharedAppearanceSystem _appearance = null!;
    [Dependency] private SharedPointLightSystem _pointLight = null!;
    [Dependency] private SharedExpendableLightSystem _expendableLight = null!;
    [Dependency] private SharedUserInterfaceSystem _ui = null!;
    [Dependency] private IRobustRandom _random = null!;
    [Dependency] private MobStateSystem _mobState = null!;

    public override void Initialize()
    {
        base.Initialize();

        // BUI messages aren't ByRefEvents so we can't use [SubscribeLocalEvent].
        SubscribeLocalEvent<BreadcrumbDropperComponent, BreadcrumbDropperColorMessage>(OnSelectColor);
    }

    [SubscribeLocalEvent]
    public void OnAutoHandleState(Entity<BreadcrumbDropperComponent> dropper, ref AfterAutoHandleStateEvent args)
        => UpdateAppearance(dropper);

    [SubscribeLocalEvent]
    public void OnDropperMapInit(Entity<BreadcrumbDropperComponent> dropper, ref MapInitEvent args)
    {
        // If no color is set explicitly, pick a random one.
        if (dropper.Comp.Color == Color.Black && dropper.Comp.ColorOptions.Count > 0)
        {
            dropper.Comp.Color = _random.PickPredicted(_timing, dropper.Comp.ColorOptions.Values.ToList(),
                GetNetEntity(dropper).Id);
            Dirty(dropper);
        }

        UpdateAppearance(dropper);
    }

    /// <summary>
    /// Toggle dropping on/off. Bound to both pressing Z (<see cref="UseInHandEvent"/>) and pressing E
    /// (<see cref="ActivateInWorldEvent"/>) on the bag.
    /// </summary>
    [SubscribeLocalEvent]
    public void OnUseInHand(Entity<BreadcrumbDropperComponent> dropper, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = Toggle(dropper, args.User);
    }

    [SubscribeLocalEvent]
    public void OnActivateInWorld(Entity<BreadcrumbDropperComponent> dropper, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = Toggle(dropper, args.User);
    }

    /// <summary>
    /// Prevent the bag from staying enabled while not held by a person.
    /// </summary>
    [SubscribeLocalEvent]
    public void OnDropped(Entity<BreadcrumbDropperComponent> dropper, ref DroppedEvent args)
        => StopSilently(dropper);

    /// <summary>
    /// Alt-click opens the color picker UI so the player can choose what color their droppings glow.
    /// </summary>
    [SubscribeLocalEvent]
    public void OnGetAltVerbs(Entity<BreadcrumbDropperComponent> dropper, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("breadcrumb-dropper-configure-verb"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/settings.svg.192dpi.png")),
            Act = () => _ui.TryOpenUi(dropper.Owner, BreadcrumbDropperUiKey.Key, user),
        });
    }

    /// <summary>
    /// The single networked event the color picker produces: apply the chosen preset to this dropper. Only newly
    /// dropped crumbs pick up the new color; ones already on the floor keep theirs.
    /// </summary>
    private void OnSelectColor(EntityUid uid, BreadcrumbDropperComponent component, BreadcrumbDropperColorMessage args)
    {
        // Resolve the key against our own options so a bad/stale key from the client can't set an arbitrary color.
        if (!component.ColorOptions.TryGetValue(args.Color, out var color))
            return;

        component.Color = color;
        UpdateAppearance((uid, component));
        Dirty(uid, component);
    }

    /// <summary>
    /// Interact using = Holding an item and then left-clicking ours. In this case we're only interested in entities
    /// that have the <see cref="BreadcrumbChargesComponent"/>.
    /// </summary>
    [SubscribeLocalEvent]
    public void OnInteractUsingEvent(Entity<BreadcrumbDropperComponent> dropper, ref InteractUsingEvent args)
    {
        if (args.Handled) return;
        if (!TryComp<BreadcrumbChargesComponent>(args.Used, out var charges))
            return;

        // Prevent filling if it's already full.
        if (_charges.GetCurrentCharges(dropper.Owner) >= _charges.GetMaxCharges(dropper.Owner))
        {
            _popup.PopupPredicted(Loc.GetString(dropper.Comp.LocRechargeFull), dropper, args.User);
            return;
        }

        _charges.AddCharges((dropper, null), charges.Charges);
        UpdateAppearance(dropper);
        _popup.PopupPredicted(Loc.GetString(dropper.Comp.LocRecharged), dropper, args.User);
        QueueDel(args.Used);
        args.Handled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var droppers = EntityQueryEnumerator<BreadcrumbDropperComponent, TransformComponent>();
        while (droppers.MoveNext(out var uid, out var dropperComp, out var xform))
        {
            if (!dropperComp.Enabled || now < dropperComp.NextDropCheck)
                continue;

            dropperComp.NextDropCheck = now + dropperComp.DropCheckFrequency;

            Entity<BreadcrumbDropperComponent> dropper = (uid, dropperComp);
            Dirty(dropper);

            if (!CanBeActive(uid))
            {
                StopSilently(dropper);
                continue;
            }

            if (HasCrumbNearby(dropper, xform))
                continue;

            if (!_charges.TryUseCharge(uid))
            {
                dropperComp.Enabled = false;
                UpdateAppearance(dropper);
                _popup.PopupPredicted(Loc.GetString(dropper.Comp.LocEmptied), dropper, null, PopupType.Medium);
                continue;
            }

            Drop(dropper, xform);
            UpdateAppearance(dropper);
        }
    }

    private bool Toggle(Entity<BreadcrumbDropperComponent> dropper, EntityUid user)
    {
        if (!dropper.Comp.Enabled && !CanBeActive(dropper))
            return false;

        // Prevent activating when disabled empty.
        if (!dropper.Comp.Enabled && _charges.GetCurrentCharges(dropper.Owner) <= 0)
        {
            _popup.PopupPredicted(Loc.GetString(dropper.Comp.LocToggleEmpty), dropper, user);
            return true;
        }

        dropper.Comp.Enabled = !dropper.Comp.Enabled;
        Dirty(dropper);
        UpdateAppearance(dropper);
        _popup.PopupPredicted(
            Loc.GetString(dropper.Comp.Enabled ? dropper.Comp.LocToggleOn : dropper.Comp.LocToggleOff),
            dropper, user);
        return true;
    }

    private bool CanBeActive(EntityUid uid)
    {
        var xformQuery = GetEntityQuery<TransformComponent>();
        var current = uid;
        while (xformQuery.TryGetComponent(current, out var xform) && xform.ParentUid.IsValid())
        {
            current = xform.ParentUid;
            if (HasComp<MobStateComponent>(current))
                return _mobState.IsAlive(current);
        }

        return false;
    }

    private void StopSilently(Entity<BreadcrumbDropperComponent> dropper)
    {
        if (!dropper.Comp.Enabled)
            return;

        dropper.Comp.Enabled = false;
        Dirty(dropper);
        UpdateAppearance(dropper);
    }

    private void Drop(Entity<BreadcrumbDropperComponent> dropper, TransformComponent xform)
    {
        var ent = PredictedSpawnAtPosition(dropper.Comp.BreadcrumbPrototype, xform.Coordinates);

        var crumb = EnsureComp<BreadcrumbComponent>(ent);
        crumb.Dropper = dropper.Owner;
        crumb.Color = dropper.Comp.Color;

        _pointLight.SetColor(ent, crumb.Color);
        _appearance.SetData(ent, BreadcrumbVisualData.Color, crumb.Color);
        Dirty(ent, crumb);

        if (TryComp<ExpendableLightComponent>(ent, out var expendableLight))
            _expendableLight.TryActivate((ent, expendableLight));
    }

    private bool HasCrumbNearby(Entity<BreadcrumbDropperComponent> dropper, TransformComponent xform)
    {
        foreach (var ent in _lookup
            .GetEntitiesInRange<BreadcrumbComponent>(xform.Coordinates, dropper.Comp.DropRange))
        {
            if (ent.Comp.Dropper == dropper.Owner)
                return true;
        }

        return false;
    }

    /// <summary>
    /// When ignition ends, remove the breadcrumb comp.
    /// </summary>
    [SubscribeLocalEvent]
    public void OnBreadcrumbIgnition(Entity<BreadcrumbComponent> crumb, ref IgnitionEvent ev)
    {
        if (ev.Ignite) return;
        RemComp<BreadcrumbComponent>(crumb);
    }

    private void UpdateAppearance(Entity<BreadcrumbDropperComponent> dropper)
    {
        _appearance.SetData(dropper, BreadcrumbDropperVisuals.Enabled, dropper.Comp.Enabled);
        _appearance.SetData(dropper, BreadcrumbDropperVisuals.Color, dropper.Comp.Color);

        var charges = _charges.GetCurrentCharges(dropper.Owner);
        var level = charges switch
        {
            <= 0 => BreadcrumbDropperFillLevel.Empty,
            < 25 => BreadcrumbDropperFillLevel.Low,
            < 70 => BreadcrumbDropperFillLevel.Medium,
            _ => BreadcrumbDropperFillLevel.High,
        };
        _appearance.SetData(dropper, BreadcrumbDropperVisuals.Fill, level);
    }
}
