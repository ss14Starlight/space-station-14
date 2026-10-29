using System.Globalization;

namespace Content.Shared._Starlight.Computers.RemoteControl;

public static class RemoteControlChannel
{
    public const int DigitCount = 6;
    public const int ChannelCount = 1_000_000;

    public static bool IsValid(int channel)
        => channel >= 0 && channel < ChannelCount;

    public static string Format(int channel)
    {
        if (!IsValid(channel))
            throw new ArgumentOutOfRangeException(nameof(channel));

        return channel.ToString("D6", CultureInfo.InvariantCulture);
    }

    public static bool TryParse(string? value, out int channel)
    {
        channel = default;
        if (value is not { Length: DigitCount })
            return false;

        foreach (var character in value)
        {
            if (character is < '0' or > '9')
                return false;
        }

        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out channel);
    }
}
