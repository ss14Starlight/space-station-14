using Content.Client.Resources;
using Content.Shared.MapText;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Configuration;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Client.MapText;

/// <inheritdoc/>
public sealed partial class MapTextSystem : SharedMapTextSystem
{
    [Dependency] private IConfigurationManager _configManager = default!;
    [Dependency] private IUserInterfaceManager _uiManager = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private IOverlayManager _overlayManager = default!;

    private MapTextOverlay _overlay = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<MapTextComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<MapTextComponent, ComponentHandleState>(HandleCompState);

        _overlay = new MapTextOverlay(_configManager, EntityManager, _uiManager, _transform);
        _overlayManager.AddOverlay(_overlay);
    }

    private void OnComponentStartup(Entity<MapTextComponent> ent, ref ComponentStartup args)
    {
        CacheText(ent.Comp);
    }

    private void HandleCompState(Entity<MapTextComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not MapTextComponentState state)
            return;

        ent.Comp.Text = state.Text;
        ent.Comp.LocText = state.LocText;
        ent.Comp.Color = state.Color;
        ent.Comp.FontId = state.FontId;
        ent.Comp.FontSize = state.FontSize;
        ent.Comp.Offset = state.Offset;

        CacheText(ent.Comp);
    }

    private void CacheText(MapTextComponent component)
    {
        component.CachedFont = null;

        component.CachedText = string.IsNullOrWhiteSpace(component.Text)
            ? Loc.GetString(component.LocText)
            : component.Text;

        if (!MapTextFonts.TryGetPath(component.FontId, out var fontPath))
        {
            component.CachedText = Loc.GetString("map-text-font-error");
            component.Color = Color.Red;
            component.CachedFont = (VectorFont) _resourceCache.GetFont(MapTextFonts.DefaultPath, 14);
            return;
        }

        component.CachedFont = (VectorFont) _resourceCache.GetFont(fontPath, component.FontSize);
    }
}
