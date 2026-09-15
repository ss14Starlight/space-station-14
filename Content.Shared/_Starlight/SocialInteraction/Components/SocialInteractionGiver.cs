namespace Content.Shared._Starlight.SocialInteraction.Components;

[RegisterComponent]
public sealed partial class SocialInteractionGiverComponent : Component
{
    /// <summary>
    /// Stores the last time this Giver did a social interaction.
    /// Needed to prevent Givers from spamming social interactions.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan? LastInteractTime;
}
