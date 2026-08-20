using System;
using Robust.Shared.Localization;

namespace Content.Shared._Starlight.TextToSpeech;

/// <summary>
/// A normalized value type representing a sanitized voice selector tag.
/// Ensures lowercase, trimmed identity invariants, and centralizes localization formatting.
/// </summary>
public readonly struct VoiceTag : IEquatable<VoiceTag>
{
    public string Value { get; }

    public VoiceTag(string rawTag)
    {
        if (string.IsNullOrWhiteSpace(rawTag))
            throw new ArgumentException("Tag value cannot be null or empty.", nameof(rawTag));
        Value = rawTag.Trim().ToLowerInvariant();
    }

    public string ToLocalizationKey()
    {
        return $"tts-tag-{Value.Replace(" ", "-")}";
    }

    public string ToDisplayName()
    {
        // Check for custom localization key first
        if (Loc.TryGetString(ToLocalizationKey(), out var localized))
            return localized;

        // Fallback to title casing
        if (string.IsNullOrEmpty(Value))
            return Value;

        var chars = Value.ToCharArray();
        bool capitalizeNext = true;
        for (int i = 0; i < chars.Length; i++)
        {
            if (chars[i] == '-' || chars[i] == ' ')
            {
                capitalizeNext = true;
            }
            else if (capitalizeNext)
            {
                chars[i] = char.ToUpper(chars[i]);
                capitalizeNext = false;
            }
        }
        return new string(chars);
    }

    public bool Equals(VoiceTag other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is VoiceTag other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => Value;

    public static bool operator ==(VoiceTag left, VoiceTag right) => left.Equals(right);
    public static bool operator !=(VoiceTag left, VoiceTag right) => !left.Equals(right);

    public static implicit operator string(VoiceTag tag) => tag.Value;
    public static implicit operator VoiceTag(string rawTag) => new(rawTag);
}
