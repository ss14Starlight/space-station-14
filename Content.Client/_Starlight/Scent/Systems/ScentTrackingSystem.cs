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

// Filters visible ScentMarker sprites to the local player's tracked scent, and plays each
// marker's fade animation.
public sealed class ScentTrackingSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly AnimationPlayerSystem _animation = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedInternalsSystem _internals = default!;

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

        SubscribeLocalEvent<SmellerComponent, AfterAutoHandleStateEvent>(OnSmellerState);
        SubscribeLocalEvent<ScentMarkerComponent, ComponentStartup>(OnMarkerStartup);
        SubscribeLocalEvent<ScentMarkerComponent, AfterAutoHandleStateEvent>(OnMarkerState);
        SubscribeLocalEvent<SmellerComponent, EntGotInsertedIntoContainerMessage>(OnOwnEnclosureChanged);
        SubscribeLocalEvent<SmellerComponent, EntGotRemovedFromContainerMessage>(OnOwnEnclosureChanged);
    }

    private void OnOwnEnclosureChanged<T>(Entity<SmellerComponent> ent, ref T args)
    {
        if (_player.LocalSession?.AttachedEntity == ent.Owner)
            RefreshAllMarkers(ent.Comp);
    }

    private void OnSmellerState(EntityUid uid, SmellerComponent component, ref AfterAutoHandleStateEvent args)
    {
        if (_player.LocalSession?.AttachedEntity != uid)
            return;

        RefreshAllMarkers(component);
    }

    private void OnMarkerStartup(Entity<ScentMarkerComponent> ent, ref ComponentStartup args)
    {
        PlayFadeAnimation(ent);
        ApplyFilterForLocalPlayer(ent);
    }

    private void OnMarkerState(Entity<ScentMarkerComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        PlayFadeAnimation(ent);
        ApplyFilterForLocalPlayer(ent);
    }

    // Re-checked on every networked update to this marker, not just the first. SpriteComponent's
    // own Visible field is itself part of its networked state, and a later resync of it alone can
    // silently undo our filter without re-firing ScentMarkerComponent's own ComponentStartup.
    private void ApplyFilterForLocalPlayer(Entity<ScentMarkerComponent> ent)
    {
        if (!TryGetLocalSmeller(out var smeller))
            return;

        var enclosure = TryGetOwnEnclosure(out var own) ? own : (EntityUid?)null;
        ApplyFilter(ent, smeller, enclosure);
    }

    // Resolves the local player's own SmellerComponent, if they have one attached.
    private bool TryGetLocalSmeller(out SmellerComponent smeller)
    {
        if (_player.LocalSession?.AttachedEntity is { } local && TryComp(local, out SmellerComponent? comp))
        {
            smeller = comp;
            return true;
        }

        smeller = default!;
        return false;
    }

    private void RefreshAllMarkers(SmellerComponent smeller)
    {
        var enclosure = TryGetOwnEnclosure(out var own) ? own : (EntityUid?)null;

        var query = EntityQueryEnumerator<ScentMarkerComponent>();
        while (query.MoveNext(out var uid, out var marker))
        {
            ApplyFilter((uid, marker), smeller, enclosure);
        }
    }

    private void ApplyFilter(Entity<ScentMarkerComponent> ent, SmellerComponent smeller, EntityUid? enclosure)
    {
        if (!TryComp<SpriteComponent>(ent.Owner, out var sprite))
            return;

        var visible = !IsPerceptionBlocked(smeller) &&
                      !(smeller.Perception == ScentPerception.Partial && ent.Comp.WasContained) &&
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

        return !TryComp<TransformComponent>(ent.Owner, out var markerXform) ||
               markerXform.ParentUid != own;
    }

    // Resolves the airtight container the local player is currently inside, if any.
    private bool TryGetOwnEnclosure(out EntityUid enclosure)
    {
        enclosure = default;

        if (_player.LocalSession?.AttachedEntity is not { } local ||
            !TryComp<TransformComponent>(local, out var localXform) ||
            !TryComp<EntityStorageComponent>(localXform.ParentUid, out var storage) ||
            !storage.Airtight)
        {
            return false;
        }

        enclosure = localXform.ParentUid;
        return true;
    }

    // A Partial perceiver loses all scent perception while breathing internals.
    private bool IsPerceptionBlocked(SmellerComponent smeller)
    {
        if (smeller.Perception != ScentPerception.Partial)
            return false;

        return _player.LocalSession?.AttachedEntity is { } local && _internals.AreInternalsWorking(local);
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
        var startColor = GetScentColor(ent.Comp.ScentId).WithAlpha(startAlpha);
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
        if (!TryGetLocalSmeller(out var smeller) ||
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
        if (!ent.Comp.WasContained)
            return strength;

        return strength * ContainedSizeFraction;
    }

    // Convert.ToUInt32 is blocked by the client sandbox and kills the client silently on startup.
    private static Color GetScentColor(string scentId)
    {
        if (scentId.Length < 8)
            return Color.White;

        var seed = uint.Parse(scentId[..8], NumberStyles.HexNumber);
        var hue = (seed % 360) / 360f;
        return Color.FromHsv(new Vector4(hue, 0.85f, 1f, 1f));
    }
}
