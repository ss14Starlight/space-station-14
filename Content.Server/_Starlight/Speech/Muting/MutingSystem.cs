using Content.Shared.Abilities.Mime;
using Content.Shared.Speech.Muting;
using Content.Shared.StatusEffectNew;

// ReSharper disable once CheckNamespace
namespace Content.Server.Speech.Muting;

// Remove when WizDen finally migrates mute status effect to StatusEffectNew
public sealed partial class MutingSystem
{
    [SubscribeLocalEvent]
    private void OnStatusEffectApplied(Entity<MutedComponent> ent, ref StatusEffectAppliedEvent args)
        => EnsureComp<MutedComponent>(args.Target);

    [SubscribeLocalEvent]
    private void OnStatusEffectRemoved(Entity<MutedComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (TryComp<MimePowersComponent>(args.Target, out var mime) && !mime.VowBroken)
            return;

        RemComp<MutedComponent>(args.Target);
    }
}
