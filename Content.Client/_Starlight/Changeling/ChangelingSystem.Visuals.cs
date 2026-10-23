using System.Numerics;
using Content.Shared._Starlight.Changeling;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._Starlight.Changeling;

public sealed partial class ChangelingSystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private AppearanceSystem _appearance = default!;

    private const string LayerKey = "changeling-fragment";
    private readonly Dictionary<EntityUid, OriginalSprite> _originals = new();

    private void InitializeColonyVisuals()
    {
        SubscribeLocalEvent<ChangelingColonyMemberComponent, AppearanceChangeEvent>(OnAppearance);
        SubscribeLocalEvent<ChangelingColonyMemberComponent, ComponentRemove>(OnRemoved);
    }

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
            original = new OriginalSprite(sprite);
            _originals.Add(ent, original);
            _sprite.SetColor((ent, sprite), Color.White);
        }

        var newLayer = !_sprite.LayerMapTryGet((ent, sprite), LayerKey, out var layer, false);
        if (newLayer)
            layer = _sprite.LayerMapReserve((ent, sprite), LayerKey);
        var index = 0;
        foreach (var existing in sprite.AllLayers)
        {
            if (original.Layers.ContainsKey(existing))
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

    private void OnRemoved(Entity<ChangelingColonyMemberComponent> ent, ref ComponentRemove args)
    {
        if (!_originals.Remove(ent, out var original) || TerminatingOrDeleted(ent)
            || !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (_sprite.LayerMapTryGet((ent, sprite), LayerKey, out var layer, false))
            _sprite.RemoveLayer((ent, sprite), layer);
        var index = 0;
        foreach (var existing in sprite.AllLayers)
        {
            if (original.Layers.TryGetValue(existing, out var visible))
                _sprite.LayerSetVisible((ent, sprite), index, visible);
            index++;
        }
        _sprite.SetScale((ent, sprite), original.Scale);
        _sprite.SetOffset((ent, sprite), original.Offset);
        _sprite.SetRotation((ent, sprite), original.Rotation);
        _sprite.SetColor((ent, sprite), original.Color);
        _sprite.SetDrawDepth((ent, sprite), original.DrawDepth);
        sprite.NoRotation = original.NoRotation;
        sprite.EnableDirectionOverride = original.EnableDirectionOverride;
    }

    private sealed class OriginalSprite
    {
        public string? Name;
        public bool Dead;
        public readonly Dictionary<ISpriteLayer, bool> Layers = new();
        public readonly Vector2 Scale;
        public readonly Vector2 Offset;
        public readonly Angle Rotation;
        public readonly Color Color;
        public readonly int DrawDepth;
        public readonly bool NoRotation;
        public readonly bool EnableDirectionOverride;

        public OriginalSprite(SpriteComponent sprite)
        {
            foreach (var layer in sprite.AllLayers)
                Layers.Add(layer, layer.Visible);
            Scale = sprite.Scale;
            Offset = sprite.Offset;
            Rotation = sprite.Rotation;
            Color = sprite.Color;
            DrawDepth = sprite.DrawDepth;
            NoRotation = sprite.NoRotation;
            EnableDirectionOverride = sprite.EnableDirectionOverride;
        }
    }
}
