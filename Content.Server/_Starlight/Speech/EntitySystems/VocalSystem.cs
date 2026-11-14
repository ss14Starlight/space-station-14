using Content.Server._Starlight.Achievement;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Humanoid;
using Content.Shared.Speech.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Speech.EntitySystems;

public sealed partial class VocalSystem
{
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private AchievementSystem _achievement = default!;

    public void SetSounds(Entity<VocalComponent?> uid, Dictionary<Sex, ProtoId<EmoteSoundsPrototype>>? sounds)
    {
        if (!Resolve(uid, ref uid.Comp)) return;
        uid.Comp.Sounds = sounds;
        uid.Comp.EmoteSounds = null;
        LoadSounds(uid, uid.Comp);
        Dirty(uid, uid.Comp);
    }
}
