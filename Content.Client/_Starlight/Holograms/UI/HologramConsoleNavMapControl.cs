using Content.Client.Pinpointer.UI;

namespace Content.Client._Starlight.Holograms.UI;

/// <summary>
/// NavMap control for hologram console showing projector locations
/// </summary>
public sealed class HologramConsoleNavMapControl : NavMapControl
{
    public NetEntity? SelectedProjector;
    
    // Colors for projector blips
    private readonly Color _selectedColor = Color.FromHex("#10b981"); // Green
    private readonly Color _unselectedColor = Color.FromHex("#ef4444"); // Red

    public HologramConsoleNavMapControl() : base()
    {
        WallColor = Color.FromHex("#66d9c4"); // Bright cyan
        TileColor = Color.FromHex("#326e64"); // Teal
        BackgroundColor = Color.FromHex("#0a1612"); // Dark teal
    }

    public Color GetProjectorColor(bool isSelected) =>
        isSelected ? _selectedColor : _unselectedColor;
}
