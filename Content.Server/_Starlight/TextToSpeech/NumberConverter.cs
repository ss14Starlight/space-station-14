using System.Text;
using System.Text.RegularExpressions;

namespace Content.Server.Starlight.TTS;

public static partial class NumberConverter
{
    private static readonly string[] _units =
    [
        "", "one", "two", "three", "four", "five", "six",
        "seven", "eight", "nine", "ten", "eleven",
        "twelve", "thirteen", "fourteen", "fifteen",
        "sixteen", "seventeen", "eighteen", "nineteen"
    ];

    private static readonly string[] _tens =
    [
        "", "ten", "twenty", "thirty", "forty", "fifty",
        "sixty", "seventy", "eighty", "ninety"
    ];

    private static readonly string[] _scales =
    [
        "", "thousand", "million", "billion", "trillion",
        "quadrillion", "quintillion", "sextillion", "septillion"
    ];

    private static readonly string[] _digits =
    ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];

    public static string Convert(string numberStr)
    {
        numberStr = numberStr.Replace(",", "");

        if (numberStr.Contains('.'))
        {
            var parts = numberStr.Split('.', 2);
            var integerPart = string.IsNullOrEmpty(parts[0]) || parts[0] == "-"
                ? "zero"
                : NumberToText(long.Parse(parts[0]));

            var decimalPart = DecimalToText(parts[1]);

            return $"{integerPart} point {decimalPart}";
        }

        return NumberToText(long.Parse(numberStr));
    }

    private static string DecimalToText(string digits)
    {
        var result = new StringBuilder();

        foreach (var c in digits)
        {
            if (char.IsDigit(c))
            {
                if (result.Length > 0)
                    result.Append(' ');
                result.Append(_digits[c - '0']);
            }
        }

        return result.ToString();
    }

    public static string NumberToText(long number)
    {
        if (number == 0)
            return "zero";

        if (number < 0)
            return "negative " + NumberToText(-number);

        var words = new StringBuilder();
        var unit = 0;

        while (number > 0)
        {
            if (number % 1000 != 0)
            {
                var chunk = ConvertChunk((int)(number % 1000));

                if (!string.IsNullOrEmpty(_scales[unit]))
                    chunk += " " + _scales[unit];

                if (words.Length > 0)
                    words.Insert(0, chunk + " ");
                else
                    words.Append(chunk);
            }

            number /= 1000;
            unit++;
        }

        return words.ToString().Trim();
    }

    private static string ConvertChunk(int number)
    {
        var result = new StringBuilder();

        var hundreds = number / 100;
        var tensUnits = number % 100;

        if (hundreds > 0)
        {
            result.Append(_units[hundreds] + " hundred");
            if (tensUnits > 0)
                result.Append(' ');
        }

        if (tensUnits > 0)
        {
            if (tensUnits < 20)
            {
                result.Append(_units[tensUnits]);
            }
            else
            {
                var tens = tensUnits / 10;
                var units = tensUnits % 10;

                result.Append(_tens[tens]);
                if (units > 0)
                    result.Append('-' + _units[units]);
            }
        }

        return result.ToString();
    }

    [GeneratedRegex(@"-?\d{1,3}(,\d{3})*(\.\d+)?|-?\d+(\.\d+)?")]
    public static partial Regex NumberPattern();
}
