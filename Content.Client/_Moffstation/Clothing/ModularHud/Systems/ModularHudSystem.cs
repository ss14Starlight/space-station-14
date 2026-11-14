using Content.Client.Guidebook.Components;
using Content.Shared._Moffstation.Clothing.ModularHud.Systems;
using Content.Shared.Verbs;

namespace Content.Client._Moffstation.Clothing.ModularHud.Systems;

/// <see cref="SharedModularHudSystem"/>
public sealed partial class ModularHudSystem : SharedModularHudSystem
{
    public override void Initialize()
    {
        base.Initialize();

        // Relay examine verb events to modules with guidebook component regardless of wearing status.
        SubscribeRelaysForEffectEvents<GetVerbsEvent<ExamineVerb>>(
            requiresActiveSlots: false,
            entity => HasComp<GuideHelpComponent>(entity)
        );
    }
}
