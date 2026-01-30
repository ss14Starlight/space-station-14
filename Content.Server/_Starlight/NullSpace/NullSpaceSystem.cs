using Content.Shared.Eye;
using Robust.Server.GameObjects;
using Content.Server.Atmos.Components;
using Content.Shared.Temperature.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using System.Linq;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared._Starlight.NullSpace;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server._Starlight.Weapons.Ranged;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Components;
using Content.Shared.Hands;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Enums;
using Content.Server._Starlight.Bluespace;

namespace Content.Server._Starlight.NullSpace;

public sealed partial class NullSpaceSystem : SharedNullSpaceSystem
{
    [Dependency] private readonly VisibilitySystem _visibilitySystem = default!;
    [Dependency] private readonly SharedStealthSystem _stealth = default!;
    [Dependency] private readonly EyeSystem _eye = default!;
    [Dependency] private readonly NpcFactionSystem _factions = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedVirtualItemSystem _virtualItem = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly NullSpacePhaseSystem _phaseSystem = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NullSpaceComponent, MapInitEvent>(OnStartup);
        SubscribeLocalEvent<NullSpaceComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<NullSpaceComponent, AtmosExposedGetAirEvent>(OnExpose);
        SubscribeLocalEvent<NullSpaceComponent, VirtualItemDeletedEvent>(OnVirtualItemDeleted);
        SubscribeLocalEvent<NullSpaceComponent, NullSpaceShuntEvent>(NullSpaceShunt);

        _player.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    // We do this to prevent a SoftLock... due to visibilitySystem.
    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.Disconnected)
            return;

        if (TryComp<NullSpaceComponent>(args.Session.AttachedEntity, out var nullspacecomp))
        {
            SpawnAtPosition(_shadekinShadow, Transform(args.Session.AttachedEntity.Value).Coordinates);
            RemComp(args.Session.AttachedEntity.Value, nullspacecomp);

            if (TryComp<PullableComponent>(args.Session.AttachedEntity, out var pullable) && pullable.BeingPulled)
                _pulling.TryStopPull(args.Session.AttachedEntity.Value, pullable);
        }
    }

    public void OnStartup(EntityUid uid, NullSpaceComponent component, MapInitEvent args)
    {
        var visibility = EnsureComp<VisibilityComponent>(uid);
        _visibilitySystem.RemoveLayer((uid, visibility), (int)VisibilityFlags.Normal, false);
        _visibilitySystem.AddLayer((uid, visibility), (int)VisibilityFlags.NullSpace, false);
        _visibilitySystem.RefreshVisibility(uid, visibility);

        if (TryComp<EyeComponent>(uid, out var eye))
            _eye.SetVisibilityMask(uid, eye.VisibilityMask | (int)VisibilityFlags.NullSpace, eye);

        if (TryComp<TemperatureComponent>(uid, out var temp))
            temp.AtmosTemperatureTransferEfficiency = 0;

        var stealth = EnsureComp<StealthComponent>(uid);
        _stealth.SetVisibility(uid, 0.8f, stealth);

        SuppressFactions(uid, component, true);

        EnsureComp<PressureImmunityComponent>(uid);
        EnsureComp<MovementIgnoreGravityComponent>(uid);

        if (TryComp<HandsComponent>(uid, out var handsComponent))
        {
            foreach (var hand in _hands.EnumerateHands((uid, handsComponent)))
            {
                if (_hands.GetHeldItem((uid, handsComponent), hand) is var item)
                {
                    if (HasComp<UnremoveableComponent>(item))
                        continue;

                    if (TryComp<VirtualItemComponent>(item, out var vcomp))
                        if (HasComp<NullSpacePulledComponent>(vcomp.BlockingEntity) && TryComp<PullableComponent>(vcomp.BlockingEntity, out var pulling) && pulling.BeingPulled)
                        {
                            RemComp<NullSpacePulledComponent>(vcomp.BlockingEntity);
                            // safety check just to make sure you dont pull something out of nullspace by phasing in
                            if (!HasComp<NullSpaceComponent>(vcomp.BlockingEntity)) _phaseSystem.Phase(vcomp.BlockingEntity);
                            continue;
                        }

                    _hands.DoDrop((uid, handsComponent), hand, true);
                }

                if (_virtualItem.TrySpawnVirtualItemInHand(uid, uid, out var virtItem))
                    EnsureComp<UnremoveableComponent>(virtItem.Value);
            }
        }

        if (TryComp<PullableComponent>(uid, out var pullable) && pullable.BeingPulled)
        {
            // if thing pulling is in nullspace, you're coming along with them.
            if (!HasComp<NullSpaceComponent>(pullable.Puller!.Value))
                _pulling.TryStopPull(uid, pullable);
        }
    }

    public void OnShutdown(EntityUid uid, NullSpaceComponent component, ComponentShutdown args)
    {
        if (TryComp<VisibilityComponent>(uid, out var visibility))
        {
            _visibilitySystem.AddLayer((uid, visibility), (int)VisibilityFlags.Normal, false);
            _visibilitySystem.RemoveLayer((uid, visibility), (int)VisibilityFlags.NullSpace, false);
            _visibilitySystem.RefreshVisibility(uid, visibility);
        }

        if (TryComp<EyeComponent>(uid, out var eye))
            _eye.SetVisibilityMask(uid, (int)VisibilityFlags.Normal, eye);

        if (TryComp<TemperatureComponent>(uid, out var temp))
            temp.AtmosTemperatureTransferEfficiency = 0.1f;

        SuppressFactions(uid, component, false);

        RemComp<StealthComponent>(uid);
        RemComp<PressureImmunityComponent>(uid);
        RemComp<MovementIgnoreGravityComponent>(uid);

        _virtualItem.DeleteInHandsMatching(uid, uid);
    }

    private void OnVirtualItemDeleted(EntityUid uid, NullSpaceComponent component, VirtualItemDeletedEvent args)
    {
        if (TryComp<HandsComponent>(uid, out var handsComponent))
        {
            foreach (var hand in _hands.EnumerateHands((uid, handsComponent)))
            {
                if (_hands.GetHeldItem((uid, handsComponent), hand) is var item)
                {
                    if (HasComp<UnremoveableComponent>(item))
                        continue;

                    if (TryComp<VirtualItemComponent>(item, out var vcomp))
                    {
                        // safety check just to make sure you dont pull something into nullspace by phasing out.
                        if(HasComp<NullSpaceComponent>(vcomp.BlockingEntity)) _phaseSystem.Phase(vcomp.BlockingEntity);
                        continue;
                    }
    
                    _hands.DoDrop((uid, handsComponent), hand, true);
                }
    
                if (_virtualItem.TrySpawnVirtualItemInHand(uid, uid, out var virtItem))
                    EnsureComp<UnremoveableComponent>(virtItem.Value);
            }
        }
    }

    private void NullSpaceShunt(EntityUid uid, NullSpaceComponent component, NullSpaceShuntEvent args)
    {
        SpawnAtPosition(_shadekinShadow, Transform(uid).Coordinates);
        RemComp(uid, component);
    }

    public void SuppressFactions(EntityUid uid, NullSpaceComponent component, bool set)
    {
        if (set)
        {
            if (!TryComp<NpcFactionMemberComponent>(uid, out var factions))
                return;

            component.SuppressedFactions = factions.Factions.ToList();

            foreach (var faction in factions.Factions)
                _factions.RemoveFaction(uid, faction);
        }
        else
        {
            foreach (var faction in component.SuppressedFactions)
                _factions.AddFaction(uid, faction);

            component.SuppressedFactions.Clear();
        }
    }

    private void OnExpose(EntityUid uid, NullSpaceComponent component, ref AtmosExposedGetAirEvent args)
    {
        if (args.Handled)
            return;

        args.Gas = null;
        args.Handled = true;
    }
}