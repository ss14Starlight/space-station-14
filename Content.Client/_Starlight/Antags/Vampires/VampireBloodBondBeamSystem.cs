using System.Numerics;
using Content.Shared._Starlight.Antags.Vampires.Components;
using Content.Shared._Starlight.Antags.Vampires.Components.Classes;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.Antags.Vampires;

/// <summary>
/// Client-side system for smooth vampire blood bond beam visualization
/// </summary>
public sealed class VampireBloodBondBeamSystem : EntitySystem
{
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private EntityQuery<VampireBeamVisualComponent> _beamVisualQuery;

    /// <summary>
    /// Tracks client-side beam visual entities
    /// Key = (source, target) pair, Value = visual beam entity
    /// </summary>
    private readonly Dictionary<(EntityUid, EntityUid), EntityUid> _activeBeamVisuals = [];

    public override void Initialize()
    {
        base.Initialize();
        _beamVisualQuery = GetEntityQuery<VampireBeamVisualComponent>();
        SubscribeNetworkEvent<VampireBloodBondBeamEvent>(OnBloodBondBeamEvent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var toRemove = new List<(EntityUid, EntityUid)>();

        foreach (var ((source, target), beamEntity) in _activeBeamVisuals)
        {
            if (!Exists(source) || !Exists(target) || !Exists(beamEntity))
            {
                toRemove.Add((source, target));
                if (Exists(beamEntity))
                    QueueDel(beamEntity);
                continue;
            }

            UpdateBeamVisual(beamEntity, source, target);
        }

        foreach (var key in toRemove)
        {
            _activeBeamVisuals.Remove(key);
        }

        UpdatePredictedBloodBondBeams();
    }

    private void UpdatePredictedBloodBondBeams()
    {
        var dantalions = EntityQueryEnumerator<DantalionComponent>();
        while (dantalions.MoveNext(out var source, out var dantalion))
        {
            if (!dantalion.BloodBondActive)
            {
                RemoveSourceBeams(source);
                continue;
            }

            var linkedThralls = dantalion.BloodBondLinkedThralls;
            RemoveUnlinkedSourceBeams(source, linkedThralls);

            foreach (var target in linkedThralls)
            {
                if (!Exists(target))
                    continue;

                CreateBeamVisual(source, target, dantalion.BloodBondBeamPrototype);
            }
        }
    }

    private void RemoveSourceBeams(EntityUid source)
    {
        var toRemove = new List<(EntityUid, EntityUid)>();
        foreach (var ((beamSource, target), beamEntity) in _activeBeamVisuals)
        {
            if (beamSource != source)
                continue;

            if (Exists(beamEntity))
                QueueDel(beamEntity);

            toRemove.Add((beamSource, target));
        }

        foreach (var key in toRemove)
            _activeBeamVisuals.Remove(key);
    }

    private void RemoveUnlinkedSourceBeams(EntityUid source, List<EntityUid> linkedThralls)
    {
        var toRemove = new List<(EntityUid, EntityUid)>();
        foreach (var ((beamSource, target), beamEntity) in _activeBeamVisuals)
        {
            if (beamSource != source || linkedThralls.Contains(target))
                continue;

            if (Exists(beamEntity))
                QueueDel(beamEntity);

            toRemove.Add((beamSource, target));
        }

        foreach (var key in toRemove)
            _activeBeamVisuals.Remove(key);
    }

    private void OnBloodBondBeamEvent(VampireBloodBondBeamEvent ev)
    {
        var source = GetEntity(ev.Source);
        var target = GetEntity(ev.Target);

        if (!Exists(source) || !Exists(target))
            return;

        var key = (source, target);

        if (ev.Create)
        {
            CreateBeamVisual(source, target, ev.VisualPrototype);
        }
        else
        {
            if (_activeBeamVisuals.TryGetValue(key, out var beamEntity))
            {
                QueueDel(beamEntity);
                _activeBeamVisuals.Remove(key);
            }
        }
    }

    private void CreateBeamVisual(EntityUid source, EntityUid target, string visualPrototype)
    {
        var key = (source, target);

        if (_activeBeamVisuals.TryGetValue(key, out var existingBeam))
        {
            if (Exists(existingBeam))
                return;

            QueueDel(existingBeam);
        }

        var beam = Spawn(visualPrototype, Transform(source).Coordinates);

        _activeBeamVisuals[key] = beam;

        UpdateBeamVisual(beam, source, target);
    }

    private void UpdateBeamVisual(EntityUid beam, EntityUid source, EntityUid target)
    {
        if (!TryComp<SpriteComponent>(beam, out var sprite)
            || !_beamVisualQuery.TryComp(beam, out var beamVisual))
            return;

        var sourcePos = _transform.GetWorldPosition(source);
        var targetPos = _transform.GetWorldPosition(target);

        var direction = targetPos - sourcePos;
        var distance = direction.Length();

        if (distance < beamVisual.MinDistance)
            return;

        var worldAngle = direction.ToWorldAngle() + beamVisual.AngleOffset;

        var midpoint = sourcePos + (direction * 0.5f);
        _transform.SetWorldPosition(beam, midpoint);

        _transform.SetWorldRotation(beam, worldAngle);
        _sprite.SetRotation((beam, sprite), Angle.Zero);

        var length = MathF.Max(beamVisual.MinLength, distance);
        var scale = beamVisual.SpriteIsVertical
            ? new Vector2(beamVisual.Thickness, length)
            : new Vector2(length, beamVisual.Thickness);
        _sprite.SetScale((beam, sprite), scale);
        _sprite.SetOffset((beam, sprite), Vector2.Zero);
    }

    public override void Shutdown()
    {
        foreach (var beamEntity in _activeBeamVisuals.Values)
        {
            if (Exists(beamEntity))
                QueueDel(beamEntity);
        }
        _activeBeamVisuals.Clear();

        base.Shutdown();
    }
}
