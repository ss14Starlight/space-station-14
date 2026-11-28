using Content.Client._Starlight.Computers.RemoteControl;
using Content.Shared._Starlight.Medical.Body.Systems;
using Content.Shared._Starlight.Scent.Components;
using Content.Shared.Storage.Components;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Animations;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using System.Globalization;
using System.Numerics;

namespace Content.Client._Starlight.Scent.Systems;

// Filters visible ScentMarker sprites to the current viewer's tracked scent, and plays each
// marker's fade animation.
public sealed partial class ScentTrackingSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private AnimationPlayerSystem _animation = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedInternalsSystem _internals = default!;

    private RemoteControlInterface _remoteControl = default!;
    private const string FadeAnimationKey = "scent-marker-fade";

    // MinScale corrects for the marker sprite's native size relative to a standard tile.
    private const float MinAlpha = 85f / 255f;
    private const float MaxAlpha = 127f / 255f;
    private const float MinScale = 32f / 96f;
    private const float MaxScale = 1.25f;

    // A Partial perceiver sees markers for less of their true life. A Full perceiver sees the
    // true ExpiresAt.
    private const float PartialVisibleFraction = 0.5f;

    // Container-sourced markers render smaller for any observer, Full or Partial.
    private const float ContainedSizeFraction = 0.25f;

    public override void Initialize()
    {
        base.Initialize();

        _remoteControl = EntityManager.System<RemoteControlInterface>();
        _remoteControl.ControlledEntityChanged += OnRemoteControlledEntityChanged;
        SubscribeLocalEvent<SmellerComponent, AfterAutoHandleStateEvent>(OnSmellerState);
        SubscribeLocalEvent<ScentMarkerComponent, ComponentStartup>(OnMarkerStartup);
        SubscribeLocalEvent<ScentMarkerComponent, AfterAutoHandleStateEvent>(OnMarkerState);
        SubscribeLocalEvent<SmellerComponent, EntGotInsertedIntoContainerMessage>(OnOwnEnclosureChanged);
        SubscribeLocalEvent<SmellerComponent, EntGotRemovedFromContainerMessage>(OnOwnEnclosureChanged);
    }

    public override void Shutdown()
    {
        if (_remoteControl is { } remoteControl)
            remoteControl.ControlledEntityChanged -= OnRemoteControlledEntityChanged;
        base.Shutdown();
    }

    private void OnOwnEnclosureChanged<T>(Entity<SmellerComponent> ent, ref T args)
    {
        if (IsCurrentViewer(ent.Owner))
            RefreshAllMarkers();
    }

    private void OnSmellerState(EntityUid uid, SmellerComponent component, ref AfterAutoHandleStateEvent args)
    {
        if (!IsCurrentViewer(uid))
            return;

        RefreshAllMarkers();
    }

    public void RefreshAfterSmellerShutdown(EntityUid uid)
    {
        if (IsCurrentViewer(uid))
            RefreshAllMarkers(uid);
    }

    private void OnMarkerStartup(Entity<ScentMarkerComponent> ent, ref ComponentStartup args)
    {
        PlayFadeAnimation(ent);
        ApplyFilterForCurrentViewer(ent);
    }

    private void OnMarkerState(Entity<ScentMarkerComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        PlayFadeAnimation(ent);
        ApplyFilterForCurrentViewer(ent);
    }

    private void OnRemoteControlledEntityChanged(EntityUid? entity)
        => RefreshAllMarkers();

    // Re-checked on every networked update to this marker, not just the first. SpriteComponent's
    // own Visible field is itself part of its networked state, and a later resync of it alone can
    // silently undo our filter without re-firing ScentMarkerComponent's own ComponentStartup.
    private void ApplyFilterForCurrentViewer(Entity<ScentMarkerComponent> ent)
    {
        if (!TryGetCurrentSmeller(out var viewer, out var smeller))
        {
            // No SmellerComponent: hide, since replay bypasses PVS and would otherwise leave
            // every recorded marker visible to a plain observer.
            if (TryComp<SpriteComponent>(ent.Owner, out var sprite))
                _sprite.SetVisible((ent.Owner, sprite), false);

            return;
        }

        var enclosure = TryGetOwnEnclosure(viewer, out var own) ? own : (EntityUid?)null;
        ApplyFilter(ent, viewer, smeller, enclosure);
    }

    private bool TryGetCurrentSmeller(out EntityUid viewer, out SmellerComponent smeller)
    {
        if (_remoteControl.ControlledEntity is { } remote)
        {
            if (TryComp(remote, out SmellerComponent? remoteSmeller))
            {
                viewer = remote;
                smeller = remoteSmeller;
                return true;
            }

            viewer = default;
            smeller = default!;
            return false;
        }

        if (_player.LocalSession?.AttachedEntity is { } local && TryComp(local, out SmellerComponent? localSmeller))
        {
            viewer = local;
            smeller = localSmeller;
            return true;
        }

        viewer = default;
        smeller = default!;
        return false;
    }

    private bool IsCurrentViewer(EntityUid uid)
        => _remoteControl.ControlledEntity is { } remote
            ? remote == uid
            : _player.LocalSession?.AttachedEntity == uid;

    private void RefreshAllMarkers(EntityUid? excludedViewer = null)
    {
        var hasSmeller = TryGetCurrentSmeller(out var viewer, out var smeller)
                         && viewer != excludedViewer;
        var enclosure = hasSmeller && TryGetOwnEnclosure(viewer, out var own) ? own : (EntityUid?)null;

        var query = EntityQueryEnumerator<ScentMarkerComponent>();
        while (query.MoveNext(out var uid, out var marker))
        {
            if (hasSmeller)
                ApplyFilter((uid, marker), viewer, smeller, enclosure);
            else if (TryComp<SpriteComponent>(uid, out var sprite))
                _sprite.SetVisible((uid, sprite), false);
        }
    }

    private void ApplyFilter(
        Entity<ScentMarkerComponent> ent,
        EntityUid viewer,
        SmellerComponent smeller,
        EntityUid? enclosure)
    {
        if (!TryComp<SpriteComponent>(ent.Owner, out var sprite))
            return;

        var visible = !IsPerceptionBlocked(viewer, smeller) &&
                        !(smeller.Perception == ScentPerception.Partial && ent.Comp.WasCloaked) &&
                        !(smeller.Perception == ScentPerception.Partial && ent.Comp.ContainedIn != null && !ent.Comp.WasDead) &&
                        !(smeller.Perception == ScentPerception.Partial && IsOutsideOwnEnclosure(ent, enclosure)) &&
                        (smeller.TrackedScentId == null || ent.Comp.ScentId == smeller.TrackedScentId);
        _sprite.SetVisible((ent.Owner, sprite), visible);
    }

    // A Partial perceiver inside an airtight container can't perceive markers outside it. A Full
    // perceiver is unaffected.
    private bool IsOutsideOwnEnclosure(Entity<ScentMarkerComponent> ent, EntityUid? enclosure)
    {
        if (enclosure is not { } own)
            return false;

        return ent.Comp.ContainedIn != own;
    }

    private bool TryGetOwnEnclosure(EntityUid viewer, out EntityUid enclosure)
    {
        enclosure = default;
        if (!TryComp(viewer, out TransformComponent? transform) ||
            !TryComp<EntityStorageComponent>(transform.ParentUid, out var storage) ||
            !storage.Airtight)
        {
            return false;
        }

        enclosure = transform.ParentUid;
        return true;
    }

    // A Partial perceiver loses all scent perception while breathing internals.
    private bool IsPerceptionBlocked(EntityUid viewer, SmellerComponent smeller)
    {
        if (smeller.Perception != ScentPerception.Partial)
            return false;

        return _internals.AreInternalsWorking(viewer);
    }

    // ExpiresAt is an absolute timestamp. Re-running this on PVS re-entry or a merge picks up
    // the correct remaining time.
    private void PlayFadeAnimation(Entity<ScentMarkerComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent.Owner, out var sprite))
            return;

        // This can run more than once per entity. Stop() first, since Play() throws on a
        // duplicate animation key.
        _animation.Stop(ent.Owner, FadeAnimationKey);

        var strength = Math.Clamp(ent.Comp.Strength, 0f, 1f);
        var scale = MathHelper.Lerp(MinScale, MaxScale, GetPerceivedSizeStrength(ent, strength));
        _sprite.SetScale((ent.Owner, sprite), new Vector2(scale, scale));

        var startAlpha = MathHelper.Lerp(MinAlpha, MaxAlpha, strength);
        var startColor = GetScentColor(ent).WithAlpha(startAlpha);
        var endColor = Color.Gray.WithAlpha(0f);
        var remaining = MathF.Max(0.1f, (float)(GetPerceivedExpiry(ent) - _timing.CurTime).TotalSeconds);

        var animation = new Animation
        {
            Length = TimeSpan.FromSeconds(remaining),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Color),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(startColor, 0f),
                        new AnimationTrackProperty.KeyFrame(endColor, remaining),
                    },
                },
            },
        };

        _animation.Play(ent.Owner, animation, FadeAnimationKey);
    }

    // A Full perceiver sees the true ExpiresAt. A Partial perceiver sees markers fade out
    // earlier.
    private TimeSpan GetPerceivedExpiry(Entity<ScentMarkerComponent> ent)
    {
        if (!TryGetCurrentSmeller(out _, out var smeller) ||
            smeller.Perception != ScentPerception.Partial ||
            ent.Comp.TotalDuration <= TimeSpan.Zero)
        {
            return ent.Comp.ExpiresAt;
        }

        var spawnedAt = ent.Comp.ExpiresAt - ent.Comp.TotalDuration;
        return spawnedAt + (ent.Comp.TotalDuration * PartialVisibleFraction);
    }

    // Affects the visual scale of an emission.
    private float GetPerceivedSizeStrength(Entity<ScentMarkerComponent> ent, float strength)
    {
        if (ent.Comp.ContainedIn == null)
            return strength;

        return strength * ContainedSizeFraction;
    }

    // Convert.ToUInt32 is blocked by the client sandbox and kills the client silently on startup.
    private static Color GetScentColor(Entity<ScentMarkerComponent> ent)
    {
        var scentId = ent.Comp.ScentId;

        if (ent.Comp.IsPollen)
        {
            uint hash = 2166136261;

            foreach (var c in scentId)
            {
                hash ^= c;
                hash *= 16777619;
            }

            var pollenHue = hash % 360 / 360f;
            return Color.FromHsv(new Vector4(pollenHue, 0.9f, 1f, 1f));
        }

        return GetScentColor(scentId);
    }

    internal static Color GetScentColor(string scentId)
    {
        if (scentId.Length < 8 ||
            !uint.TryParse(scentId[..8], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var seed))
            return Color.White;

        var hue = seed % 360 / 360f;
        return Color.FromHsv(new Vector4(hue, 0.85f, 1f, 1f));
    }
}
