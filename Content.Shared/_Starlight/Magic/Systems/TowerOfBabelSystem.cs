using System.Linq;
using Content.Shared._Starlight.Language;
using Content.Shared._Starlight.Language.Components;
using Content.Shared._Starlight.Language.Events;
using Content.Shared._Starlight.Language.Systems;
using Content.Shared._Starlight.Magic.Components;
using Content.Shared.Destructible;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Starlight.Magic.Systems;

public sealed partial class TowerOfBabelSystem : EntitySystem
{
    [Dependency] private readonly SharedLanguageSystem _language = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<TowerOfBabelComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<TowerOfBabelComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<LanguageKnowledgeInitEvent>(OnLanguageKnowledgeInit);
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

        if (comp.SpokenLanguages.Count > comp.UnderstoodLanguages.Count)
        {
            _random.Shuffle(allLangs);
            comp.SpokenLanguages = [.. allLangs.Take(comp.SpokenLanguages.Count)];
            var spoken = comp.SpokenLanguages.ToList();
            _random.Shuffle(spoken);
            comp.UnderstoodLanguages = [.. spoken.Take(comp.UnderstoodLanguages.Count())];
        }
        else
        {
            _random.Shuffle(allLangs);
            comp.UnderstoodLanguages = [.. allLangs.Take(comp.UnderstoodLanguages.Count)];
            var understood = comp.UnderstoodLanguages.ToList();
            _random.Shuffle(understood);
            comp.SpokenLanguages = [.. understood.Take(comp.SpokenLanguages.Count())];
        }

        if (
            comp.SpokenLanguages.Contains(SharedLanguageSystem.UniversalPrototype) ||
            comp.UnderstoodLanguages.Contains(SharedLanguageSystem.UniversalPrototype)
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

    private void OnComponentShutdown(Entity<TowerOfBabelComponent> ent, ref ComponentShutdown ev)
    {
        TowerRemoved(ent);
    }

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
        if (!EntityManager.EntityQueryEnumerator<TowerOfBabelComponent>().MoveNext(out var _, out var _))
            return; //if there is not atleast 1 tower of babel in existence do not shuttle languages.
        var ent = ev.Entity;

        ShuffleLanguages(ent);
    }
}