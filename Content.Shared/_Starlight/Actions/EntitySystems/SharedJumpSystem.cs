using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared._Starlight.Actions.Components;
using Content.Shared._Starlight.Actions.Events;
using Content.Shared.Atmos.Components;
using Content.Shared.Throwing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Content.Shared.Stunnable;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Popups;
using Content.Shared._Starlight.Shoelaces.Components;

namespace Content.Shared._Starlight.Actions.EntitySystems;

//idea taked from VigersRay
public abstract class SharedJumpSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IMapManager _mapMan = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedChargesSystem _chargesSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<JumpComponent, MapInitEvent>(OnStartup);
        SubscribeLocalEvent<JumpComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<JumpComponent, JetJumpActionEvent>(OnJump);
        SubscribeLocalEvent<JumpComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<JumpComponent, ThrowDoHitEvent>(OnThrowCollide);
        SubscribeLocalEvent<JumpActionEvent>(OnJump);
    }

    private void OnThrowCollide(EntityUid uid, JumpComponent component, ref ThrowDoHitEvent args)
    {
        if (component.KnockdownSelfOnCollision)
            _stun.TryKnockdown(uid, TimeSpan.FromSeconds(component.KnockdownSelfDuration), true);

        if (component.KnockdownTargetOnCollision)
            _stun.TryKnockdown(args.Target, TimeSpan.FromSeconds(component.KnockdownTargetDuration), true);
    }

    private void OnGetItemActions(Entity<JumpComponent> ent, ref GetItemActionsEvent args)
    {
        if (ent.Comp.IsEquipment)
            args.AddAction(ref ent.Comp.ActionEntity, ent.Comp.Action);
    }

    private void OnStartup(EntityUid uid, JumpComponent component, MapInitEvent args)
    {
        if (component.IsEquipment)
        {
            if (_actionContainer.EnsureAction(uid, ref component.ActionEntity, out var action, component.Action))
                _action.SetEntityIcon((component.ActionEntity.Value, action), uid);
        }
        else
            _action.AddAction(uid, ref component.ActionEntity, component.Action);

        Dirty(uid, component);
    }

    private void OnShutdown(EntityUid uid, JumpComponent component, ComponentShutdown args)
    {
        if (Deleted(uid) || component.ActionEntity is null)
            return;

        if (component.IsEquipment)
            _actionContainer.RemoveAction(component.ActionEntity.Value);
        else
            _action.RemoveAction((uid, null), component.ActionEntity);
    }

    protected virtual bool TryReleaseGas(Entity<JumpComponent> ent, ref JetJumpActionEvent args)
        => TryComp<GasTankComponent>(ent, out var gasTank) && gasTank.TotalMoles > args.MoleUsage;

    private void OnJump(Entity<JumpComponent> ent, ref JetJumpActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = CanJump(ent) && TryReleaseGas(ent, ref args) && TryJump(ent, args.Performer, args.Target, args);
    }

    private void OnJump(JumpActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryJump(args.Performer, args.Performer, args.Target, args);
    }

    private bool CanJump(EntityUid performer)
    {
        if (!HasComp<ShoelaceTiedComponent>(performer))
            return true;

        _popup.PopupClient(Loc.GetString("shoelaces-popup-jump-blocked"), performer, performer);
        return false;
    }

    private bool TryJump(EntityUid performer, EntityUid target, EntityCoordinates targetCoords, JumpActionEvent args)
    {
        var userTransform = Transform(target);
        var userMapCoords = _transform.GetMapCoordinates(userTransform);

        if (args.FromGrid && !_mapMan.TryFindGridAt(userMapCoords, out _, out _)) return false;

        return TryJump(performer, targetCoords, args, target, 15f, args.ToPointer, args.Sound, args.Distance);
    }

    public bool TryJump(EntityUid performer, EntityCoordinates targetCoords, JumpActionEvent args, EntityUid? target = null, float speed = 15f, bool toPointer = false, SoundSpecifier? sound = null, float? distance = null, bool decreaseCharges = false)
    {
        if (args.Action == null || _action.IsCooldownActive(args.Action) || !CanJump(performer))
            return false;

        if (target == null)
            target = performer;

        Jump(performer, target.Value, targetCoords, args, speed, toPointer, sound, distance, decreaseCharges);
        return true;
    }

    public bool TryJump(Entity<JumpComponent?> performer, EntityCoordinates targetCoords, EntityUid? target = null, float speed = 15f, bool toPointer = false, SoundSpecifier? sound = null, float? distance = null, bool decreaseCharges = false)
    {
        if (!Resolve(performer, ref performer.Comp, false)
            || performer.Comp.ActionEntity == null || !TryComp(performer.Comp.ActionEntity, out ActionComponent? action))
            return false;

        var jump = new JumpActionEvent()
        {
            Performer=performer,
            Action=(performer.Comp.ActionEntity.Value, action),
        };
        return TryJump(performer, targetCoords, jump, target, speed, toPointer, sound, distance, decreaseCharges);
    }

    public void Jump(EntityUid performer, EntityUid target, EntityCoordinates targetCoords,  JumpActionEvent args, float speed = 15f, bool toPointer = false, SoundSpecifier? sound = null, float? distance = null, bool decreaseCharges = false)
    {
        if (args.Action == null)
            return;

        if (TryComp<LimitedChargesComponent>(args.Action.Owner, out var limitedCharges)
            && !_chargesSystem.HasCharges((args.Action.Owner, limitedCharges), 1))
            return;
        else if (args.Action.Owner != null && decreaseCharges)
            _chargesSystem.TryUseCharge(args.Action.Owner);

        var userTransform = Transform(target);
        var userMapCoords = _transform.GetMapCoordinates(userTransform);
        var targetMapCoords = _transform.ToMapCoordinates(targetCoords);

        var vector = targetMapCoords.Position - userMapCoords.Position;
        if (distance != null
            && (!toPointer || Vector2.Distance(userMapCoords.Position, targetMapCoords.Position) > distance))
            vector = Vector2.Normalize(vector) * distance.Value;

        if (TryComp<ActionComponent>(args.Action.Owner, out var action) && (limitedCharges == null || limitedCharges.MaxCharges <= 1))
            _action.SetCooldown((args.Action.Owner, action), args.Action.Comp.UseDelay?? TimeSpan.FromSeconds(1));

        _throwing.TryThrow(target, vector, baseThrowSpeed: speed, doSpin: false);

        if (sound != null)
            _audio.PlayPredicted(sound, target, target, AudioParams.Default.WithVolume(-4f));
    }
}
