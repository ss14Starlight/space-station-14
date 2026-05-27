using Content.Shared._Starlight.CosmicCult.Components;
using Content.Shared._Starlight.CosmicCult;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;
using Content.Shared._Starlight.CosmicCult.Components.Examine;
using System.Numerics;
using Timer = Robust.Shared.Timing.Timer;
using Robust.Client.Audio;
using Robust.Shared.Audio;
using Content.Client.Alerts;
using Content.Client.UserInterface.Systems.Alerts.Controls;

namespace Content.Client._Starlight.CosmicCult;

public sealed partial class CosmicCultSystem : SharedCosmicCultSystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private readonly ResPath _rsiPath = new("/Textures/_Starlight/CosmicCult/Effects/ability_siphonvfx.rsi");

    private readonly SoundSpecifier _siphonSFX = new SoundPathSpecifier("/Audio/_Starlight/CosmicCult/ability_siphon.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CosmicStarMarkComponent, ComponentStartup>(OnCosmicStarMarkAdded);
        SubscribeLocalEvent<CosmicStarMarkComponent, ComponentShutdown>(OnCosmicStarMarkRemoved);

        SubscribeLocalEvent<CosmicImposingComponent, ComponentStartup>(OnCosmicImpositionAdded);
        SubscribeLocalEvent<CosmicImposingComponent, ComponentShutdown>(OnCosmicImpositionRemoved);

        SubscribeLocalEvent<CosmicCultComponent, GetStatusIconsEvent>(GetCosmicCultIcon);
        SubscribeLocalEvent<CosmicCultLeadComponent, GetStatusIconsEvent>(GetCosmicCultLeadIcon);
        SubscribeLocalEvent<CosmicBlankComponent, GetStatusIconsEvent>(GetCosmicSSDIcon);

        SubscribeNetworkEvent<CosmicSiphonIndicatorEvent>(OnSiphon);
        SubscribeLocalEvent<CosmicCultComponent, UpdateAlertSpriteEvent>(OnUpdateAlert);
    }

    #region Siphon Visuals
    private void OnSiphon(CosmicSiphonIndicatorEvent args)
    {
        var ent = GetEntity(args.Target);
        var layer = _sprite.AddLayer(ent, new SpriteSpecifier.Rsi(_rsiPath, "vfx"));
        _sprite.LayerMapSet(ent, CultSiphonedVisuals.Key, layer);
        _sprite.LayerSetOffset(ent, layer, new Vector2(0, 0.8f));
        _sprite.LayerSetScale(ent, layer, new Vector2(0.65f, 0.65f));
        if (TryComp<SpriteComponent>(ent, out var sprite))
            sprite.LayerSetShader(layer, "unshaded");

        Timer.Spawn(TimeSpan.FromSeconds(2), () => _sprite.RemoveLayer(ent, CultSiphonedVisuals.Key));
        _audio.PlayLocal(_siphonSFX, ent, ent, AudioParams.Default.WithVariation(0.1f));
    }

    private void OnUpdateAlert(Entity<CosmicCultComponent> ent, ref UpdateAlertSpriteEvent args)
    {
        if (args.Alert.ID != ent.Comp.EntropyAlert)
            return;
        var entropy = Math.Clamp(ent.Comp.EntropyStored, 0, 14);
        _sprite.LayerSetRsiState(args.SpriteViewEnt.Owner, AlertVisualLayers.Base, $"base{entropy}");
        _sprite.LayerSetRsiState(args.SpriteViewEnt.Owner, CultAlertVisualLayers.Counter, $"num{entropy}");
    }
    #endregion

    #region Layer Additions
    private void OnCosmicStarMarkAdded(Entity<CosmicStarMarkComponent> uid, ref ComponentStartup args)
    {
        if (_sprite.LayerMapTryGet(uid.Owner, CosmicRevealedKey.Key, out _, false))
            return;

        var layer = _sprite.AddLayer(uid.Owner, uid.Comp.Sprite);
        _sprite.LayerMapSet(uid.Owner, CosmicRevealedKey.Key, layer);
        if (TryComp<SpriteComponent>(uid.Owner, out var sprite))
            sprite.LayerSetShader(layer, "unshaded");

        //offset the mark if the mob has an offset comp, needed for taller species like Thaven
        if (TryComp<CosmicStarMarkOffsetComponent>(uid, out var offset))
        {
            _sprite.LayerSetOffset(uid.Owner, CosmicRevealedKey.Key, offset.Offset);
        }
    }

    private void OnCosmicImpositionAdded(Entity<CosmicImposingComponent> uid, ref ComponentStartup args)
    {
        if (_sprite.LayerMapTryGet(uid.Owner, CosmicImposingKey.Key, out _, false))
            return;

        var layer = _sprite.AddLayer(uid.Owner, uid.Comp.Sprite);

        _sprite.LayerMapSet(uid.Owner, CosmicImposingKey.Key, layer);
        if (TryComp<SpriteComponent>(uid, out var sprite))
            sprite.LayerSetShader(layer, "unshaded");
    }
    #endregion

    #region Layer Removals
    private void OnCosmicStarMarkRemoved(Entity<CosmicStarMarkComponent> uid, ref ComponentShutdown args)
        => _sprite.RemoveLayer(uid.Owner, CosmicRevealedKey.Key);

    private void OnCosmicImpositionRemoved(Entity<CosmicImposingComponent> uid, ref ComponentShutdown args)
        => _sprite.RemoveLayer(uid.Owner, CosmicImposingKey.Key);
    #endregion

    #region Icons
    private void GetCosmicCultIcon(Entity<CosmicCultComponent> ent, ref GetStatusIconsEvent args)
    {
        if (HasComp<CosmicCultLeadComponent>(ent))
            return;

        if (_prototype.TryIndex(ent.Comp.StatusIcon, out var iconPrototype))
            args.StatusIcons.Add(iconPrototype);
    }

    private void GetCosmicCultLeadIcon(Entity<CosmicCultLeadComponent> ent, ref GetStatusIconsEvent args)
    {
        if (_prototype.TryIndex(ent.Comp.StatusIcon, out var iconPrototype))
            args.StatusIcons.Add(iconPrototype);
    }

    private void GetCosmicSSDIcon(Entity<CosmicBlankComponent> ent, ref GetStatusIconsEvent args)
    {
        if (_prototype.TryIndex(ent.Comp.StatusIcon, out var iconPrototype))
            args.StatusIcons.Add(iconPrototype);
    }
    #endregion
}

public enum CultSiphonedVisuals : byte
{
    Key
}
