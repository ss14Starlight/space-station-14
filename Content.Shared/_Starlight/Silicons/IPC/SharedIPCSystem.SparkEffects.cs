// IPC System - Spark Effects (Shared)
// Created by Killer Tamashi and Princess Gurchi for the FH project.
// https://github.com/Far-Horizons-SS14/Far-Horizons-SS14/pull/135

using System.Numerics;
using Content.Shared._Starlight.Combat.Effects.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared._Starlight.Silicons.IPC;

public abstract partial class SharedIPCSystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    
    private void SetupSparkEffects()
    {
        SubscribeLocalEvent<CyborgSparkEffectComponent, DamageChangedEvent>(OnCyborgDamageChanged);
    }

    /// <summary>
    /// Handles spawning spark effects when IPCs/cyborgs take damage.
    /// </summary>
    private void OnCyborgDamageChanged(EntityUid uid, CyborgSparkEffectComponent component, ref DamageChangedEvent args)
    {
        // Only process on server
        if (!_net.IsServer)
            return;

        // Only spawn sparks if damage increased (not healing)
        if (args.DamageDelta == null || args.DamageDelta.GetTotal() <= 0)
            return;

        SpawnCyborgSparkEffect(uid, component);
    }

    /// <summary>
    /// Spawns a spark effect at the cyborg/IPC's location with random offset.
    /// </summary>
    private void SpawnCyborgSparkEffect(EntityUid cyborgUid, CyborgSparkEffectComponent component)
    {
        var cyborgTransform = Transform(cyborgUid);
        
        // Calculate random offset within the tile
        var offsetX = _random.NextFloat(-component.MaxOffset, component.MaxOffset);
        var offsetY = _random.NextFloat(-component.MaxOffset, component.MaxOffset);
        var offset = new Vector2(offsetX, offsetY);
        
        // Spawn the effect at the cyborg's position with offset
        var effectCoords = cyborgTransform.Coordinates.Offset(offset);
        
        // Spawn spark effect entity
        Spawn(component.SparkEffectPrototype, effectCoords);
        
        // Play spark sound from the sound collection
        _audio.PlayPvs(new SoundCollectionSpecifier(component.RicochetSoundCollection), cyborgUid);
    }
}
