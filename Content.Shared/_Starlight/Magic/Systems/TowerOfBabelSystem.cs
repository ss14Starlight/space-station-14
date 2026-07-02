using System.Linq;
using Content.Shared._Starlight.Language;
using Content.Shared._Starlight.Language.Components;
using Content.Shared._Starlight.Language.Events;
using Content.Shared._Starlight.Language.Systems;
using Content.Shared._Starlight.Magic.Components;
using Content.Shared._Starlight.Traits;
using Content.Shared._Starlight.Traits.Effects;
using Content.Shared.GameTicking;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Starlight.Magic.Systems;

public sealed partial class TowerOfBabelSystem : EntitySystem
{
    [Dependency] private SharedLanguageSystem _language = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private TraitSystem _trait = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<TowerOfBabelComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<TowerOfBabelComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<LanguageKnowledgeInitEvent>(OnLanguageKnowledgeInit);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void ShuffleLanguages(Entity<LanguageKnowledgeComponent> languageKnower, List<ProtoId<LanguagePrototype>>? allLangs = null)
    {
        if (TerminatingOrDeleted(languageKnower))
            return; // Entity is terminating caching can cause it to testfail/crash
        if (HasComp<UniversalLanguageSpeakerComponent>(languageKnower))
            return; // One who knows the knowledge of all things cannot know less.

        allLangs ??= _language.Languages.ToList(); //we can skip it if we know we wont be re-using it for perf reasons.
        _language.CaptureCache(languageKnower);

        var comp = languageKnower.Comp;

        if (comp.Speaks.Count > comp.Understands.Count)
        {
            _random.Shuffle(allLangs);
            comp.Speaks = [.. allLangs.Take(comp.Speaks.Count)];
            var spoken = comp.Speaks.ToList();
            _random.Shuffle(spoken);
            comp.Understands = [.. spoken.Take(comp.Understands.Count())];
        }
        else
        {
            _random.Shuffle(allLangs);
            comp.Understands = [.. allLangs.Take(comp.Understands.Count)];
            var understood = comp.Understands.ToList();
            _random.Shuffle(understood);
            comp.Speaks = [.. understood.Take(comp.Speaks.Count())];
        }

        if (
            comp.Speaks.Contains(SharedLanguageSystem.UniversalPrototype) ||
            comp.Understands.Contains(SharedLanguageSystem.UniversalPrototype)
        )
            EnsureComp<UniversalLanguageSpeakerComponent>(languageKnower);

        if (TryComp<LanguageSpeakerComponent>(languageKnower, out var speaker))
            _language.UpdateEntityLanguages((languageKnower, speaker));
    }

    private void OnMapInit(Entity<TowerOfBabelComponent> ent, ref MapInitEvent ev)
    {
        if (Transform(ent).MapID == MapId.Nullspace)
            return; //the entitty is in the land between time. dont init it.

        var langs = _language.Languages.ToList();
        foreach (var languageKnower in EntityManager.AllEntities<LanguageKnowledgeComponent>())
        {
            ShuffleLanguages(languageKnower, langs);
            _popup.PopupEntity(Loc.GetString("tower-of-babel-shifted"), languageKnower, languageKnower);
        }
    }

    private void OnComponentShutdown(Entity<TowerOfBabelComponent> ent, ref ComponentShutdown ev) => TowerRemoved(ent);

    private void TowerRemoved(Entity<TowerOfBabelComponent> ent)
    {
        var towerEnumerator = EntityManager.EntityQueryEnumerator<TowerOfBabelComponent>();
        towerEnumerator.MoveNext(out var _, out var _); //the tower being destroyed
        if (towerEnumerator.MoveNext(out var _, out var _))
            return; //there is a 2nd tower that is NOT detroyed. so dont reset languages yet.

        foreach (var languageKnower in EntityManager.AllEntities<LanguageKnowledgeComponent>())
        {
            if (TryComp<LanguageCacheComponent>(languageKnower, out var cache))
                _language.RestoreCache((languageKnower, cache));
            _popup.PopupEntity(Loc.GetString("tower-of-babel-returned"), languageKnower, languageKnower);
            if (TryComp<LanguageSpeakerComponent>(languageKnower, out var speaker))
                _language.UpdateEntityLanguages((languageKnower, speaker));
        }
    }

    private void OnLanguageKnowledgeInit(ref LanguageKnowledgeInitEvent ev)
    {
        if (!EntityQueryEnumerator<TowerOfBabelComponent>().MoveNext(out var _, out var _))
            return; //if there is not atleast 1 tower of babel in existence do not shuttle languages.
        var ent = ev.Entity;

        ShuffleLanguages(ent);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (!EntityQueryEnumerator<TowerOfBabelComponent>().MoveNext(out var _, out var _))
            return; //if there is not atleast 1 tower of babel in existence do not shuttle languages.
                // Check if player's job allows traits
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
    }
}
