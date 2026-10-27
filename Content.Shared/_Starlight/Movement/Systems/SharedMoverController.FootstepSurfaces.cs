using Content.Shared._Starlight.Movement;
using Content.Shared._Starlight.Movement.Components;
using Content.Shared.Maps;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Movement.Systems;

public abstract partial class SharedMoverController
{
    [Dependency] private IPrototypeManager _footstepProto = default!;

    private const string DefaultBarestep = "BarestepHard";

    private SoundSpecifier? GetTileStepSound(ContentTileDefinition tileDef, bool haveShoes)
    {
        if (haveShoes)
            return tileDef.FootstepSounds;

        if (tileDef.BarestepSounds is SoundCollectionSpecifier { Collection: { } bare } &&
            bare.Id == DefaultBarestep &&
            tileDef.FootstepSounds is SoundCollectionSpecifier { Collection: { } surface } &&
            _footstepProto.TryIndex<BarestepSoundPrototype>(surface.Id, out var barestep))
            return barestep.Sound;

        return tileDef.BarestepSounds;
    }

    protected AudioParams ApplyStepTimbre(EntityUid uid, AudioParams audioParams)
    {
        if (!_inventory.TryGetSlotEntity(uid, "shoes", out var shoes) ||
            !TryComp(shoes, out FootstepTimbreComponent? timbre))
            return audioParams;

        if (_inventory.TryGetSlotEntity(uid, "outerClothing", out var outer) &&
            FootstepModifierQuery.TryComp(outer, out var outerModifier) &&
            outerModifier.FootstepSoundCollection != null)
            return audioParams;

        return audioParams
            .WithPitchScale(audioParams.Pitch * timbre.Pitch)
            .WithVolume(audioParams.Volume + timbre.Volume);
    }
}
