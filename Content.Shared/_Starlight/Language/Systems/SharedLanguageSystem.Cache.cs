using System.Linq;
using Content.Shared._Starlight.Language.Components;
using Content.Shared._Starlight.Traits;
using Content.Shared._Starlight.Traits.Effects;
using Content.Shared.GameTicking;
using Content.Shared.Roles;

namespace Content.Shared._Starlight.Language.Systems;

public abstract partial class SharedLanguageSystem
{
    /// <summary>
    ///     Captures a <see cref="LanguageCacheComponent"/> for this entity and stores it there.
    /// </summary>
    /// <param name="ent">The entity the cache should be taken and applied to</param>
    public void CaptureCache(Entity<LanguageKnowledgeComponent> ent)
    {
        if (EnsureComp(ent, out LanguageCacheComponent cache))
            return; //The entity already has a cache which means languages were modified twice. eg: randomized twice. if we re-rolled it, it would restore the randomized languages

        cache.HasUniversal = HasComp<UniversalLanguageSpeakerComponent>(ent);
        cache.SpeakingCache = ent.Comp.Speaks.ToHashSet();
        cache.UnderstandingCache = ent.Comp.Understands.ToHashSet();
        Dirty(ent.Owner, cache);
    }

    /// <summary>
        /// Restores an entity's languages from their <see cref="LanguageCacheComponent"/>
        /// recommended to call UpdateLanguages after calling this.
    /// </summary>
    /// <param name="ent">The entity the cache should be restored for</param>
    public void RestoreCache(Entity<LanguageCacheComponent> ent)
    {
        if (!TryComp<LanguageKnowledgeComponent>(ent, out var knowledge))
            return; //The ent had no knowledge to restore the cache into

        var cache = ent.Comp;
        if (cache.HasUniversal)
            EnsureComp<UniversalLanguageSpeakerComponent>(ent);
        else
            RemComp<UniversalLanguageSpeakerComponent>(ent);

        knowledge.Speaks = cache.SpeakingCache?.ToList() ?? knowledge.Speaks;
        knowledge.Understands = cache.UnderstandingCache?.ToList() ?? knowledge.Understands;
        Dirty(ent, knowledge);

        RemComp<LanguageCacheComponent>(ent);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (args.JobId == null ||
            !_prototype.TryIndex<JobPrototype>(args.JobId, out var jobProto) ||
            !jobProto.ApplyTraits)
            return;
        if (!TryComp<LanguageCacheComponent>(args.Mob, out var cache))
            return; //We dont have a cache on this entity somehow???

        var validTraits = _trait.ValidateTraits(args.Mob, args.Profile.TraitPreferences, args.Player, args.Profile);

        var effects = validTraits
            .Select(t => _prototype.Index<TraitPrototype>(t))
            .SelectMany(t => t.Effects)
            .ToList();

        var languageEffects = effects.OfType<LanguageEffect>();

        foreach (var effect in languageEffects)
        {
            if (effect.RemoveLanguagesSpoken is not null)
            {
                cache.SpeakingCache ??= [];
                cache.SpeakingCache.ExceptWith(effect.RemoveLanguagesSpoken);
            }
            if (effect.RemoveLanguagesUnderstood is not null)
            {
                cache.UnderstandingCache ??= [];
                cache.UnderstandingCache.ExceptWith(effect.RemoveLanguagesUnderstood);
            }
            if (effect.LanguagesSpoken is not null)
            {
                cache.SpeakingCache ??= [];
                cache.SpeakingCache.UnionWith(effect.LanguagesSpoken);
            }
            if (effect.LanguagesUnderstood is not null)
            {
                cache.UnderstandingCache ??= [];
                cache.UnderstandingCache.UnionWith(effect.LanguagesUnderstood);
            }
        }
        Dirty(new Entity<LanguageCacheComponent>(args.Mob, cache), MetaData(args.Mob));
    }
}
