using System.Linq;
using Content.Shared._Starlight.Abstract.Extensions;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Eui;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Admeme.Jumpscare;

/// <summary>
/// For doing jumpscares
/// </summary>
public abstract partial class SharedJumpscareSystem : EntitySystem
{
    [Dependency] private MobStateSystem _state = null!;
    [Dependency] private SharedStunSystem _stun = null!;
    [Dependency] private DamageableSystem _damage = null!;
    [Dependency] private SharedPopupSystem _popup = null!;
    [Dependency] private EntityLookupSystem _lookup = null!;
    [Dependency] private SharedDoAfterSystem _doafter = null!;
    [Dependency] private SharedActionsSystem _actions = null!;
    [Dependency] private IGameTiming _timing = null!;
    [Dependency] private IRobustRandom _rand = null!;

    [SubscribeLocalEvent]
    private void OnComponentInit(Entity<JumpscareComponent> ent, ref ComponentInit args) =>
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action);

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<JumpscareComponent> ent, ref ComponentShutdown args) =>
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);

    [SubscribeLocalEvent]
    private void OnJumpscareAction(Entity<JumpscareComponent> ent, ref JumpscareActionEvent args)
    {
        if (args.Handled)
            return;

        if (args.Target == ent.Owner)
        {
            if (!ent.Comp.CanMassJumpscare)
                return;

            var doafter = new DoAfterArgs(EntityManager, ent, ent.Comp.MassJumpscareDoAfterTime,
                new MassJumpscareDoAfterEvent(), ent)
            {
                BreakOnMove = true, BreakOnDamage = true, BreakOnWeightlessMove = true
            };

            _doafter.TryStartDoAfter(doafter);
            args.Handled = true;
            return;
        }

        DoJumpscareAct(ent, args.Target);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnMassJumpscareDoAfter(Entity<JumpscareComponent> ent, ref MassJumpscareDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;
        args.Handled = true;

        var nearby = _lookup.GetEntitiesInRange(Transform(ent).Coordinates, ent.Comp.MassRange);
        foreach (var target in nearby.Where(target => target != ent.Owner && HasComp<ActorComponent>(target)))
            DoJumpscareAct(ent, target);
    }

    private void DoJumpscareAct(Entity<JumpscareComponent> ent, EntityUid target)
    {
        if (HasComp<JumpscaredComponent>(target))
        {
            _popup.PopupClient(Loc.GetString("jumpscare-popup-immune", ("ent", MetaData(target).EntityName)), ent);
            return;
        }

        if (_state.IsDead(target))
        {
            _popup.PopupClient(Loc.GetString("jumpscare-popup-dead", ("ent", MetaData(target).EntityName)), ent);
            return;
        }

        var weighted = ent.Comp.Jumpscares.ToDictionary(jumpscare => jumpscare, jumpscare => jumpscare.Weight);
        var picked = _rand.PickPredicted(_timing, weighted);

        if (!_rand.ProbPredicted(_timing, picked.Prob))
        {
            _popup.PopupClient(Loc.GetString("jumpscare-popup-failed", ("ent", MetaData(target).EntityName)), ent);
            return;
        }

        _popup.PopupClient(Loc.GetString("jumpscare-popup-succeed", ("ent", MetaData(target).EntityName)), ent);

        var comp = new JumpscaredComponent
        {
            Jumpscare = picked,
            JumpscareAt = _timing.CurTime + picked.StartDelay,
            Jumpscarer = ent
        };
        AddComp(target, comp);
        Dirty(target, comp);
        _actions.SetCooldown(ent.Comp.ActionEntity, picked.CooldownTime);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<JumpscaredComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime >= comp.JumpscareAt && !comp.DidJumpscare)
            {
                comp.DidJumpscare = true;
                comp.RemoveSelfAt = _timing.CurTime + comp.Jumpscare.TotalTime + comp.Jumpscare.ImmunityTime;
                Dirty(uid, comp);
                Jumpscare((uid, comp));
                continue;
            }

            if (_timing.CurTime < comp.RemoveSelfAt || !comp.DidJumpscare)
                continue;
            RemComp(uid, comp);
            if (comp.Jumpscare.FreezeTarget && TryComp<InputMoverComponent>(uid, out var input))
                input.CanMove = true;
        }
    }

    protected virtual void Jumpscare(Entity<JumpscaredComponent> ent)
    {
        if (ent.Comp.Jumpscare.SelfDestruct) PredictedQueueDel(ent.Comp.Jumpscarer);
        if (ent.Comp.Jumpscare.AshTarget)
        {
            PredictedQueueDel(ent);
            return;
        }

        if (ent.Comp.Jumpscare.KnockdownTarget)
            _stun.TryKnockdown(ent.Owner, ent.Comp.Jumpscare.KnockdownTime, force: true);
        if (ent.Comp.Jumpscare.FreezeTarget && TryComp<InputMoverComponent>(ent, out var input))
            input.CanMove = false;
        if (ent.Comp.Jumpscare.Damage is null) return;
        _damage.ChangeDamage(ent.Owner, ent.Comp.Jumpscare.Damage, true, true, ent, canHeal: true);
    }
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class JumpscareData
{
    /// <summary>
    /// Sprite/Texture to flash on target's screen.
    /// </summary>
    [DataField] public SpriteSpecifier? Sprite;

    /// <summary>
    /// Sound to play for target when getting spooked.
    /// </summary>
    [DataField] public SoundSpecifier? Sound;

    /// <summary>
    /// How long to wait before actually starting the jumpscare.
    /// </summary>
    [DataField] public TimeSpan StartDelay = TimeSpan.Zero;

    /// <summary>
    /// How long to fade in before holding.
    /// </summary>
    [DataField] public TimeSpan FadeInTime = TimeSpan.Zero;

    /// <summary>
    /// How long to hold the sprite on screen for.
    /// </summary>
    [DataField] public TimeSpan HoldTime = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How long to fade out after holding.
    /// </summary>
    [DataField] public TimeSpan FadeTime = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How long the sprite is on screen for in total, from the start of the fade in to the end of the fade out.
    /// </summary>
    public TimeSpan TotalTime => FadeInTime + HoldTime + FadeTime;

    /// <summary>
    /// How long before entity can jumpscare again.
    /// </summary>
    [DataField] public TimeSpan CooldownTime = TimeSpan.Zero;

    /// <summary>
    /// Time that <see cref="JumpscaredComponent"/> will remain on the target, acting as immunity.
    /// </summary>
    [DataField] public TimeSpan ImmunityTime = TimeSpan.Zero;

    /// <summary>
    /// The time to knock down the target for if <see cref="KnockdownTarget"/> is <see langword="true"/>.
    /// </summary>
    [DataField] public TimeSpan KnockdownTime = TimeSpan.Zero;

    /// <summary>
    /// How much damage to deal to the target once the jumpscare starts.
    /// </summary>
    [DataField] public DamageSpecifier? Damage;

    /// <summary>
    /// Should target be frozen in place during the jumpscare?
    /// </summary>
    [DataField] public bool FreezeTarget;

    /// <summary>
    /// Should the target trip when jumpscared?
    /// </summary>
    [DataField] public bool KnockdownTarget;

    /// <summary>
    /// Should the target be turned to ash when jumpscared? (ashbaby)
    /// </summary>
    [DataField] public bool AshTarget;

    /// <summary>
    /// Should the jumpscare sound be audible to other players than just the target?
    /// </summary>
    [DataField] public bool SoundAudibleToOthers;

    /// <summary>
    /// Should the jumpscare sprite render above the UI elements?
    /// </summary>
    [DataField] public bool RenderAboveUI;

    /// <summary>
    /// Should entity delete itself after performing jumpscare? (one-off)
    /// </summary>
    [DataField] public bool SelfDestruct;

    /// <summary>
    /// Weight of being picked, if in a list.
    /// </summary>
    [DataField] public float Weight = 1;

    /// <summary>
    /// Probability of jumpscare succeeding.
    /// </summary>
    [DataField] public float Prob = 1;
}

[Serializable, NetSerializable]
public sealed class JumpscarePanelEuiState(NetEntity? lockedTarget, List<JumpscarePlayerInfo> players) : EuiStateBase
{
    public readonly NetEntity? LockedTarget = lockedTarget;
    public readonly List<JumpscarePlayerInfo> Players = players;
}

[Serializable, NetSerializable]
public readonly record struct JumpscarePlayerInfo(
    NetEntity Entity,
    string Username,
    string EntityName,
    bool IsGhost,
    bool IsAlive);
