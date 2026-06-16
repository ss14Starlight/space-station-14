using Content.Shared._Starlight.Magic.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Network;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._Starlight.Magic.Systems;

/// <summary>
///     Plays sounds when a <see cref="SoundStatusEffectComponent" /> comes into effect and/or expires.
/// </summary>
public sealed partial class SoundStatusEffectSystem : EntitySystem
{
    [Dependency] private INetManager _netMan = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SoundStatusEffectComponent, StatusEffectAppliedEvent>(SoundStatusEffectApplied);
        SubscribeLocalEvent<SoundStatusEffectComponent, StatusEffectRemovedEvent>(SoundStatusEffectRemoved);
    }

    private void SoundStatusEffectRemoved(Entity<SoundStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        var effect = ent.Comp;
        if(effect.endSound != null)
            TryEmitSound(args.Target, effect.endSound, effect.endPositional);
    }

    private void SoundStatusEffectApplied(Entity<SoundStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        var effect = ent.Comp;
        if (effect.startSound != null)
            TryEmitSound(args.Target, effect.startSound, effect.startPositional);
    }

    private void TryEmitSound(EntityUid uid, SoundSpecifier sound, bool positional)
    {
        if (positional)
        {
            var coords = Transform(uid).Coordinates;
            if (_netMan.IsServer)
                // don't predict sounds that client couldn't have played already
                _audioSystem.PlayPvs(sound, coords);
        }
        else
        {
            if (_netMan.IsServer)
                // don't predict sounds that client couldn't have played already
                _audioSystem.PlayPvs(sound, uid);
        }
    }
}
