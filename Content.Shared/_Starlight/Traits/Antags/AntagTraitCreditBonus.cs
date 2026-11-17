namespace Content.Shared._Starlight.Traits.Antags;

public static class AntagTraitCreditBonus
{
    public static int Apply(int credits) => credits > 0 ? (int)Math.Ceiling(credits * 1.1m) : credits;
}
