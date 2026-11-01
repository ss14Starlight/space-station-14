using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Breadcrumb.UI;

/// <summary>
/// Sent when the player picks a preset color and hits submit in the glowstone dropper UI.
/// Carries the preset key; the server resolves it against the dropper's ColorOptions.
/// This is the only networked event the UI produces - no live sync.
/// </summary>
[Serializable, NetSerializable]
public sealed class GlowstoneDropperColorMessage(string colorKey) : BoundUserInterfaceMessage
{
    public string ColorKey = colorKey;
}
