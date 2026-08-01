using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.DoAfter;
using Content.Server.Forensics;
using Content.Server.Popups;
using Content.Server._Starlight.Scent.Components;
using Content.Shared._Starlight.Scent;
using Content.Shared._Starlight.Scent.Components;
using Content.Shared._Starlight.Scent.Systems;
using Content.Shared.Atmos;
using Content.Shared.DoAfter;
using Content.Shared.Eye;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Verbs;
using Content.Shared.Zombies;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.Scent.Systems;

public sealed class ScentSystem : SharedScentSystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    private const string ScentMarkerPrototype = "ScentMarker";
    private const int ScentIdByteLength = 8;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ScentComponent, MapInitEvent>(OnScentMapInit);
        SubscribeLocalEvent<ScentComponent, ContactInteractionEvent>(OnContactInteraction);
        SubscribeLocalEvent<SmellerComponent, GetVisMaskEvent>(OnGetVisMask);

        SubscribeLocalEvent<SmellerComponent, SniffObjectActionEvent>(OnSniffObjectAction);
        SubscribeLocalEvent<SmellerComponent, SniffObjectDoAfterEvent>(OnSniffObjectDoAfter);
        SubscribeLocalEvent<SmellerComponent, ScentSniffTrackMessage>(OnTrackMessage);
        SubscribeLocalEvent<SmellerComponent, BoundUIClosedEvent>(OnSniffMenuClosed);
        SubscribeLocalEvent<SmellerComponent, MoveEvent>(OnSmellerMove);
        SubscribeLocalEvent<SmellerComponent, EntityZombifiedEvent>(OnSmellerZombified);

        SubscribeLocalEvent<CleansScentComponent, AfterInteractEvent>(OnCleanAfterInteract,
            before: [typeof(ForensicsSystem), typeof(IngestionSystem)]);
        SubscribeLocalEvent<CleansScentComponent, CleanScentDoAfterEvent>(OnCleanScentDoAfter);
        SubscribeLocalEvent<CleansScentComponent, GetVerbsEvent<UtilityVerb>>(OnUtilityVerb);
    }

    private void OnSniffObjectAction(Entity<SmellerComponent> ent, ref SniffObjectActionEvent args)
    {
        if (args.Handled)
            return;

        TryComp<ScentTraceComponent>(args.Target, out var trace);
        if (trace != null)
            PruneExpiredTraces(trace);

        if (trace == null || trace.Scents.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("scent-sniff-no-scents", ("target", Name(args.Target))), args.Target, ent.Owner);
            args.Handled = true;
            return;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, ent.Owner, ent.Comp.SniffDelay,
            new SniffObjectDoAfterEvent(), ent.Owner, target: args.Target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };

        _doAfterSystem.TryStartDoAfter(doAfterArgs);
        _audio.PlayPvs(ent.Comp.SniffSound, ent.Owner);
        args.Handled = true;
    }

    private void OnSniffObjectDoAfter(EntityUid uid, SmellerComponent component, SniffObjectDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Target is not { } target)
            return;

        if (!TryComp<ScentTraceComponent>(target, out var trace))
            return;

        PruneExpiredTraces(trace);

        var now = _timing.CurTime;
        var entries = new List<ScentTraceEntry>(trace.Scents.Count);
        foreach (var (scentId, info) in trace.Scents)
        {
            var speciesName = Loc.GetString("scent-species-non-humanoid");
            if (info.Species != null && _prototype.TryIndex<SpeciesPrototype>(info.Species, out var species))
                speciesName = Loc.GetString(species.Name);

            var age = (float)(now - info.LastTouched).TotalSeconds;
            entries.Add(new ScentTraceEntry(scentId, GetFreshness(age, trace.TraceLifetime), speciesName));
        }

        if (!_ui.TryOpenUi(uid, ScentSniffUiKey.Key, uid))
        {
            Log.Warning($"{ToPrettyString(uid)} has SmellerComponent but couldn't open ScentSniffUiKey - " +
                        "does its prototype have a matching '- type: UserInterface' block?");
            return;
        }

        component.SniffTarget = target;
        _ui.SetUiState(uid, ScentSniffUiKey.Key, new ScentSniffBoundUserInterfaceState(entries));

        args.Handled = true;
    }

    private void OnSniffMenuClosed(Entity<SmellerComponent> ent, ref BoundUIClosedEvent args)
    {
        if (args.UiKey is ScentSniffUiKey)
            ent.Comp.SniffTarget = null;
    }

    private void OnSmellerMove(Entity<SmellerComponent> ent, ref MoveEvent args)
    {
        if (ent.Comp.SniffTarget is not { } target || !TryComp<TransformComponent>(target, out var targetXform))
            return;

        if (_transform.InRange(args.NewPosition, targetXform.Coordinates, ent.Comp.SniffRange))
            return;

        _ui.CloseUi(ent.Owner, ScentSniffUiKey.Key);
        ent.Comp.SniffTarget = null;
    }

    // Zombies shouldn't be able to hunt survivors by scent.
    private void OnSmellerZombified(Entity<SmellerComponent> ent, ref EntityZombifiedEvent args)
    {
        RemComp<SmellerComponent>(ent.Owner);
    }

    private void OnTrackMessage(EntityUid uid, SmellerComponent component, ScentSniffTrackMessage args)
    {
        if (component.SniffTarget is not { } target || !Exists(target))
            return;

        if (!TryComp<TransformComponent>(uid, out var xform) || !TryComp<TransformComponent>(target, out var targetXform))
            return;

        if (!_transform.InRange(xform.Coordinates, targetXform.Coordinates, component.SniffRange))
            return;

        if (!TryComp<ScentTraceComponent>(target, out var trace) || !trace.Scents.ContainsKey(args.ScentId))
            return;

        SetTrackedScent((uid, component), args.ScentId);
        _popup.PopupEntity(Loc.GetString("scent-sniff-window-tracking-popup"), uid, uid);
    }

    private void OnCleanAfterInteract(Entity<CleansScentComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        args.Handled = TryStartCleanEvidence(ent, args.User, args.Target.Value);
    }

    // Right-click fallback for when AfterInteractEvent gets intercepted (e.g. soap on food).
    private void OnUtilityVerb(Entity<CleansScentComponent> entity, ref GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var user = args.User;
        var target = args.Target;

        var verb = new UtilityVerb()
        {
            Act = () => TryStartCleanEvidence(entity, user, target),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/bubbles.svg.192dpi.png")),
            Text = Loc.GetString("scent-verb-text"),
            Message = Loc.GetString("scent-verb-message"),
            DoContactInteraction = false,
        };

        args.Verbs.Add(verb);
    }

    // Cleans scent and forensic evidence together in one pass.
    private bool TryStartCleanEvidence(Entity<CleansScentComponent> cleaner, EntityUid user, EntityUid target)
    {
        var isSelf = target == user;

        TryComp<ScentTraceComponent>(target, out var trace);
        var hasScent = isSelf ? HasComp<ScentComponent>(target) : trace != null && trace.Scents.Count > 0;

        var hasForensics = TryComp<ForensicsComponent>(target, out var forensics) &&
                            (forensics.Fingerprints.Count + forensics.Fibers.Count > 0 ||
                             (forensics.DNAs.Count > 0 && forensics.CanDnaBeCleaned));

        if (!hasScent && !hasForensics)
        {
            if (!HasComp<CleansForensicsComponent>(cleaner.Owner))
            {
                _popup.PopupEntity(
                    Loc.GetString(isSelf ? "scent-cleaning-cannot-clean-self" : "scent-cleaning-cannot-clean-other",
                        ("target", Identity.Entity(target, EntityManager))),
                    user, user, PopupType.MediumCaution);
            }

            return false;
        }

        string evidence;
        if (hasScent && hasForensics)
            evidence = Loc.GetString("scent-evidence-both");
        else if (hasScent)
            evidence = Loc.GetString("scent-evidence-scent");
        else
            evidence = Loc.GetString("scent-evidence-forensics");

        var doAfterArgs = new DoAfterArgs(EntityManager, user, cleaner.Comp.CleanDelay,
            new CleanScentDoAfterEvent(), cleaner, target: target, used: cleaner)
        {
            NeedHand = true,
            BreakOnDamage = true,
            BreakOnMove = true,
            MovementThreshold = cleaner.Comp.MovementThreshold,
        };

        if (!isSelf)
        {
            if (trace != null && forensics != null && hasScent && hasForensics)
                doAfterArgs.DistanceThreshold = Math.Min(trace.CleanDistance, forensics.CleanDistance);
            else if (trace != null && hasScent)
                doAfterArgs.DistanceThreshold = trace.CleanDistance;
            else if (forensics != null && hasForensics)
                doAfterArgs.DistanceThreshold = forensics.CleanDistance;
        }

        _doAfterSystem.TryStartDoAfter(doAfterArgs);
        _popup.PopupEntity(
            Loc.GetString(isSelf ? "scent-cleaning-self" : "scent-cleaning-other",
                ("evidence", evidence), ("target", Identity.Entity(target, EntityManager))),
            user, user);
        return true;
    }

    private void OnCleanScentDoAfter(EntityUid uid, CleansScentComponent component, CleanScentDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Target is not { } target)
            return;

        if (target == args.User)
            RandomizeScent((target, null));

        if (TryComp<ScentTraceComponent>(target, out var trace))
            trace.Scents.Clear();

        if (TryComp<ForensicsComponent>(target, out var forensics))
        {
            forensics.Fibers.Clear();
            forensics.Fingerprints.Clear();
            if (forensics.CanDnaBeCleaned)
                forensics.DNAs.Clear();
        }

        args.Handled = true;
    }

    private void OnGetVisMask(Entity<SmellerComponent> ent, ref GetVisMaskEvent args)
    {
        if (ent.Comp.Sniffing)
            args.VisibilityMask |= (int)VisibilityFlags.Scent;
    }

    // PVS networking decisions can't be predicted client-side.
    public override void SetSniffing(Entity<SmellerComponent> ent, bool sniffing)
    {
        base.SetSniffing(ent, sniffing);
        _eye.RefreshVisibilityMask(ent.Owner);
    }

    private void OnScentMapInit(Entity<ScentComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.ScentId == null)
            RandomizeScent((ent.Owner, ent.Comp));
    }

    public override void RandomizeScent(Entity<ScentComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        var scent = new byte[ScentIdByteLength];
        _random.NextBytes(scent);
        ent.Comp.ScentId = Convert.ToHexString(scent);
        Dirty(ent.Owner, ent.Comp);
    }

    private void OnContactInteraction(EntityUid uid, ScentComponent component, ContactInteractionEvent args)
    {
        if (component.ScentId == null)
            return;

        ApplyScentTrace(uid, component.ScentId, args.Other);
    }

    private void ApplyScentTrace(EntityUid depositor, string scentId, EntityUid target)
    {
        if (HasComp<CleansScentComponent>(target))
            return;

        var species = TryComp<HumanoidAppearanceComponent>(depositor, out var appearance) ? (string?)appearance.Species : null;

        var trace = EnsureComp<ScentTraceComponent>(target);
        PruneExpiredTraces(trace);
        trace.Scents[scentId] = new ScentTraceInfo { LastTouched = _timing.CurTime, Species = species };
    }

    // Expiry is checked lazily, on read and write, not via a per-tick sweep.
    private void PruneExpiredTraces(ScentTraceComponent trace)
    {
        var now = _timing.CurTime;
        List<string>? expired = null;

        foreach (var (scentId, info) in trace.Scents)
        {
            if ((now - info.LastTouched).TotalSeconds > trace.TraceLifetime)
                (expired ??= new List<string>()).Add(scentId);
        }

        if (expired == null)
            return;

        foreach (var scentId in expired)
            trace.Scents.Remove(scentId);
    }

    private static ScentFreshness GetFreshness(float age, float lifetime)
    {
        var quarter = lifetime / 4f;

        if (age < quarter)
            return ScentFreshness.VeryFresh;
        if (age < quarter * 2f)
            return ScentFreshness.Fresh;
        if (age < quarter * 3f)
            return ScentFreshness.SomewhatFresh;

        return ScentFreshness.NotVeryFresh;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ScentComponent>();
        while (query.MoveNext(out var uid, out var scent))
        {
            if (scent.ScentId is not { } scentId)
                continue;

            if (scent.NextEmitTime == TimeSpan.Zero)
                scent.NextEmitTime = now + RollEmitDelay(scent);

            if (now < scent.NextEmitTime)
                continue;

            scent.NextEmitTime = now + RollEmitDelay(scent);

            if (TryComp<TransformComponent>(uid, out var xform))
                EmitScent((uid, scent, xform), scentId);
        }
    }

    private TimeSpan RollEmitDelay(ScentComponent scent)
    {
        var variance = scent.EmitInterval * scent.EmitIntervalVariance;
        var seconds = Math.Max(scent.MinEmitInterval, scent.EmitInterval + _random.NextFloat(-variance, variance));
        return TimeSpan.FromSeconds(seconds);
    }

    // Scales DecayTime toward MinDecayTime as pressure drops below one atmosphere.
    private TimeSpan GetDecayTime(ScentComponent scent, EntityUid emitter)
    {
        var pressure = _atmosphere.GetTileMixture(emitter)?.Pressure ?? 0f;
        var ratio = Math.Clamp(pressure / Atmospherics.OneAtmosphere, 0f, 1f);
        var seconds = Math.Max(scent.MinDecayTime, scent.DecayTime * ratio);
        return TimeSpan.FromSeconds(seconds);
    }

    private void EmitScent(Entity<ScentComponent, TransformComponent> ent, string scentId)
    {
        var (uid, scent, xform) = ent;

        if (IsSealed(uid))
            return;

        if (TryMergeIntoExisting(ent))
            return;

        var marker = Spawn(ScentMarkerPrototype, xform.Coordinates);
        var decayTime = GetDecayTime(scent, uid);

        var markerComp = Comp<ScentMarkerComponent>(marker);
        markerComp.ScentId = scentId;
        markerComp.ExpiresAt = _timing.CurTime + decayTime;
        markerComp.TotalDuration = decayTime;
        markerComp.WasContained = IsContained(xform);
        Dirty(marker, markerComp);

        var despawn = Comp<TimedDespawnComponent>(marker);
        despawn.Lifetime = (float)decayTime.TotalSeconds;

        scent.LastMarkerEntity = marker;
    }

    // Fully encased in pressure-protective gear - a sealed hardsuit/EVA setup. Nothing is
    // actually escaping into the room.
    private bool IsSealed(EntityUid uid)
    {
        var headSealed = _inventory.TryGetSlotEntity(uid, "head", out var head) &&
                          HasComp<PressureProtectionComponent>(head!.Value);
        var outerSealed = _inventory.TryGetSlotEntity(uid, "outerClothing", out var outer) &&
                           HasComp<PressureProtectionComponent>(outer!.Value);

        return headSealed && outerSealed;
    }

    // Is this entity's immediate parent an airtight container (locker, crate)?
    private bool IsContained(TransformComponent xform)
    {
        return TryComp<EntityStorageComponent>(xform.ParentUid, out var storage) && storage.Airtight;
    }

    // Only merges into our own chain tail, never any other nearby marker. Revisiting an old spot
    // would otherwise rewrite the trail's visit order.
    private bool TryMergeIntoExisting(Entity<ScentComponent, TransformComponent> ent)
    {
        var (uid, scent, xform) = ent;

        if (scent.LastMarkerEntity is not { } tail ||
            !TryComp<ScentMarkerComponent>(tail, out var marker) ||
            !TryComp<TransformComponent>(tail, out var tailXform))
        {
            return false;
        }

        if (!_transform.InRange(xform.Coordinates, tailXform.Coordinates, scent.MergeRadius))
            return false;

        var decayTime = GetDecayTime(scent, uid);

        marker.Strength = Math.Min(1f, marker.Strength + scent.MergeStrengthStep);
        marker.ExpiresAt = _timing.CurTime + decayTime;
        marker.TotalDuration = decayTime;
        marker.WasContained = IsContained(xform);
        Dirty(tail, marker);

        if (TryComp<TimedDespawnComponent>(tail, out var despawn))
            despawn.Lifetime = (float)decayTime.TotalSeconds;

        return true;
    }
}
