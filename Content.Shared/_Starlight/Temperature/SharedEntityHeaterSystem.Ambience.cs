using Content.Shared.Audio;
using Content.Shared.Temperature.Components;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Temperature.Systems;

public abstract partial class SharedEntityHeaterSystem
{
    [Dependency] private SharedAmbientSoundSystem _ambientSound = default!;

    private void UpdateAmbience(Entity<EntityHeaterComponent> ent, bool powered)
        => _ambientSound.SetAmbience(ent, powered && ent.Comp.Setting != EntityHeaterSetting.Off);
}
