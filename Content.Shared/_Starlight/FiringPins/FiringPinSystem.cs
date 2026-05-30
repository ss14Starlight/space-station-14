using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools.Components;
using Robust.Shared.Containers;
using Content.Shared.Tools.Systems;
using Robust.Shared.Audio.Systems;
using System.Linq;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Examine;

namespace Content.Shared._Starlight.FiringPins;

public sealed partial class FiringPinSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FiringPinHolderComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FiringPinHolderComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<FiringPinHolderComponent, ExaminedEvent>(OnExamined);

        SubscribeLocalEvent<FiringPinHolderComponent, FiringPinRemovalFinishEvent>(OnPinRemovalFinish);
        SubscribeLocalEvent<FiringPinHolderComponent, AttemptShootEvent>(OnBeforeGunShot);
    }

    public bool CanFire(Entity<FiringPinHolderComponent> ent) => ent.Comp.PinContainer.ContainedEntities.Count > 0;

    public EntityUid[] FiringPins(Entity<FiringPinHolderComponent> ent) => ent.Comp.PinContainer.ContainedEntities.ToArray();

    private void OnStartup(Entity<FiringPinHolderComponent> ent, ref ComponentStartup args)
        => ent.Comp.PinContainer = _container.EnsureContainer<Container>(ent.Owner, FiringPinHolderComponent.PinContainerName);

    private void OnInteractUsing(Entity<FiringPinHolderComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled) return;

        // check insert attempt
        if (TryComp<FiringPinComponent>(args.Used, out var pinComp))
        {
            args.Handled = true;

            if (ent.Comp.PinContainer.ContainedEntities.Count > 0)
            {
                _popup.PopupClient(Loc.GetString("firing-pin-already-there"), ent.Owner, args.User);
                return;
            }

            if (!ent.Comp.SupportedPins.Contains(pinComp.PinType))
            {
                _popup.PopupClient(Loc.GetString("firing-pin-no-fit"), ent.Owner, args.User);
                return;
            }

            if (_container.Insert(args.Used, ent.Comp.PinContainer))
            {
                _popup.PopupClient(Loc.GetString("firing-pin-inserted"), ent.Owner, args.User);
                _audio.PlayPredicted(ent.Comp.PinInsertionSound, ent.Owner, args.User);
                return;
            }
        } else if (TryComp<ToolComponent>(args.Used, out var toolComp) &&
                   _tool.HasQuality(args.Used, ent.Comp.PinExtractionMethod))
        {
            args.Handled = true;

            if(ent.Comp.PinContainer.ContainedEntities.Count == 0)
            {
                _popup.PopupClient(Loc.GetString("firing-pin-holder-empty"), ent.Owner, args.User);
                return;
            }

            _tool.UseTool(args.Used, args.User, ent.Owner, ent.Comp.PinExtractionDelay, ent.Comp.PinExtractionMethod, new FiringPinRemovalFinishEvent(), toolComponent: toolComp);
        }
    }

    private void OnExamined(Entity<FiringPinHolderComponent> ent, ref ExaminedEvent args)
    {
        var pins = FiringPins(ent);
        if (pins.Count() == 0)
        {
            args.PushMarkup(Loc.GetString("firing-pin-no-pin"));
            return;
        }

        foreach (var pin in pins)
        {
            // reflect onto individual pins so they can add their own descriptions to parent
            // example, shot counter pin wants to report the number of shots fired on the gun
            RaiseLocalEvent(pin, args);
        }
    }

    private void OnPinRemovalFinish(Entity<FiringPinHolderComponent> ent, ref FiringPinRemovalFinishEvent args)
    {
        if (args.Cancelled) return;

        var firingPins = FiringPins(ent);
        _container.EmptyContainer(ent.Comp.PinContainer, reparent: false);
        // sandbox gets mad at me when I .First(), so just going to loop through all
        foreach (var pin in firingPins)
        {
            _hands.PickupOrDrop(args.User, pin, dropNear: true);
        }
        _popup.PopupClient(Loc.GetString("firing-pin-removed"), ent.Owner, args.User);
        _audio.PlayPredicted(ent.Comp.PinExtractionSound, ent.Owner, args.User);
    }

    private void OnBeforeGunShot(Entity<FiringPinHolderComponent> ent, ref AttemptShootEvent args)
    {
        if(CanFire(ent))
        {
            var prefireEvent = new FiringPinFireAttemptEvent(args.User, ent.Owner);
            var firingPins = FiringPins(ent);
            foreach(var pin in firingPins)
            {
                RaiseLocalEvent(pin, ref prefireEvent);
            }

            if(!prefireEvent.Cancelled) return; // can fire
        }

        // firing fail
        args.Cancelled = true;
        _popup.PopupClient(Loc.GetString("firing-pin-weapon-failure"), ent.Owner, args.User);

        if(!TryComp<GunComponent>(ent.Owner, out var gun)) return;
        _audio.PlayPredicted(gun.SoundEmpty, ent.Owner, args.User);
    }
}
