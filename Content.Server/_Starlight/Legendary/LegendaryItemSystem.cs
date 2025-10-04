using Content.Shared._Starlight.Legendary;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Legendary;

public sealed class LegendaryItemSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LegendaryItemComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, LegendaryItemComponent component, ref MapInitEvent args)
    {
        if (component.RollProcessed)
            return;

        component.RollProcessed = true;

        if (!TryApplyLegendary(uid, component))
        {
            RemCompDeferred<LegendaryItemComponent>(uid);
            return;
        }
    }

    internal bool TryApplyLegendary(EntityUid uid, LegendaryItemComponent component)
    {
        var chance = Math.Clamp(component.Chance, 0f, 1f);
        if (chance <= 0f || !_random.Prob(chance))
            return false;

        component.LegendaryApplied = true;

        var description = GetDescription(component);
        if (description != null)
        {
            var meta = MetaData(uid);
            _meta.SetEntityDescription(uid, description, meta);
        }

        return true;
    }

    private string? GetDescription(LegendaryItemComponent component)
    {
        if (component.Story is { } storyId && TryBuildStory(storyId, component.Description, out var story))
            return story;

        if (component.Description != null)
            return Loc.GetString(component.Description.Value);

        return null;
    }

    private bool TryBuildStory(ProtoId<StoryPrototype> storyId, LocId? template, out string? result)
    {
        result = null;

        if (!_prototypeManager.TryIndex(storyId, out StoryPrototype? storyProto))
            return false;

        if (storyProto.Opens.Count == 0 || storyProto.Mids.Count == 0 || storyProto.Ends.Count == 0)
            return false;

        var open = Loc.GetString(_random.Pick(storyProto.Opens));
        var mid = Loc.GetString(_random.Pick(storyProto.Mids));
        var end = Loc.GetString(_random.Pick(storyProto.Ends));
        var combined = $"{open} {mid} {end}";

        if (template != null)
        {
            result = Loc.GetString(template.Value,
                ("open", open),
                ("mid", mid),
                ("end", end),
                ("story", combined));
            return true;
        }

        result = combined;
        return true;
    }
}
