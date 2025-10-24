using Content.Server.Chat.Systems;
using Content.Server.Emoting.Components;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Hands.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Emoting.Systems;

public sealed class BodyEmotesSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyEmotesComponent, EmoteEvent>(OnEmote);
    }

    private void OnEmote(EntityUid uid, BodyEmotesComponent component, ref EmoteEvent args)
    {
        if (args.Handled)
            return;

        var cat = args.Emote.Category;
        if (cat.HasFlag(EmoteCategory.General)) // Starlight: Make General sounds *actually* not require Vocal/Hands
        {
            args.Handled = TryEmoteBody(uid, args.Emote, component);
        }
        else if (cat.HasFlag(EmoteCategory.Hands))
        {
            args.Handled = TryEmoteHands(uid, args.Emote, component);
        }
    }

    private bool TryEmoteHands(EntityUid uid, EmotePrototype emote, BodyEmotesComponent component)
    {
        // check that user actually has hands to do emote sound
        if (!TryComp(uid, out HandsComponent? hands) || hands.Count <= 0)
            return false;

        if (!_proto.Resolve(component.SoundsId, out var sounds))
            return false;

        return _chat.TryPlayEmoteSound(uid, sounds, emote);
    }
    
    /// <summary>
    /// Starlight.
    ///
    /// Due to how emoting is set up (bitmasks/flags) it just happens that the EmoteCategory "General" classifies as
    /// both "Vocal" and "Hands". By pure coincidence, this works out for the deathgasp sound (the only sound currently
    /// in the General category), which will play if you have either of those components.
    ///
    /// But, if you don't have either of those, General emotes will never play. This method fixes that. 
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="emote"></param>
    /// <param name="component"></param>
    /// <returns></returns>
    private bool TryEmoteBody(EntityUid uid, EmotePrototype emote, BodyEmotesComponent component)
    {
        if (!_proto.Resolve(component.SoundsId, out var sounds))
            return false;

        return _chat.TryPlayEmoteSound(uid, sounds, emote);
    }
}
