using System.Text;
using Robust.Shared.Random;

namespace Content.Shared._Starlight.Humanoid;

public sealed partial class RomanNamingSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly Dictionary<string, int> RomanMap = new()
    {
        { "M", 1000 },
        { "CM", 900 },
        { "D", 500 },
        { "CD", 400 },
        { "C", 100 },
        { "XC", 90 },
        { "L", 50 },
        { "XL", 40 },
        { "X", 10 },
        { "IX", 9 },
        { "V", 5 },
        { "IV", 4 },
        { "I", 1 }
    };

    public string GenerateRomanNumeral()
    {
        (int, int) range;
        while (true)
        {
            if (_random.Prob(0.8f))
                range = (1, 101);
            else if (_random.Prob(0.6f))
                range = (101, 501);
            else if (_random.Prob(0.75f))
                range = (501, 1001);
            else
                range = (1001, 4000);

            var numeral = IntToRomanNumeral(_random.Next(range.Item1, range.Item2));

            if (numeral.Length > 8)
                continue;

            return numeral;
        }
    }

    private static string IntToRomanNumeral(int number)
    {
        var sb = new StringBuilder();
        foreach (var (letters, equivalentNumber) in RomanMap)
        {
            while (number >= equivalentNumber)
            {
                sb.Append(letters);
                number -= equivalentNumber;
            }
        }

        return sb.ToString();
    }
}
