using Content.Shared.NPC.Components;
using Content.Shared.Silicons.Bots;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Silicons.Bots;

public sealed partial class MedibotSystem
{
    private partial bool OnTryInject(Entity<MedibotComponent?> medibot, EntityUid target)
        => HasComp<NPCRecentlyInjectedComponent>(target);

    partial void OnInjected(Entity<MedibotComponent?> medibot, EntityUid target)
        => EnsureComp<NPCRecentlyInjectedComponent>(target);
}
