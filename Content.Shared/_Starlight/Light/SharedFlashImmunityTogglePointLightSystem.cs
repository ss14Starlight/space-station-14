using Content.Shared._Starlight.Overlay.Events;
using Content.Shared._Starlight.Overlay.Systems;

namespace Content.Shared._Starlight.Light;

public sealed partial class SharedFlashImmunityTogglePointLightSystem : EntitySystem
{
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private FlashImmunitySystem _flash = default!;

    [SubscribeLocalEvent]
    private void OnComponentStartUp(Entity<FlashImmunityTogglePointLightComponent> ent, ref ComponentStartup args)
        =>  ToggleLight(ent, !_flash.HasFlashImmunityVisionBlockers(ent));

    [SubscribeLocalEvent]
    private void OnFlashImmunityChanged(Entity<FlashImmunityTogglePointLightComponent> ent, ref FlashImmunityCheckEvent args)
        => ToggleLight(ent, args.IsImmune);

    private void ToggleLight(Entity<FlashImmunityTogglePointLightComponent> ent, bool toggle)
    {
        // Doing it like this because any direct checks with SharedPointLightComponent seem to break.
        if(!_light.TryGetLight(ent, out var light))
            return;

        _light.SetEnabled(ent, ent.Comp.Invert ?  !toggle : toggle, light);
    }
}
