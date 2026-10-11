using Content.Server.Audio;
using static Content.Shared.MedicalScanner.SharedMedicalScannerComponent;

// ReSharper disable once CheckNamespace
namespace Content.Server.Medical;

public sealed partial class MedicalScannerSystem
{
    [Dependency] private AmbientSoundSystem _ambientSound = default!;

    private void UpdateAmbience(EntityUid uid, MedicalScannerStatus status)
        => _ambientSound.SetAmbience(uid, status is not (MedicalScannerStatus.Open or MedicalScannerStatus.Off));
}
