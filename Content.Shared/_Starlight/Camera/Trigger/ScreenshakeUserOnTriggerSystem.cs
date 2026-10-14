using Content.Shared.Trigger;

namespace Content.Shared._Starlight.Camera.Trigger;

public sealed partial class ScreenshakeUserOnTriggerSystem : XOnTriggerSystem<ScreenshakeUserOnTriggerComponent>
{
    [Dependency] private ScreenshakeSystem _shake = default!;

    protected override void OnTrigger(Entity<ScreenshakeUserOnTriggerComponent> ent, EntityUid target,
        ref TriggerEvent args)
    {
        if (args.User is null) return;

        _shake.Screenshake(args.User.Value, ent.Comp.Translation, ent.Comp.Rotation);
        args.Handled = true;
    }
}
