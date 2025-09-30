using Content.Shared.Weapons.Ranged.Systems;

using Content.Shared.Speech.Components;

namespace Content.Shared.Starlight.MumbleShooting;

public abstract partial class SharedPreventMumbleShootingSystem : EntitySystem
{

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MumbleAccentComponent, AttemptShootEvent>(OnAttemptShoot);
    }
    private void OnAttemptShoot(EntityUid uid, MumbleAccentComponent comp, ref AttemptShootEvent args)
    {
        if (args.User == uid)
        {
            args.Cancelled = true;
            args.Message = Loc.GetString("gun-mumble");
        }
    }
}