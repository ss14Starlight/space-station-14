using Content.Client.Pinpointer.UI;
using Content.Shared.Pinpointer;
using Content.Shared._Starlight.Medical.Virology;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Medical.Virology;

[UsedImplicitly]
public sealed class PathogenDetectorBoundUserInterface(
    EntityUid owner,
    Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private PathogenDetectorWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<PathogenDetectorWindow>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window is null || state is not PathogenDetectorUiState detectorState)
            return;

        _window.UpdateState(detectorState);

        var map = _window.MapControl;
        map.MapUid = detectorState.Grid is { } grid ? EntMan.GetEntity(grid) : null;
        map.TrackedCoordinates.Clear();
        map.TrackedEntities.Clear();

        var texture = EntMan.System<SpriteSystem>().Frame0(
            new SpriteSpecifier.Texture(
                new ResPath("/Textures/Interface/NavMap/beveled_circle.png")));

        foreach (var group in detectorState.Groups)
        {
            var color = GetGroupColor(group);
            var scale = Math.Clamp(0.65f + group.Total / 20f, 0.65f, 1.5f);
            map.TrackedEntities[group.Beacon] = new NavMapBlip(
                EntMan.GetCoordinates(group.Coordinates),
                texture,
                color,
                group.InfectiousSourceCount > 0,
                false,
                scale);
        }

        map.ForceNavMapUpdate();
    }

    private static Color GetGroupColor(PathogenContaminationBeaconGroup group)
    {
        if (Math.Abs(group.Bacteria - group.Fungus) < 0.05f)
            return Color.FromHex("#d477e8");

        return group.Bacteria > group.Fungus
            ? Color.FromHex("#f0a34a")
            : Color.FromHex("#69d46f");
    }
}
