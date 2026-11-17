using Content.Shared._Daiquiri.Emoting;
using Content.Shared.Chat;

namespace Content.Server._Daiquiri.Emoting;

public sealed partial class AnimatedEmotesSystem : SharedAnimatedEmotesSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnimatedEmotesComponent, EmoteEvent>(OnEmote);
    }

    private void OnEmote(Entity<AnimatedEmotesComponent> ent, ref EmoteEvent args)
    {
        if (args.Emote.Event == null)
            return;

        ent.Comp.Emote = args.Emote.ID;
        ent.Comp.Sequence++;
        Dirty(ent);
    }
}
