namespace Content.Shared._Starlight.Dice;

public sealed class DiceRolledEvent(int value) : EntityEventArgs
{
    public int Value = value;
}