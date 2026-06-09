using System.Linq;
using Content.Shared._Starlight.Abstract.Extensions;
using Content.Shared.Chat;
using Content.Shared.Examine;
using Content.Shared.NameModifier.EntitySystems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.NameConfusion;

/// <summary>
/// Admeme system to make people get confused about their name.
/// </summary>
public sealed class NameConfusionSystem : EntitySystem
{
    [Dependency] private readonly NameModifierSystem _name = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _rand = default!;

    private const int NameModPriority = -900; // Basically it just needs to happen first.

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NameConfusionComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<NameConfusionComponent, EntitySpokeEvent>(OnSpeak);
        SubscribeLocalEvent<NameConfusionComponent, RefreshNameModifiersEvent>(OnRefreshNameMod);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityManager.EntityQueryEnumerator<NameConfusionComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.ConfuseOnInterval) continue;
            if (_timing.CurTime < comp.NextConfuseTime) continue;
            comp.NextConfuseTime = _timing.CurTime + comp.ConfuseInterval;
            ConfuseName(uid, comp);
        }
    }

    private void OnExamined(Entity<NameConfusionComponent> entity, ref ExaminedEvent args)
    {
        if (!entity.Comp.ConfuseOnExamine) return;
        ConfuseName(entity, entity.Comp);
        args.AddNameModifier("name-confusion-mod", NameModPriority,
            ("confusedName", entity.Comp.CurrentName ?? Name(entity)));
    }

    private void OnSpeak(Entity<NameConfusionComponent> entity, ref EntitySpokeEvent args)
    {
        if (!entity.Comp.ConfuseOnSpeak) return;
        ConfuseName(entity, entity.Comp);
    }

    private static void OnRefreshNameMod(Entity<NameConfusionComponent> entity, ref RefreshNameModifiersEvent args) =>
        args.AddModifier("name-confusion-mod", NameModPriority,
            ("confusedName", entity.Comp.CurrentName ?? args.BaseName));

    public void ConfuseName(EntityUid uid, NameConfusionComponent? comp = null, bool forced = false)
    {
        if (!Resolve(uid, ref comp)) return;
        if (comp.CurrentName is not null && _rand.ProbPredicted(_timing, comp.NameRestoreProbability)) // if you want to force restore, use RestoreName.
        {
            RestoreName(uid, comp);
            return;
        }

        if (comp.Names.Count == 0) return;
        if (!_rand.ProbPredicted(_timing, comp.NameConfusionProbability) && !forced) return;
        if (comp.CurrentName is null) comp.OriginalName = Name(uid);
        comp.CurrentName = _rand.PickPredicted(_timing, comp.Names.ToList());
        Dirty(uid, comp);
        _name.RefreshNameModifiers(uid);
    }

    public void RestoreName(EntityUid uid, NameConfusionComponent? comp = null)
    {
        if (!Resolve(uid, ref comp)) return;
        comp.CurrentName = null;
        comp.OriginalName = null;
        Dirty(uid, comp);
        _name.RefreshNameModifiers(uid);
    }

    public void AddConfusedName(EntityUid uid, string name, NameConfusionComponent? comp = null)
    {
        if (!Resolve(uid, ref comp)) return;
        comp.Names.Add(name);
        Dirty(uid, comp);
    }

    public void RemoveConfusedName(EntityUid uid, string name, NameConfusionComponent? comp = null)
    {
        if (!Resolve(uid, ref comp)) return;
        comp.Names.Remove(name);
        Dirty(uid, comp);
    }

    public void ClearConfusedNames(EntityUid uid, NameConfusionComponent? comp = null)
    {
        if (!Resolve(uid, ref comp)) return;
        comp.Names.Clear();
        Dirty(uid, comp);
    }

    public void SetConfusedOnSpeak(EntityUid uid, bool state, NameConfusionComponent? comp = null)
    {
        if (!Resolve(uid, ref comp)) return;
        comp.ConfuseOnSpeak = state;
        Dirty(uid, comp);
    }

    public void SetConfusedOnExamine(EntityUid uid, bool state, NameConfusionComponent? comp = null)
    {
        if (!Resolve(uid, ref comp)) return;
        comp.ConfuseOnExamine = state;
        Dirty(uid, comp);
    }

    public void SetConfusedOnInterval(EntityUid uid, bool state, NameConfusionComponent? comp = null)
    {
        if (!Resolve(uid, ref comp)) return;
        comp.ConfuseOnInterval = state;
        Dirty(uid, comp);
    }

    public void SetConfusedIntervalTime(EntityUid uid, TimeSpan time, NameConfusionComponent? comp = null)
    {
        if (!Resolve(uid, ref comp)) return;
        comp.ConfuseInterval = time;
        Dirty(uid, comp);
    }

    public void SetConfusionProbability(EntityUid uid, float prob, NameConfusionComponent? comp = null)
    {
        if (!Resolve(uid, ref comp)) return;
        comp.NameConfusionProbability = Math.Clamp(prob, 0, 1);
        Dirty(uid, comp);
    }

    public void SetRestoreProbability(EntityUid uid, float prob, NameConfusionComponent? comp = null)
    {
        if (!Resolve(uid, ref comp)) return;
        comp.NameRestoreProbability = Math.Clamp(prob, 0, 1);
        Dirty(uid, comp);
    }
}
