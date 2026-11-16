using Content.Client.Light.Visualizers;
using Content.Shared._Starlight.Light.Flicker;
using Content.Shared.Light;
using Content.Shared.Light.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Light;

/// <summary>
/// Plays the flicker the server picked for a light fixture, see <see cref="LightFlickerComponent"/>.
/// Flips the light from whatever its appearance says it should be, so it never fights the server over the light's state.
/// </summary>
public sealed partial class LightFlickerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPointLightSystem _pointLight = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<LightFlickerComponent, PoweredLightVisualsComponent, AppearanceComponent>();
        while (query.MoveNext(out var uid, out var flicker, out var visuals, out var appearance))
        {
            var profileId = visuals.IsBlinking ? null : flicker.Profile;
            if (profileId != flicker.Playing)
            {
                SetToggled((uid, flicker, visuals, appearance), false);
                flicker.Playing = profileId;
                flicker.TogglesLeft = 0;
                if (profileId != null && _proto.TryIndex(profileId, out var started))
                    flicker.NextToggle = now + (Delay(started) * _random.NextFloat());
                continue;
            }

            if (profileId == null || now < flicker.NextToggle || !_proto.TryIndex(profileId, out var profile))
                continue;

            if (flicker.TogglesLeft == 0)
            {
                flicker.TogglesLeft = _random.Next(profile.MinBlips, profile.MaxBlips + 1) * 2;
                if (profile.Sound != null && _random.Prob(profile.SoundChance))
                    _audio.PlayEntity(profile.Sound, Filter.Local(), uid, false);
            }

            SetToggled((uid, flicker, visuals, appearance), !flicker.Toggled);
            flicker.TogglesLeft--;
            flicker.NextToggle = flicker.TogglesLeft > 0
                ? now + TimeSpan.FromSeconds(_random.NextFloat(profile.MinBlipTime, profile.MaxBlipTime))
                : now + Delay(profile);
        }
    }

    private TimeSpan Delay(LightFlickerPrototype profile)
        => TimeSpan.FromSeconds(_random.NextFloat(profile.MinDelay, profile.MaxDelay));

    private void SetToggled(Entity<LightFlickerComponent, PoweredLightVisualsComponent, AppearanceComponent> ent, bool toggled)
    {
        var (uid, flicker, visuals, appearance) = ent;
        if (flicker.Toggled == toggled)
            return;

        flicker.Toggled = toggled;

        var lit = _appearance.TryGetData<PoweredLightState>(uid, PoweredLightVisuals.BulbState, out var state, appearance)
            && state == PoweredLightState.On;
        _pointLight.SetEnabled(uid, lit != toggled);

        if (!toggled)
        {
            _appearance.QueueUpdate(uid, appearance);
            return;
        }

        if (!lit || !TryComp<SpriteComponent>(uid, out var sprite))
            return;

        var states = visuals.SpriteStateMap;
        if (states.TryGetValue(PoweredLightState.Off, out var off))
            _sprite.LayerSetRsiState((uid, sprite), PoweredLightLayers.Base, off);

        if (_sprite.LayerExists((uid, sprite), PoweredLightLayers.Glow))
            _sprite.LayerSetVisible((uid, sprite), PoweredLightLayers.Glow, false);
    }
}
