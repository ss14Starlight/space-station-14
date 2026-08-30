// ReSharper disable CheckNamespace

using Robust.Shared.Map;

namespace Content.Client.Shuttles.UI;

public sealed partial class NavScreen
{
    public event Action<EntityCoordinates>? OnRadarClick;

    partial void InitializeStarlight()
    {
        NavRadar.OnRadarClick += OnRadarClickPressed;
    }

    private void OnRadarClickPressed(EntityCoordinates coordinates)
    {
        OnRadarClick?.Invoke(coordinates);
    }
}
