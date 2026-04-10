using System.Linq;
using Content.Server._Starlight.Computers.RemoteEye;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.DoAfter;
using Content.Shared.Effects;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Starlight.Antags.Abductor;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;

namespace Content.Server.Starlight.Antags.Abductor;

public sealed partial class AbductorSystem : SharedAbductorSystem
{
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;
    [Dependency] private readonly PullingSystem _pullingSystem = default!;
    [Dependency] private readonly InventorySystem _inv = default!;
    [Dependency] private readonly SharedCuffableSystem _cuffs = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly RemoteEyeSystem _remoteEye = default!;

    private static readonly EntProtoId _teleportationEffect = "EffectTeleportation";
    private static readonly EntProtoId _teleportationEffectEntity = "EffectTeleportationEntity";
    public void InitializeActions()
    {
        SubscribeLocalEvent<AbductorScientistComponent, ComponentStartup>(AbductorScientistComponentStartup);
        SubscribeLocalEvent<AbductorAgentComponent, ComponentStartup>(AbductorAgentComponentStartup);

        SubscribeLocalEvent<AbductorReturnToShipEvent>(OnReturn);
        SubscribeLocalEvent<AbductorScientistComponent, AbductorReturnDoAfterEvent>(OnDoAfterAbductorScientistReturn);
        SubscribeLocalEvent<AbductorAgentComponent, AbductorReturnDoAfterEvent>(OnDoAfterAbductorAgentReturn);

        SubscribeLocalEvent<SendYourselfEvent>(OnSendYourself);
        SubscribeLocalEvent<AbductorScientistComponent, AbductorSendYourselfDoAfterEvent>(OnDoAfterAbductorScientistSendYourself);
        SubscribeLocalEvent<AbductorAgentComponent, AbductorSendYourselfDoAfterEvent>(OnDoAfterAbductorAgentSendYourself);

        SubscribeLocalEvent<GizmoMarkEvent>(OnGizmoMark);
    }

    private void AbductorScientistComponentStartup(Entity<AbductorScientistComponent> ent, ref ComponentStartup args)
        => InitializeComponentStartup(ent.Owner, ent.Comp);

    private void AbductorAgentComponentStartup(Entity<AbductorAgentComponent> ent, ref ComponentStartup args)
        => InitializeComponentStartup(ent.Owner, ent.Comp);

    private void InitializeComponentStartup(EntityUid uid, Component comp)
    {
        var xform = Transform(uid);
        var console = _entityLookup.GetEntitiesInRange<AbductorConsoleComponent>(xform.Coordinates, 4, LookupFlags.Approximate | LookupFlags.Dynamic).FirstOrDefault();

        if (console == default)
            return;

        if (comp is AbductorScientistComponent sci)
        {
            sci.SpawnPosition = xform.Coordinates;
            console.Comp.Scientist = uid;
        }
        else if (comp is AbductorAgentComponent agent)
        {
            agent.SpawnPosition = xform.Coordinates;
            console.Comp.Agent = uid;
        }

        SyncAbductors(console);
    }

    private void OnReturn(AbductorReturnToShipEvent ev)
    {
        // Check if abductor is stunned or cuffed- if so, cancel the return
        if (HasComp<StunnedComponent>(ev.Performer))
        {
            _popup.PopupEntity(Loc.GetString("abductor-return-stunned"), ev.Performer, ev.Performer);
            return;
        }

        if (TryComp<CuffableComponent>(ev.Performer, out var cuffable) && _cuffs.IsCuffed((ev.Performer, cuffable)))
        {
            _popup.PopupEntity(Loc.GetString("abductor-return-cuffed"), ev.Performer, ev.Performer);
            return;
        }

        AbductorAgentComponent? agentComp = null;
        if (!TryComp<AbductorScientistComponent>(ev.Performer, out var scientistComp) && !TryComp<AbductorAgentComponent>(ev.Performer, out agentComp))
            EnsureComp<AbductorScientistComponent>(ev.Performer, out scientistComp);

        EntityCoordinates? spawnPosition = null;

        if (scientistComp != null && scientistComp.SpawnPosition.HasValue)
            spawnPosition = scientistComp.SpawnPosition.Value;
        else if (agentComp != null && agentComp.SpawnPosition.HasValue)
            spawnPosition = agentComp.SpawnPosition.Value;

        if (spawnPosition == null)
            return;

        _color.RaiseEffect(Color.FromHex("#BA0099"), new List<EntityUid>(1) { ev.Performer }, Filter.Pvs(ev.Performer, entityManager: EntityManager));
        EnsureComp<TransformComponent>(ev.Performer, out var xform);

        var effectEnt = SpawnAttachedTo(_teleportationEffectEntity, xform.Coordinates);
        _xformSys.SetParent(effectEnt, ev.Performer);
        EnsureComp<TimedDespawnComponent>(effectEnt, out var despawnEffectEntComp);
        despawnEffectEntComp.Lifetime = 3.0f;
        _audioSystem.PlayPvs(_alienTeleport, effectEnt);

        var effect = _entityManager.SpawnEntity(_teleportationEffect, spawnPosition.Value);
        EnsureComp<TimedDespawnComponent>(effect, out var despawnComp);
        despawnComp.Lifetime = 3.0f;
        _audioSystem.PlayPvs(_alienTeleport, effect);

        var doAfter = new DoAfterArgs(EntityManager, ev.Performer, TimeSpan.FromSeconds(3), new AbductorReturnDoAfterEvent(), ev.Performer)
        {
            BreakOnDamage = true
        };


        _doAfter.TryStartDoAfter(doAfter);
        ev.Handled = true;
    }

    private void OnDoAfterAbductorScientistReturn(Entity<AbductorScientistComponent> ent, ref AbductorReturnDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        // Check again if abductor is stunned, cuffed, or dead during DoAfter
        if (HasComp<StunnedComponent>(ent) ||
            (TryComp<CuffableComponent>(ent, out var cuffable) && _cuffs.IsCuffed((ent, cuffable))) ||
            _mobState.IsDead(ent))
            return;

        Return(ent, ent.Comp, null);
    }

    private void OnDoAfterAbductorAgentReturn(Entity<AbductorAgentComponent> ent, ref AbductorReturnDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        // Check again if abductor is stunned, cuffed, or dead during DoAfter
        if (HasComp<StunnedComponent>(ent) ||
            (TryComp<CuffableComponent>(ent, out var cuffable) && _cuffs.IsCuffed((ent, cuffable))) ||
            _mobState.IsDead(ent))
            return;

        Return(ent, null, ent.Comp);
    }

    private void Return(EntityUid uid, AbductorScientistComponent? scientistComp, AbductorAgentComponent? agentComp)
    {

        if (_pullingSystem.IsPulling(uid))
        {
            if (!TryComp<PullerComponent>(uid, out var pullerComp)
                || pullerComp.Pulling == null
                || !TryComp<PullableComponent>(pullerComp.Pulling.Value, out var pullableComp)
                || !_pullingSystem.TryStopPull(pullerComp.Pulling.Value, pullableComp)) return;
        }

        if (_pullingSystem.IsPulled(uid))
        {
            if (!TryComp<PullableComponent>(uid, out var pullableComp)
                || !_pullingSystem.TryStopPull(uid, pullableComp)) return;
        }

        EntityCoordinates? spawnPosition = null;

        if (scientistComp != null && scientistComp.SpawnPosition.HasValue)
            spawnPosition = scientistComp.SpawnPosition.Value;
        else if (agentComp != null && agentComp.SpawnPosition.HasValue)
            spawnPosition = agentComp.SpawnPosition.Value;

        if (spawnPosition == null)
            return;

        _xformSys.SetCoordinates(uid, spawnPosition.Value);
        _remoteEye.CameraExit(uid);
    }

    private void OnSendYourself(SendYourselfEvent ev)
    {
        _color.RaiseEffect(Color.FromHex("#BA0099"), new List<EntityUid>(1) { ev.Performer }, Filter.Pvs(ev.Performer, entityManager: EntityManager));
        EnsureComp<TransformComponent>(ev.Performer, out var xform);
        var effectEnt = SpawnAttachedTo(_teleportationEffectEntity, xform.Coordinates);
        _xformSys.SetParent(effectEnt, ev.Performer);
        EnsureComp<TimedDespawnComponent>(effectEnt);

        var effect = _entityManager.SpawnEntity(_teleportationEffect, ev.Target);
        EnsureComp<TimedDespawnComponent>(effect);

        var @event = new AbductorSendYourselfDoAfterEvent(GetNetCoordinates(ev.Target));
        var doAfter = new DoAfterArgs(EntityManager, ev.Performer, TimeSpan.FromSeconds(5), @event, ev.Performer);
        _doAfter.TryStartDoAfter(doAfter);
        ev.Handled = true;
    }

    private void OnDoAfterAbductorScientistSendYourself(Entity<AbductorScientistComponent> ent, ref AbductorSendYourselfDoAfterEvent args) => OnDoAfterSendYourself(ent, args);

    private void OnDoAfterAbductorAgentSendYourself(Entity<AbductorAgentComponent> ent, ref AbductorSendYourselfDoAfterEvent args) => OnDoAfterSendYourself(ent, args);

    private void OnDoAfterSendYourself(EntityUid ent, AbductorSendYourselfDoAfterEvent args)
    {
        if (_mobState.IsIncapacitated(ent))
            return;
        _color.RaiseEffect(Color.FromHex("#BA0099"), new List<EntityUid>(1) { ent }, Filter.Pvs(ent, entityManager: EntityManager));
        if (_pullingSystem.IsPulling(ent))
        {
            if (!TryComp<PullerComponent>(ent, out var pullerComp)
                || pullerComp.Pulling == null
                || !TryComp<PullableComponent>(pullerComp.Pulling.Value, out var pullableComp)
                || !_pullingSystem.TryStopPull(pullerComp.Pulling.Value, pullableComp)) return;
        }

        if (_pullingSystem.IsPulled(ent))
        {
            if (!TryComp<PullableComponent>(ent, out var pullableComp)
                || !_pullingSystem.TryStopPull(ent, pullableComp)) return;
        }
        _xformSys.SetCoordinates(ent, GetCoordinates(args.TargetCoordinates));
        _remoteEye.CameraExit(ent);
    }

    private void OnGizmoMark(GizmoMarkEvent ev)
    {
        if (!HasComp<AbductorComponent>(ev.Target))
            return;

        if (!_inv.TryGetSlotContainer(ev.Performer, "pocket1", out var pocket1, out _) || !_inv.TryGetSlotContainer(ev.Performer, "pocket2", out var pocket2, out _))
            return;

        var pocket1PossibleGizmo = pocket1.ContainedEntity;
        var pocket2PossibleGizmo = pocket2.ContainedEntity;

        if (TryComp<AbductorGizmoComponent>(pocket1PossibleGizmo, out var pocket1Gizmo))
            pocket1Gizmo.Target = GetNetEntity(ev.Target);
        else if (TryComp<AbductorGizmoComponent>(pocket2PossibleGizmo, out var pocket2Gizmo))
            pocket2Gizmo.Target = GetNetEntity(ev.Target);

    }
}
