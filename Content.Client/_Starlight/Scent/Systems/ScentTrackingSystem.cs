using Content.Shared._Starlight.Scent.Components;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Animations;
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

    private const string FadeAnimationKey = "scent-marker-fade";

    // MinScale corrects for the marker sprite's native size relative to a standard tile.
    private const float MinAlpha = 85f / 255f;
    private const float MaxAlpha = 127f / 255f;
    private const float MinScale = 32f / 96f;
    private const float MaxScale = 1.25f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SmellerComponent, AfterAutoHandleStateEvent>(OnSmellerState);
        SubscribeLocalEvent<ScentMarkerComponent, ComponentStartup>(OnMarkerStartup);
        SubscribeLocalEvent<ScentMarkerComponent, AfterAutoHandleStateEvent>(OnMarkerState);
    }

    private void OnSmellerState(EntityUid uid, SmellerComponent component, ref AfterAutoHandleStateEvent args)
    {
        if (_player.LocalSession?.AttachedEntity != uid)
            return;

        RefreshAllMarkers(component.TrackedScentId);
    }

    private void OnMarkerStartup(Entity<ScentMarkerComponent> ent, ref ComponentStartup args)
    {
        PlayFadeAnimation(ent);

        if (_player.LocalSession?.AttachedEntity is not { } local ||
            !TryComp<SmellerComponent>(local, out var smeller))
        {
            return;
        }

        ApplyFilter(ent, smeller.TrackedScentId);
    }

    private void OnMarkerState(Entity<ScentMarkerComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        PlayFadeAnimation(ent);
    }

    private void RefreshAllMarkers(string? trackedScentId)
    {
        var query = EntityQueryEnumerator<ScentMarkerComponent>();
        while (query.MoveNext(out var uid, out var marker))
        {
            ApplyFilter((uid, marker), trackedScentId);
        }
    }

    private void ApplyFilter(Entity<ScentMarkerComponent> ent, string? trackedScentId)
    {
        if (!TryComp<SpriteComponent>(ent.Owner, out var sprite))
            return;

        var visible = trackedScentId == null || ent.Comp.ScentId == trackedScentId;
        _sprite.SetVisible((ent.Owner, sprite), visible);
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
        var scale = MathHelper.Lerp(MinScale, MaxScale, strength);
        _sprite.SetScale((ent.Owner, sprite), new Vector2(scale, scale));

        var startAlpha = MathHelper.Lerp(MinAlpha, MaxAlpha, strength);
        var startColor = GetScentColor(ent.Comp.ScentId).WithAlpha(startAlpha);
        var endColor = Color.Gray.WithAlpha(0f);
        var remaining = MathF.Max(0.1f, (float)(ent.Comp.ExpiresAt - _timing.CurTime).TotalSeconds);

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
