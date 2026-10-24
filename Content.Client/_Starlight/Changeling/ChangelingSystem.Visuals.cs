using System.Numerics;
using Content.Shared._Starlight.Changeling;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Utility;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._Starlight.Changeling;

public sealed partial class ChangelingSystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private AppearanceSystem _appearance = default!;

    private const string LayerKey = "changeling-fragment";
    private readonly Dictionary<EntityUid, OriginalSprite> _originals = new();

    [SubscribeLocalEvent]
    private void OnAppearance(Entity<ChangelingColonyMemberComponent> ent, ref AppearanceChangeEvent args)
        => UpdateSprite(ent);

    private void UpdateSprite(Entity<ChangelingColonyMemberComponent> ent)
    {
        if (!ent.Comp.IsFragment || !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var (name, scale) = ent.Comp.IsBlood ? ("blood", 0.27f) : ent.Comp.FragmentType switch
        {
            BodyPartType.Head => ("head", 0.65f),
            BodyPartType.Torso => ("torso", 0.81f),
            BodyPartType.Arm => ("arm", 0.46f),
            BodyPartType.Hand => ("hand", 0.37f),
            BodyPartType.Leg => ("leg", 0.46f),
            BodyPartType.Foot => ("foot", 0.38f),
            BodyPartType.Tail => ("tail", 0.50f),
            _ => (null, 1f),
        };
        if (name == null)
            return;

        if (!_originals.TryGetValue(ent, out var original))
        {
            var backup = Spawn(null, MapCoordinates.Nullspace);
            var backupSprite = AddComp<SpriteComponent>(backup);
            _sprite.CopySprite((ent, sprite), (backup, backupSprite));
            original = new OriginalSprite((backup, backupSprite), sprite.AllLayers);
            _originals.Add(ent, original);
            _sprite.SetColor((ent, sprite), Color.White);
        }

        var newLayer = !_sprite.LayerMapTryGet((ent, sprite), LayerKey, out var layer, false);
        if (newLayer)
            layer = _sprite.LayerMapReserve((ent, sprite), LayerKey);
        var index = 0;
        foreach (var existing in sprite.AllLayers)
        {
            if (original.Layers.Contains(existing))
                _sprite.LayerSetVisible((ent, sprite), index, false);
            index++;
        }

        var state = TryComp<MobStateComponent>(ent, out var mob) ? mob.CurrentState : MobState.Alive;
        if (_appearance.TryGetData<MobState>(ent, MobStateVisuals.State, out var appearanceState))
            state = appearanceState;
        var dead = state == MobState.Dead;
        // Setting the RSI again resets its animation, even when its state has not changed.
        if (newLayer || original.Name != name || original.Dead != dead)
        {
            _sprite.LayerSetRsi((ent, sprite), layer, new ResPath($"_Starlight/Mobs/Changeling/fragments/{name}.rsi"), dead ? "dead" : "alive");
            original.Name = name;
            original.Dead = dead;
        }
        _sprite.LayerSetAutoAnimated((ent, sprite), layer, !dead);
        _sprite.LayerSetVisible((ent, sprite), layer, true);
        _sprite.SetScale((ent, sprite), new Vector2(scale));
        _sprite.SetOffset((ent, sprite), Vector2.Zero);
        _sprite.SetRotation((ent, sprite), Angle.Zero);
        _sprite.SetDrawDepth((ent, sprite), (int) (dead ? DrawDepth.DeadMobs : DrawDepth.Mobs));
        sprite.NoRotation = true;
        sprite.EnableDirectionOverride = false;
    }

    [SubscribeLocalEvent]
    private void OnRemoved(Entity<ChangelingColonyMemberComponent> ent, ref ComponentRemove args)
    {
        if (!_originals.Remove(ent, out var original))
            return;
        if (!TerminatingOrDeleted(ent) && TryComp<SpriteComponent>(ent, out var sprite))
        {
            _sprite.CopySprite(original.Backup.AsNullable(), (ent, sprite));
            if (TryComp<AppearanceComponent>(ent, out var appearance))
                _appearance.QueueUpdate(ent, appearance);
        }
        QueueDel(original.Backup);
    }

    private sealed class OriginalSprite
    {
        public string? Name;
        public bool Dead;
        public readonly Entity<SpriteComponent> Backup;
        public readonly HashSet<ISpriteLayer> Layers;

        public OriginalSprite(Entity<SpriteComponent> backup, IEnumerable<ISpriteLayer> layers)
        {
            Backup = backup;
            Layers = new HashSet<ISpriteLayer>(layers);
        }
    }
}
