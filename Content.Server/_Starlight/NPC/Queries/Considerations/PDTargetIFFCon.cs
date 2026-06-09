using Content.Server.NPC.Queries.Considerations;

namespace Content.Server._Starlight.NPC.Queries.Considerations;

/// <summary>
/// Returns 1f if the target is not shot from the same grid or 0f if its is.
/// </summary>
public sealed partial class PDTargetIFFCon : UtilityConsideration;
