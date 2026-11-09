using Content.Shared._Starlight.TransitTube;
using Content.Shared.Destructible;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Stacks;

namespace Content.Server._Starlight.TransitTube;

public sealed partial class TransitTubeSystem
{
    [SubscribeLocalEvent]
    private void OnGlassInteractUsing(Entity<TransitTubeGlassComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<StackComponent>(args.Used, out var stack))
            return;

        var stackType = stack.StackTypeId;
        TransitTubeMaterialPrototype? material = null;
        foreach (var proto in _proto.EnumeratePrototypes<TransitTubeMaterialPrototype>())
        {
            if (proto.Stack == stackType)
            {
                material = proto;
                break;
            }
        }

        if (material == null)
            return;

        args.Handled = true;

        if (material.ID == ent.Comp.Material)
        {
            _popup.PopupEntity(Loc.GetString("transit-tube-glass-same"), ent, args.User);
            return;
        }

        if (!_stack.TryUse((args.Used, stack), material.Cost))
        {
            _popup.PopupEntity(Loc.GetString("transit-tube-glass-not-enough", ("count", material.Cost)), ent, args.User);
            return;
        }

        ent.Comp.Material = material.ID;
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("transit-tube-glass-reglazed", ("material", Loc.GetString(material.Name))), ent, args.User);
    }

    [SubscribeLocalEvent]
    private void OnGlassDestroyed(Entity<TransitTubeGlassComponent> ent, ref DestructionEventArgs args)
    {
        if (_proto.Resolve(ent.Comp.Material, out var material) && material.Shard is { } shard)
            SpawnAtPosition(shard, Transform(ent).Coordinates);
    }

    [SubscribeLocalEvent]
    private void OnGlassExamined(Entity<TransitTubeGlassComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !_proto.Resolve(ent.Comp.Material, out var material))
            return;

        args.PushMarkup(Loc.GetString("transit-tube-glass-examine", ("material", Loc.GetString(material.Name))));
    }

    private float GetHeatInsulation(TransformComponent podXform)
    {
        if (!TryGetTube(podXform, out var tube)
            || !TryComp<TransitTubeGlassComponent>(tube, out var glass)
            || !_proto.Resolve(glass.Material, out var material))
            return 0f;

        return Math.Clamp(material.HeatInsulation, 0f, 1f);
    }
}
