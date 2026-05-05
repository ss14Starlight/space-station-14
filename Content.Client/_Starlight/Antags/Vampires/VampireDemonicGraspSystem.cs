using System.Numerics;
using Content.Shared._Starlight.Antags.Vampires;
using Content.Shared._Starlight.Antags.Vampires.Components;
using Content.Shared._Starlight.Antags.Vampires.Components.Classes;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Antags.Vampires;

public sealed class VampireDemonicGraspSystem : EntitySystem
{
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, ActiveGraspVisual> _active = new();
    private readonly List<EntityUid> _finished = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VampireComponent, VampireDemonicGraspActionEvent>(OnPredictedDemonicGrasp);
        SubscribeNetworkEvent<VampireDemonicGraspVisualEvent>(OnDemonicGraspVisual);
    }

    private void OnPredictedDemonicGrasp(Entity<VampireComponent> ent, ref VampireDemonicGraspActionEvent args)
    {
        if (args.Handled || !HasComp<GargantuaComponent>(ent))
            return;

        if (TryComp<VampireActionComponent>(args.Action.Owner, out var action)
            && (ent.Comp.TotalBlood < action.BloodToUnlock
                || ent.Comp.DrunkBlood < action.BloodCost
                || action.RequiresFullPower && !ent.Comp.FullPower))
        {
            return;
        }

        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid)
            return;

        if (_transform.GetGrid(args.Target) != gridUid)
            return;

        if (!TryGetVisualEnd(xform.Coordinates, args.Target, args.Range, out var end))
            return;

        var interval = args.ProjectileSpeed > 0f
            ? TimeSpan.FromSeconds(1f / args.ProjectileSpeed)
            : args.TileInterval;

        TryStartVisual(ent.Owner, xform.Coordinates, end, interval, args.EffectPrototype);
    }

    private void OnDemonicGraspVisual(VampireDemonicGraspVisualEvent ev)
    {
        var source = GetEntity(ev.Source);
        if (!Exists(source))
            return;

        var interval = ev.Speed > 0f
            ? TimeSpan.FromSeconds(1f / ev.Speed)
            : TimeSpan.FromMilliseconds(50);

        TryStartVisual(source, GetCoordinates(ev.Start), GetCoordinates(ev.Target), interval, ev.Prototype);
    }

    private bool TryGetVisualEnd(EntityCoordinates start, EntityCoordinates target, float range, out EntityCoordinates end)
    {
        end = default;

        if (Deleted(start.EntityId) || Deleted(target.EntityId) || start.EntityId != target.EntityId)
            return false;

        var delta = target.Position - start.Position;
        var distance = delta.Length();
        if (distance < 0.1f)
            return false;

        var direction = delta / distance;
        var maxTiles = Math.Max(1, (int) MathF.Ceiling(MathF.Min(range, distance)));
        end = start.Offset(direction * maxTiles);
        return true;
    }

    private void TryStartVisual(EntityUid source, EntityCoordinates start, EntityCoordinates end, TimeSpan interval, EntProtoId prototype)
    {
        if (Deleted(start.EntityId) || Deleted(end.EntityId) || start.EntityId != end.EntityId)
            return;

        var delta = end.Position - start.Position;
        var distance = delta.Length();
        if (distance < 0.1f)
            return;

        var direction = delta / distance;
        var maxTiles = Math.Max(1, (int) MathF.Ceiling(distance));

        if (_active.TryGetValue(source, out var existing))
        {
            if (existing.End.EntityId == end.EntityId
                && (existing.End.Position - end.Position).LengthSquared() < 0.25f)
            {
                return;
            }

            _active.Remove(source);
        }

        _active[source] = new ActiveGraspVisual(start, end, direction, prototype, maxTiles, interval, _timing.CurTime);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        _finished.Clear();
        foreach (var entry in _active)
        {
            var source = entry.Key;
            var active = entry.Value;

            if (Deleted(active.Start.EntityId))
            {
                _finished.Add(source);
                continue;
            }

            while (now >= active.NextStepTime)
            {
                active.CurrentTile++;
                if (active.CurrentTile > active.MaxTiles)
                {
                    _finished.Add(source);
                    break;
                }

                Spawn(active.Prototype, active.Start.Offset(active.Direction * active.CurrentTile));
                active.NextStepTime += active.StepInterval;
            }
        }

        foreach (var source in _finished)
        {
            _active.Remove(source);
        }
    }

    public override void Shutdown()
    {
        _active.Clear();
        base.Shutdown();
    }

    private sealed class ActiveGraspVisual(
        EntityCoordinates start,
        EntityCoordinates end,
        Vector2 direction,
        EntProtoId prototype,
        int maxTiles,
        TimeSpan stepInterval,
        TimeSpan now)
    {
        public EntityCoordinates Start = start;
        public EntityCoordinates End = end;
        public Vector2 Direction = direction;
        public EntProtoId Prototype = prototype;
        public int MaxTiles = maxTiles;
        public int CurrentTile;
        public TimeSpan StepInterval = stepInterval;
        public TimeSpan NextStepTime = now;
    }
}
