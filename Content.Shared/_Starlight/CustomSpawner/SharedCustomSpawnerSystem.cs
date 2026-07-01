using System.Linq;
using System.Numerics;
using Content.Shared._Starlight.Abstract.Extensions;
using Content.Shared.Random.Helpers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.CustomSpawner;

public abstract partial class SharedCustomSpawnerSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CustomSpawnerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CustomSpawnerComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
        SubscribeLocalEvent<CustomSpawnerComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<CustomSpawnerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Enabled) continue;
            if (!comp.SpawnOnInterval) continue;
            if (_timing.CurTime < comp.NextSpawnTime) continue;
            comp.NextSpawnTime = _timing.CurTime + comp.SpawnInterval;
            DoSpawn(uid, comp);
        }
    }

    protected virtual void DoSpawn(EntityUid uid, CustomSpawnerComponent comp)
    {
        if (!comp.Enabled) return;
        if (comp.SpawnData.Count <= 0) return;
        if (comp.MaxTriggers >= 0)
        {
            if(comp.TimesTriggered >= comp.MaxTriggers) return;
            comp.TimesTriggered++;
        }
        if (!_random.ProbPredicted(_timing, comp.TriggerProb)) return;
        switch (comp.SpawnStrategy)
        {
            case SpawnStrategy.All:
                SpawnAll(uid, comp);
                break;
            case SpawnStrategy.Sequential:
                SpawnSequence(uid, comp);
                break;
            case SpawnStrategy.Random:
                SpawnRandom(uid, comp);
                break;
            default:
                SpawnAll(uid, comp);
                break;
        }

        if (comp.OneShot) comp.Enabled = false;
    }

    private void SpawnAll(EntityUid uid, CustomSpawnerComponent comp)
    {
        foreach (var data in comp.SpawnData)
            SpawnFromData(uid, comp, data);
    }

    private void SpawnSequence(EntityUid uid, CustomSpawnerComponent comp)
    {
        var data = comp.SpawnData[comp.SpawnIndex++];
        if (comp.SpawnIndex == comp.SpawnData.Count) comp.SpawnIndex = 0;
        SpawnFromData(uid, comp, data);
    }

    private void SpawnRandom(EntityUid uid, CustomSpawnerComponent comp) =>
        SpawnFromData(uid, comp,
            _random.PickPredicted(_timing, comp.SpawnData.ToDictionary(data => data, data => data.PickWeight)));

    private void SpawnFromData(EntityUid uid, CustomSpawnerComponent comp, CSpawnData data)
    {
        var xform = Transform(uid);
        EntityCoordinates? storedPosition = null;
        ComponentRegistry? storedOverrides = null;
        float? storedRotation = null;
        var rng = _random.GetPredictedRandom(_timing);
        for (var i = 0; i < data.RepeatCount + 1; i++)
        {
            // RNG check
            if (!_random.ProbPredicted(_timing, data.SpawnProb, rng.Next()))
                continue;
            // Only consume a spawn if it passes RNG.
            if (data.MaxSpawns >= 0)
            {
                if (data.TimesSpawned >= data.MaxSpawns) return;
                data.TimesSpawned++;
            }

            if ((data.RepeatSameOffsets && storedPosition is null) || !data.RepeatSameOffsets)
                storedPosition = GetSpawnPosition(comp, data, xform, rng);
            if ((data.RepeatSameRotations && storedRotation is null) || !data.RepeatSameRotations)
                storedRotation = GetSpawnRotation(comp, data, rng);
            if ((data.RepeatSameOverrides && storedOverrides is null) || !data.RepeatSameOverrides)
                storedOverrides = data.Overrides.Count > 0
                    ? data.Overrides.Count == 1
                        ? data.Overrides.First()
                        : _random.PickPredicted(_timing, data.Overrides, rng.Next())
                    : null;
            // Impossible to get here with storedPosition being null, should be anyway.
            var spawned = PredictedSpawnAtPosition(data.ProtoId, storedPosition!.Value, storedOverrides);
            Transform(spawned).LocalRotation = float.DegreesToRadians(storedRotation!.Value);
        }
    }

    private EntityCoordinates GetSpawnPosition(CustomSpawnerComponent comp, CSpawnData data, TransformComponent xform, System.Random rng)
    {
        var pos = xform.Coordinates + new EntityCoordinates(xform.ParentUid, comp.GlobalSpawnOffset);
        switch (data.SpawnOffsets.Count)
        {
            case 0:
                break;
            case 1:
                pos += new EntityCoordinates(xform.ParentUid, data.SpawnOffsets.First());
                break;
            default:
                pos += new EntityCoordinates(xform.ParentUid,
                    _random.PickPredicted(_timing, data.SpawnOffsets, rng.Next()));
                break;
        }

        // apply range if enabled
        if (!data.UseOffsetRange) return pos;
        var x = _random.NextFloatPredicted(_timing, data.OffsetRangeMin.X, data.OffsetRangeMax.X, rng.Next());
        var y = _random.NextFloatPredicted(_timing, data.OffsetRangeMin.Y, data.OffsetRangeMax.Y, rng.Next());
        pos += new EntityCoordinates(xform.ParentUid, new Vector2(x, y));
        return pos;
    }

    private float GetSpawnRotation(CustomSpawnerComponent comp, CSpawnData data, System.Random rng)
    {
        var rotation = comp.GlobalSpawnRotation;
        switch (data.SpawnRotations.Count)
        {
            case 0:
                break;
            case 1:
                rotation += data.SpawnRotations.First();
                break;
            default:
                rotation += _random.PickPredicted(_timing, data.SpawnRotations, rng.Next());
                break;
        }

        // apply range if enabled
        if (!data.UseRotationRange) return rotation;
        rotation += _random.NextFloatPredicted(_timing, data.RotationRangeMin, data.RotationRangeMax, rng.Next());
        return rotation;
    }

    private void OnMapInit(Entity<CustomSpawnerComponent> ent, ref MapInitEvent args)
    {
        Dirty(ent); // Force dirty to trigger update on client when spawned or during mapinit because client is fucking stupid.
        if (ent.Comp.LightVisible)
        {
            _light.SetEnabled(ent, true);
            _light.SetColor(ent, Color.InterpolateBetween(ent.Comp.HologramColor1, ent.Comp.HologramColor2, 0.5f));
        }
        else _light.SetEnabled(ent, false);
        if (ent.Comp.IsMarker) return;
        ent.Comp.HologramEntity = PredictedSpawnAttachedTo(ent.Comp.HologramProtoId, Transform(ent).Coordinates);
        _xform.SetParent(ent.Comp.HologramEntity.Value, ent); // PredictedSpawnAttachedTo seems to just not work for this??? so here we are i guess
        _xform.SetLocalPosition(ent.Comp.HologramEntity.Value, ent.Comp.HologramOffset);
        UpdateHologram(ent, (
            ent.Comp.HologramEntity.Value,
            Comp<CustomSpawnerHologramComponent>(ent.Comp.HologramEntity.Value)
        ));
    }

    private void OnAfterAutoHandleState(Entity<CustomSpawnerComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Comp.LightVisible)
        {
            _light.SetEnabled(ent, true);
            _light.SetColor(ent, Color.InterpolateBetween(ent.Comp.HologramColor1, ent.Comp.HologramColor2, 0.5f));
        }
        else _light.SetEnabled(ent, false);
        if (ent.Comp.IsMarker) return;
        if (TryComp<CustomSpawnerHologramComponent>(ent.Comp.HologramEntity, out var holo))
            UpdateHologram(ent, (ent.Comp.HologramEntity.Value, holo));
    }

    private void OnShutdown(Entity<CustomSpawnerComponent> ent, ref ComponentShutdown args)
    {
        if(ent.Comp.HologramEntity is not null)
            PredictedQueueDel(ent.Comp.HologramEntity.Value);
    }

    protected virtual void UpdateHologram(Entity<CustomSpawnerComponent> ent, Entity<CustomSpawnerHologramComponent> holo)
    {
        holo.Comp.Color1 = ent.Comp.HologramColor1;
        holo.Comp.Color2 = ent.Comp.HologramColor2;
        if (ent.Comp.HologramSprite is not null)
        {
            holo.Comp.Rsi = ent.Comp.HologramSprite.RsiPath.ToString();
            holo.Comp.State = ent.Comp.HologramSprite.RsiState;
        }
        _xform.SetLocalPosition(holo, ent.Comp.HologramOffset);
    }
}
