using System.Linq;
using System.Numerics;
using Content.Shared._Starlight.MindCollective;
using Content.Shared._Starlight.BodyControl;
using Content.Shared._Starlight.Overlay.Components;
using Content.Shared._Starlight.Overlay.Systems;
using Content.Shared._Starlight.Sound;
using Content.Shared.Atmos;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server._Starlight.MindCollective;

public sealed partial class MindCollectiveSystem
{
    [Dependency] private SharedEyeSystem _collectiveEye = default!;
    [Dependency] private SharedViewSubscriberSystem _collectiveViews = default!;
    [Dependency] private BlindableSystem _collectiveBlindness = default!;
    [Dependency] private FlashImmunitySystem _collectiveVision = default!;

    private readonly Dictionary<EntityUid, ICommonSession> _observerSessions = new();

    /// <summary>Changes the view of an accepted participant currently controlling their observer.</summary>
    public bool Observe(EntityUid observer, EntityUid body)
    {
        if (!TryGetObserver(observer, out var view, out var collective, out var actor)
            || !IsRegisteredBody(view.Collective, collective, body)
            || !_mind.TryGetMind(observer, out var mind, out _) || !HasPermission(mind, "body.observe", body))
            return false;

        if (TryComp<BodyControlSourceComponent>(observer, out var control) && control.Body != body)
            _bodyControl.StopControl(observer);
        SyncObserverViews(observer, view, collective, actor.PlayerSession);
        SetObserverView(observer, view, body);
        return true;
    }

    public void UpdateObserver(EntityUid observer, MindCollectiveComponent collective)
    {
        if (!TryGetObserver(observer, out var view, out var current, out var actor) || current != collective)
        {
            RemoveObserverViews(observer);
            return;
        }

        SyncObserverViews(observer, view, collective, actor.PlayerSession);
        if (view.Viewing is { } body && view.Bodies.Contains(body))
            SetObserverView(observer, view, body);
        else if (collective.ActiveBody is { } active && view.Bodies.Contains(active))
            SetObserverView(observer, view, active);
        else if (view.Bodies.Count > 0)
            SetObserverView(observer, view, view.Bodies[0]);
        else
            ClearObserverView(observer, view);
    }

    private bool TryGetObserver(EntityUid observer, out MindCollectiveObserverComponent view,
        out MindCollectiveComponent collective, out ActorComponent actor)
    {
        view = default!;
        collective = default!;
        actor = default!;
        return !TerminatingOrDeleted(observer)
               && TryComp(observer, out view!)
               && TryComp(view.Collective, out collective!) && !collective.Closing
               && TryGetActorMind(observer, view.Collective, collective, out var mind)
               && collective.Members.TryGetValue(mind, out var member) && member.Observer == observer
               && TryComp(observer, out actor!);
    }

    private void SyncObserverViews(EntityUid observer, MindCollectiveObserverComponent view,
        MindCollectiveComponent collective, ICommonSession session)
    {
        if (_observerSessions.TryGetValue(observer, out var previous) && previous != session)
            RemoveObserverViews(observer);

        _observerSessions[observer] = session;
        _mind.TryGetMind(observer, out var mind, out _);
        var bodies = collective.Bodies.Where(body => IsRegisteredBody(view.Collective, collective, body)
            && HasPermission(mind, "body.observe", body)).ToList();
        foreach (var removed in view.Bodies.Except(bodies))
        {
            if (!Deleted(removed))
                _collectiveViews.RemoveViewSubscriber(removed, session);
        }
        foreach (var body in bodies)
            _collectiveViews.AddViewSubscriber(body, session);
        SyncObserverHearing(observer, view, bodies);
        if (view.Bodies.SequenceEqual(bodies))
            return;
        view.Bodies = bodies;
        Dirty(observer, view);
    }

    private void SyncObserverHearing(EntityUid observer, MindCollectiveObserverComponent view, List<EntityUid> bodies)
    {
        var hearing = bodies.Select(body => TryComp<HearingPressureComponent>(body, out var state)
            ? new MindCollectiveHearingState(GetNetEntity(body), state.Pressure, state.SealedHelmet)
            : new MindCollectiveHearingState(GetNetEntity(body), Atmospherics.OneAtmosphere, false)).ToList();
        if (view.Hearing.SequenceEqual(hearing))
            return;
        view.Hearing = hearing;
        Dirty(observer, view);
    }

    private void SetObserverView(EntityUid observer, MindCollectiveObserverComponent view, EntityUid body)
    {
        if (TryComp<BodyControlSourceComponent>(observer, out var control) && control.Body != body)
            _bodyControl.StopControl(observer);
        if (view.Viewing != body)
        {
            view.Viewing = body;
            Dirty(observer, view);
            if (view.AllowWorldSpeech)
                _collectiveLanguage.UpdateEntityLanguages(observer);
        }
        _mind.TryGetMind(observer, out var mind, out _);
        RefreshObserverWorldPermissions(observer, view, mind);
        var previous = TryComp<EyeComponent>(observer, out var eye) ? eye.Target : null;
        _collectiveEye.SetTarget(observer, body);
        // SetTarget removes its previous view subscription; the collective still listens through that body.
        if (previous is { } old && old != body && view.Bodies.Contains(old)
            && !TerminatingOrDeleted(old) && _observerSessions.TryGetValue(observer, out var session))
            _collectiveViews.AddViewSubscriber(old, session);
        if (Transform(observer).ParentUid != body)
            _transform.SetCoordinates(observer, new EntityCoordinates(body, Vector2.Zero));
        SyncObserverVision(observer, body);
        EnsureComp<BlindableComponent>(observer);
        _collectiveBlindness.UpdateIsBlind((observer, null), bypass: true);
    }

    private void ClearObserverView(EntityUid observer, MindCollectiveObserverComponent view)
    {
        if (view.Viewing != null)
        {
            view.Viewing = null;
            Dirty(observer, view);
        }
        view.Listening = null;
        _bodyControl.StopControl(observer);
        _mind.TryGetMind(observer, out var mind, out _);
        RefreshObserverWorldPermissions(observer, view, mind);
        if (HasComp<EyeComponent>(observer))
            _collectiveEye.SetTarget(observer, null);
        if (!TerminatingOrDeleted(observer))
        {
            SyncObserverVision(observer, null);
            EnsureComp<BlindableComponent>(observer);
            _collectiveBlindness.UpdateIsBlind((observer, null), bypass: true);
            _transform.AttachToGridOrMap(observer);
        }
    }

    private void SyncObserverVision(EntityUid observer, EntityUid? body)
    {
        if (body is not { } viewed || TerminatingOrDeleted(viewed))
        {
            RemComp<NightVisionComponent>(observer);
            RemComp<ThermalVisionComponent>(observer);
            return;
        }

        var blocked = _collectiveVision.HasFlashImmunityVisionBlockers(viewed);
        if (!blocked && TryComp<NightVisionComponent>(viewed, out var night) && night.Active
            && !(night.DisabilityBlockable && HasComp<NightVisionBlockerComponent>(viewed)))
        {
            var mirrored = EnsureComp<NightVisionComponent>(observer);
            if (!mirrored.Active)
            {
                mirrored.Active = true;
                Dirty(observer, mirrored);
            }
        }
        else
            RemComp<NightVisionComponent>(observer);

        if (!blocked && TryComp<ThermalVisionComponent>(viewed, out var thermal) && thermal.Active)
        {
            if (!TryComp<ThermalVisionComponent>(observer, out var mirrored)
                || !mirrored.Active || mirrored.GrantToggleAction
                || mirrored.UseAlternativeShader != thermal.UseAlternativeShader)
            {
                RemComp<ThermalVisionComponent>(observer);
                AddComp(observer, new ThermalVisionComponent
                {
                    Active = true,
                    UseAlternativeShader = thermal.UseAlternativeShader,
                    GrantToggleAction = false,
                });
            }
        }
        else
            RemComp<ThermalVisionComponent>(observer);
    }

    /// <summary>Removes all additional views even after the observer's ActorComponent has been removed.</summary>
    public void RemoveObserverViews(EntityUid observer)
    {
        _observerSessions.Remove(observer, out var session);
        if (!TryComp<MindCollectiveObserverComponent>(observer, out var view))
            return;
        if (session == null && TryComp<ActorComponent>(observer, out var actor))
            session = actor.PlayerSession;
        if (session != null)
        {
            foreach (var body in view.Bodies)
            {
                if (!Deleted(body))
                    _collectiveViews.RemoveViewSubscriber(body, session);
            }
        }
        if (view.Bodies.Count != 0 || view.Hearing.Count != 0)
        {
            view.Bodies.Clear();
            view.Hearing.Clear();
            Dirty(observer, view);
        }
        ClearObserverView(observer, view);
    }

    /// <summary>Called after removing a body from the pool and before deleting its entity.</summary>
    public void DetachObservers(EntityUid collectiveId, MindCollectiveComponent collective, EntityUid body)
    {
        foreach (var observer in collective.Members.Values.Select(member => member.Observer).Concat(collective.VacantObservers).Distinct().ToArray())
        {
            if (TerminatingOrDeleted(observer) || !TryComp<MindCollectiveObserverComponent>(observer, out var view)
                || view.Collective != collectiveId)
                continue;
            if (view.Viewing == body || Transform(observer).ParentUid == body)
                ClearObserverView(observer, view);
            if (view.Listening == body)
                view.Listening = null;
            UpdateObserver(observer, collective);
        }
    }

    [SubscribeLocalEvent]
    private void OnObserverDetached(Entity<MindCollectiveObserverComponent> ent, ref PlayerDetachedEvent args)
        => RemoveObserverViews(ent);

    [SubscribeLocalEvent]
    private void OnObserverShutdown(Entity<MindCollectiveObserverComponent> ent, ref ComponentShutdown args)
        => RepairObserver(ent);
}
