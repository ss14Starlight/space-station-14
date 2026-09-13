using Content.Shared._Starlight.Scent.Components;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._Starlight.Scent.Systems;

/// <summary>
/// See ClientScentSystem for the concrete client-side subclass.
/// </summary>
public abstract partial class SharedScentSystem : EntitySystem
{
    [Dependency] protected SharedActionsSystem Actions = default!;
    [Dependency] protected SharedAudioSystem Audio = default!;
    [Dependency] protected SharedPopupSystem Popup = default!;
    [Dependency] protected MobStateSystem MobState = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SmellerComponent, ComponentInit>(OnSmellerInit);
        SubscribeLocalEvent<SmellerComponent, ComponentShutdown>(OnSmellerShutdown);
        SubscribeLocalEvent<SmellerComponent, ToggleSniffActionEvent>(OnToggleSniff);
        SubscribeLocalEvent<SmellerComponent, SneezeActionEvent>(OnSneeze);
        SubscribeLocalEvent<SmellerComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(Entity<SmellerComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == Content.Shared.Mobs.MobState.Dead)
            ClearTrackedScent(ent);
    }

    /// <summary>
    /// Runs when SmellerComponent initializes, whether it was present at spawn or granted later
    /// (i.e. an implant, a species change)
    /// </summary>
    /// <param name="uid">The entity the SmellerComponent was added to.</param>
    /// <param name="component">The newly added SmellerComponent.</param>
    /// <param name="args">Component initialization event args.</param>
    protected virtual void OnSmellerInit(EntityUid uid, SmellerComponent component, ComponentInit args)
        => Actions.AddAction(uid, ref component.ToggleActionEntity, component.ToggleAction);

    /// <summary>
    /// virtual so ClientScentSystem can override instead of subscribing to ComponentShutdown again.
    /// </summary>
    /// <param name="ent">The entity and SmellerComponent being removed.</param>
    /// <param name="args">Component shutdown event args.</param>
    protected virtual void OnSmellerShutdown(Entity<SmellerComponent> ent, ref ComponentShutdown args)
    {
        Actions.RemoveAction(ent.Owner, ent.Comp.ToggleActionEntity);
        Actions.RemoveAction(ent.Owner, ent.Comp.SneezeActionEntity);
        Actions.RemoveAction(ent.Owner, ent.Comp.SniffObjectActionEntity);
    }

    private void OnToggleSniff(Entity<SmellerComponent> ent, ref ToggleSniffActionEvent args)
    {
        if (args.Handled)
            return;

        SetSniffing(ent, !ent.Comp.Sniffing);
        args.Handled = true;
    }

    private void OnSneeze(Entity<SmellerComponent> ent, ref SneezeActionEvent args)
    {
        if (args.Handled)
            return;

        Sneeze(ent, predicted: true);
        args.Handled = true;
    }

    /// <summary>
    /// predicted excludes the performer from the server's broadcast; ForceSneeze doesn't want that.
    /// </summary>
    /// <param name="ent">Entity with a Smeller component.</param>
    /// <param name="predicted">Whether this is a predicted, player-initiated sneeze.</param>
    public void Sneeze(Entity<SmellerComponent> ent, bool predicted = false)
    {
        ClearTrackedScent(ent);
        Audio.PlayPredicted(ent.Comp.SneezeSound, ent.Owner, predicted ? ent.Owner : null);

        var message = Loc.GetString("scent-sneeze-popup");
        if (predicted)
            Popup.PopupClient(message, ent.Owner, ent.Owner);
        else
            Popup.PopupEntity(message, ent.Owner, ent.Owner);
    }

    /// <summary>
    /// Not the voluntary sneeze action. Locks out Toggle Smelling for 'lockout'
    /// </summary>
    /// <param name="ent">Entity with a Smeller component.</param>
    /// <param name="lockout">Lockout period, in seconds, to disable the smelling ability.</param>
    public void ForceSneeze(Entity<SmellerComponent> ent, TimeSpan lockout)
    {
        if (MobState.IsDead(ent.Owner))
            return;

        if (TryComp<ActionComponent>(ent.Comp.ToggleActionEntity, out var toggleAction) &&
            Actions.IsCooldownActive(toggleAction))
        {
            Actions.SetCooldown(ent.Comp.ToggleActionEntity, lockout);
            return;
        }

        Sneeze(ent);
        SetSniffing(ent, false);
        Actions.SetCooldown(ent.Comp.ToggleActionEntity, lockout);
    }

    public void SetTrackedScent(Entity<SmellerComponent> ent, string scentId, EntityUid? source = null)
    {
        if (ent.Comp.TrackedScentId == scentId)
            return;

        var previousScentId = ent.Comp.TrackedScentId;
        var hadTracked = previousScentId != null;
        ent.Comp.TrackedScentId = scentId;

        if (!hadTracked)
            Actions.AddAction(ent.Owner, ref ent.Comp.SneezeActionEntity, ent.Comp.SneezeAction);

        if (previousScentId != null)
            LogTrackedScent(ent.Owner, previousScentId, began: false);

        LogTrackedScent(ent.Owner, scentId, began: true, source);

        Dirty(ent);
    }

    public void ClearTrackedScent(Entity<SmellerComponent> ent)
    {
        if (ent.Comp.TrackedScentId is not { } scentId)
            return;

        ent.Comp.TrackedScentId = null;
        Actions.RemoveAction(ent.Owner, ent.Comp.SneezeActionEntity);

        LogTrackedScent(ent.Owner, scentId, began: false);

        Dirty(ent);
    }

    private void LogTrackedScent(EntityUid smeller, string scentId, bool began, EntityUid? source = null)
    {
        var verb = began ? "began" : "stopped";
        var hasOwner = TryResolveScentOwner(scentId, out var owner);

        if (source is { } src)
        {
            if (hasOwner)
                _adminLogger.Add(LogType.Scent,
                    $"{ToPrettyString(smeller):user} sniffed {ToPrettyString(src):source} and {verb} following scent trace belonging to {ToPrettyString(owner):target}.");
            else
                _adminLogger.Add(LogType.Scent,
                    $"{ToPrettyString(smeller):user} sniffed {ToPrettyString(src):source} and {verb} following an untraceable scent trace.");
        }
        else if (hasOwner)
        {
            _adminLogger.Add(LogType.Scent,
                $"{ToPrettyString(smeller):user} {verb} following scent trace belonging to {ToPrettyString(owner):target}.");
        }
        else
        {
            _adminLogger.Add(LogType.Scent, $"{ToPrettyString(smeller):user} {verb} following an untraceable scent trace.");
        }
    }

    private bool TryResolveScentOwner(string scentId, out EntityUid owner)
    {
        var query = EntityQueryEnumerator<ScentComponent>();
        while (query.MoveNext(out var uid, out var scent))
        {
            if (scent.ScentId == scentId)
            {
                owner = uid;
                return true;
            }
        }

        owner = default;
        return false;
    }

    /// <summary>
    /// Sneeze action grant/revoke happens in SetTrackedScent/ClearTrackedScent instead.
    /// </summary>
    /// <param name="ent">Entity with a Smeller component.</param>
    /// <param name="sniffing">Whether sniffing should be turned on or off.</param>
    public virtual void SetSniffing(Entity<SmellerComponent> ent, bool sniffing)
    {
        if (ent.Comp.Sniffing == sniffing)
            return;

        ent.Comp.Sniffing = sniffing;
        Actions.SetToggled(ent.Comp.ToggleActionEntity, sniffing);

        if (sniffing)
            Actions.AddAction(ent.Owner, ref ent.Comp.SniffObjectActionEntity, ent.Comp.SniffObjectAction);
        else
            Actions.RemoveAction(ent.Owner, ent.Comp.SniffObjectActionEntity);

        // The generic action-use log already covers Action; this adds the on/off detail under Scent.
        var state = sniffing ? "on" : "off";
        _adminLogger.Add(LogType.Scent, $"{ToPrettyString(ent.Owner):user} toggled sniffing {state}.");

        Dirty(ent);
    }

    public virtual void RandomizeScent(Entity<ScentComponent?> ent) { }
}
