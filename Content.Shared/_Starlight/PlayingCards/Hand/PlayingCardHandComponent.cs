namespace Content.Shared._Starlight.PlayingCards.Hand;

[RegisterComponent]
public sealed partial class PlayingCardHandComponent : Component
{
    [DataField] public float Angle = 120f;

    [DataField] public float XOffset = 0.5f;

    [DataField] public float Scale = 1;

    [DataField] public int Limit = 10;
}