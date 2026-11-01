using Content.Shared._Starlight.StationRadio.Components;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.StationRadio.Systems;

public sealed partial class DiscCoverSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private IGameTiming _timing = default!;

    [SubscribeLocalEvent]
    private void OnInserted(EntityUid uid, DiscCoverComponent comp, EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == comp.Slot)
            UpdateCover((uid, comp));
    }

    [SubscribeLocalEvent]
    private void OnRemoved(EntityUid uid, DiscCoverComponent comp, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == comp.Slot)
            UpdateCover((uid, comp));
    }

    /// <summary>
    /// The disc in a sleeve or case, if any.
    /// </summary>
    public EntityUid? GetDisc(Entity<DiscCoverComponent> ent)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.Slot, out var container) || container.ContainedEntities.Count == 0)
            return null;

        return container.ContainedEntities[0];
    }

    public void UpdateCover(Entity<DiscCoverComponent> ent)
    {
        if (_timing.ApplyingState)
            return;

        var disc = GetDisc(ent);
        _appearance.SetData(ent, DiscCoverVisuals.Disc, disc != null);
        if (disc == null)
            return;

        var (pattern, main, accent) = GetCover(disc.Value, ent.Comp.Patterns);
        _appearance.SetData(ent, DiscCoverVisuals.Pattern, pattern);
        _appearance.SetData(ent, DiscCoverVisuals.Main, main);
        _appearance.SetData(ent, DiscCoverVisuals.Accent, accent);
    }

    /// <summary>
    /// Cover pattern and colours of a disc
    /// </summary>
    public (int Pattern, Color Main, Color Accent) GetCover(EntityUid disc, int patterns)
    {
        var vinyl = CompOrNull<VinylComponent>(disc);
        var id = MetaData(disc).EntityPrototype?.ID ?? Name(disc);
        var hash = Fnv(id);

        var pattern = vinyl?.CoverPattern ?? (int) (hash % (uint) Math.Max(1, patterns));

        var main = vinyl?.CoverColor ?? MainFromLabel(vinyl?.LabelColor ?? Color.White, hash);
        var accent = vinyl?.CoverAccent ?? AccentFor(main, hash);
        return (pattern, main, accent);
    }

    private static Color MainFromLabel(Color label, uint hash)
    {
        var hsl = Color.ToHsl(label);

        if (hsl.Y < 0.15f)
        {
            hsl.X = (hash >> 8) % 360 / 360f;
            hsl.Y = 0.45f;
        }

        hsl.Z = Math.Clamp(hsl.Z, 0.35f, 0.62f);
        return Color.FromHsl(hsl);
    }

    private static Color AccentFor(Color main, uint hash)
    {
        var hsl = Color.ToHsl(main);
        if ((hash & 1) == 0)
        {
            hsl.Y *= 0.35f;
            hsl.Z = 0.88f;
        }
        else
        {
            hsl.Z = 0.14f;
        }

        return Color.FromHsl(hsl);
    }

    private static uint Fnv(string text)
    {
        var hash = 2166136261u;
        foreach (var c in text)
        {
            hash ^= c;
            hash *= 16777619u;
        }

        return hash;
    }
}
