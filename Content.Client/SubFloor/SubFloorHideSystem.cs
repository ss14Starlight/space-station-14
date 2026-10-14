using Content.Client.UserInterface.Systems.Sandbox;
using Content.Shared.Atmos.Components;
using Content.Shared.SubFloor;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Player;

namespace Content.Client.SubFloor;

public sealed partial class SubFloorHideSystem : SharedSubFloorHideSystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;

    private bool _showVentPipe;
    public SubFloorVisibilityMask _showLayers; //Starlight edit - Subfloor layers


    [ViewVariables(VVAccess.ReadWrite)]
    public bool ShowVentPipe
    {
        get => _showVentPipe;
        set
        {
            if (_showVentPipe == value) return;
            _showVentPipe = value;

            var ev = new ShowSubfloorRequestEvent()
            {
                Value = value,
            };
            RaiseNetworkEvent(ev);
        }
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SubFloorHideComponent, AppearanceChangeEvent>(OnAppearanceChanged);
        SubscribeNetworkEvent<ShowSubfloorRequestEvent>(OnRequestReceived);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    private void OnPlayerDetached(LocalPlayerDetachedEvent ev)
    {
        // Vismask resets so need to reset this.
        // Starlight-start
        _showLayers = SubFloorVisibilityMask.None;
        var req = new ShowSubfloorRequestEvent()
        {
            Value = false,
        };
        RaiseNetworkEvent(req);
        // Starlight-end
    }

    private void OnRequestReceived(ShowSubfloorRequestEvent ev)
    {
        // When client receives request Queue an update on all vis.
        UpdateAll();
    }

    private void OnAppearanceChanged(EntityUid uid, SubFloorHideComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        _appearance.TryGetData<bool>(uid, SubFloorVisuals.Covered, out var covered, args.Component);
        _appearance.TryGetData<bool>(uid, SubFloorVisuals.ScannerRevealed, out var scannerRevealed, args.Component);

        scannerRevealed &= _showLayers == SubFloorVisibilityMask.None; // Starlight-edit

        var showVentPipe = false;
        if (HasComp<PipeAppearanceComponent>(uid))
        {
            showVentPipe = ShowVentPipe;
        }

        var revealed = !covered || scannerRevealed || showVentPipe || (_showLayers & (SubFloorVisibilityMask)component.SubfloorLayer) != 0; //Starlight edit - Subfloor layers

        // set visibility & color of each layer
        foreach (var layer in args.Sprite.AllLayers)
        {
            // pipe connection visuals are updated AFTER this, and may re-hide some layers
            layer.Visible = revealed;
        }

        // Is there some layer that is always visible?
        var hasVisibleLayer = false;
        foreach (var layerKey in component.VisibleLayers)
        {
            if (!_sprite.LayerMapTryGet((uid, args.Sprite), layerKey, out var layerIndex, false))
                continue;

            var layer = args.Sprite[layerIndex];
            layer.Visible = true;
            layer.Color = layer.Color.WithAlpha(1f);
            hasVisibleLayer = true;
        }

        _sprite.SetVisible((uid, args.Sprite), hasVisibleLayer || revealed);

        if ((_showLayers & (SubFloorVisibilityMask)component.SubfloorLayer) != 0) //Starlight-edit
        {
            // Allows sandbox mode to make wires visible over other stuff.
            component.OriginalDrawDepth ??= args.Sprite.DrawDepth;
            _sprite.SetDrawDepth((uid, args.Sprite), (int)Shared.DrawDepth.DrawDepth.Overdoors);
        }
        else if (scannerRevealed)
        {
            // Allows a t-ray to show wires/pipes above carpets/puddles.
            if (component.OriginalDrawDepth is not null)
                return;
            component.OriginalDrawDepth = args.Sprite.DrawDepth;
            var drawDepthDifference = Shared.DrawDepth.DrawDepth.ThickPipe - Shared.DrawDepth.DrawDepth.Puddles;
            _sprite.SetDrawDepth((uid, args.Sprite), args.Sprite.DrawDepth - (drawDepthDifference - 1));
        }
        else if (component.OriginalDrawDepth.HasValue)
        {
            _sprite.SetDrawDepth((uid, args.Sprite), component.OriginalDrawDepth.Value);
            component.OriginalDrawDepth = null;
        }
    }

    private void UpdateAll()
    {
        var query = AllEntityQuery<SubFloorHideComponent, AppearanceComponent>();
        while (query.MoveNext(out var uid, out _, out var appearance))
        {
            _appearance.QueueUpdate(uid, appearance);
        }
    }
}
