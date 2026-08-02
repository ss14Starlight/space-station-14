using Content.Client.Pinpointer.UI;
using Content.Shared._Starlight.Medical.Virology;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Medical.Virology;

[UsedImplicitly]
public sealed class PathogenContaminationScannerBoundUserInterface(
    EntityUid owner,
    Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private StationMapWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<StationMapWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window is null ||
            state is not PathogenContaminationScannerUiState scannerState)
        {
            return;
        }

        var grid = EntMan.GetEntity(scannerState.Grid);
        _window.Set(scannerState.StationName, grid, Owner);
        _window.MapControl.TrackedEntities.Clear();
        var annotations = new Dictionary<NetEntity, string>();

        var texture = EntMan.System<SpriteSystem>().Frame0(
            new SpriteSpecifier.Texture(
                new ResPath("/Textures/Interface/NavMap/beveled_circle.png")));

        foreach (var group in scannerState.Groups)
        {
            var color = GetGroupColor(group);
            var scale = Math.Clamp(0.65f + group.Total / 20f, 0.65f, 1.5f);
            _window.MapControl.TrackedEntities[group.Beacon] = new NavMapBlip(
                EntMan.GetCoordinates(group.Coordinates),
                texture,
                color,
                group.InfectiousSourceCount > 0,
                false,
                scale);
            annotations[group.Beacon] =
                Loc.GetString(
                    "pathogen-contamination-map-beacon-annotation",
                    ("level", group.Total.ToString("0.0")),
                    ("sources", group.SourceCount),
                    ("infectious", group.InfectiousSourceCount));
        }

        _window.SetBeaconAnnotations(annotations);
        _window.MapControl.ForceNavMapUpdate();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            _window?.Close();
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
