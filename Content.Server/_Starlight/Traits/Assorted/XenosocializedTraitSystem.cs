using System.Linq;
using Content.Server.Hands.Systems;
using Content.Server._Starlight.Language;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory;
using Content.Shared._Starlight.Language;
using Content.Shared._Starlight.Language.Systems;
using Content.Shared._Starlight.Language.Components;
using Content.Shared.Storage;
using Robust.Shared.Prototypes;
using NetCord;

namespace Content.Server._Starlight.Traits.Assorted;

public sealed partial class XenosocializedTraitSystem : EntitySystem // Talita heartlocket gif
{
    [Dependency] private EntityManager _entMan = default!;
    [Dependency] private LanguageSystem _languages = default!;

    public override void Initialize()
        => SubscribeLocalEvent<XenosocializedTraitComponent, ComponentInit>(OnSpawn); // TraitSystem adds it after PlayerSpawnCompleteEvent so it's fine.

    private void OnSpawn(Entity<XenosocializedTraitComponent> entity, ref ComponentInit args)
    {

        if (!TryComp<LanguageKnowledgeComponent>(entity, out var knowledge))
        {
            Log.Warning($"Entity {entity.Owner} does not have a LanguageKnowledge but has a XenosocializedTrait!");
            return;
        }
        if (!TryComp(entity, out MetaDataComponent? metadata))
        {
            Log.Warning($"Entity {entity.Owner} does not have a MetaDataComponent?!");
            return;
        }
        if (metadata.EntityPrototype == null)
        {
            Log.Warning($"Entity {entity.Owner} does not have an EntityPrototype?!");
            return;
        }
        if (!metadata.EntityPrototype.Components.TryGetComponent("LanguageKnowledge", out var component))
        {
            Log.Warning($"Entity {entity.Owner}'s prototype does not have a LanguageKnowledgeComponent?!");
            return;
        }
        var prototypeLanguages = ((LanguageKnowledgeComponent)component).Speaks;

        var nativeLanguage = prototypeLanguages.Find(it => it != SharedLanguageSystem.FallbackLanguagePrototype && it != entity.Comp.NeocyteLanguage);
        if (nativeLanguage == default) prototypeLanguages.Find(it => it != SharedLanguageSystem.FallbackLanguagePrototype); // Try again (IPC, I don't see any other case where this happens and nativeLanguage changes.)
        if (nativeLanguage == default)
        {
            Log.Warning($"Entity {entity.Owner} does not have an native language to choose from (must have at least one non-GC for XenosocializedTrait!");
            return;
        }

        _languages.RemoveLanguage(entity.Owner, nativeLanguage, true, true);
    }
}
