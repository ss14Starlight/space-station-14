// ReSharper disable CheckNamespace

using Robust.Shared.Map;

namespace Content.Client.Shuttles.UI;

public sealed partial class ShuttleConsoleWindow
{
    public event Action<EntityCoordinates>? RadarClicked;

    partial void InitializeStarlight()
        => NavContainer.OnRadarClick += OnRadarClick;
    

    private void OnRadarClick(EntityCoordinates coordinates)
        => RadarClicked?.Invoke(coordinates);
}
