using Content.Server.Audio;
using Content.Shared.Cloning;
using Content.Shared.Power;

// ReSharper disable once CheckNamespace
namespace Content.Server.Cloning;

public sealed partial class CloningPodSystem
{
    [Dependency] private AmbientSoundSystem _ambientSound = default!;

    [SubscribeLocalEvent]
    private void OnPodPowerChanged(Entity<CloningPodComponent> ent, ref PowerChangedEvent args)
        => UpdateAmbience(ent, ent.Comp, args.Powered);

    private void UpdateAmbience(EntityUid uid, CloningPodComponent pod)
        => UpdateAmbience(uid, pod, _powerReceiverSystem.IsPowered(uid));

    private void UpdateAmbience(EntityUid uid, CloningPodComponent pod, bool powered)
        => _ambientSound.SetAmbience(uid, pod.Status == CloningPodStatus.Cloning && powered);
}
