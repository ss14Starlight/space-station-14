using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._Starlight.Xenobiology.MiscItems;

public sealed partial class SlimeScannerSoundSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audioSystem = default!;

    private static readonly SoundPathSpecifier _scannerSound = new("/Audio/Items/Medical/healthscanner.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<SlimeScannerSoundMessage>(OnSlimeScannerSound);
    }

    private void OnSlimeScannerSound(SlimeScannerSoundMessage args)
    {
        _audioSystem.PlayPredicted(_scannerSound, GetEntity(args.Owner), GetEntity(args.User));
    }
}
