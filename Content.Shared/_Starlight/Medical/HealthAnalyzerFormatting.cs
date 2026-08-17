using Content.Shared.Atmos;
using Content.Shared.Mobs;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Medical;

// Shared analyzer formatting for UI and printable reports.
public static class HealthAnalyzerFormatting
{
    public static readonly string[] DamageGroupOrder = { "Burn", "Brute", "Airloss", "Toxin", "Genetic" };
    public static readonly string[] ReagentGroupOrder = { "Medicine", "Narcotics", "Toxins", "Pyrotechnic", "Botanical", "Biological", "Foods", "Drinks", "Elements", "Special" };
    private static readonly Color SeveritySafeColor = Color.FromHex("#00FF00");
    private static readonly Color SeverityDangerColor = Color.FromHex("#8B0000");

    public static string FormatTemperature(float temperature)
    {
        return float.IsNaN(temperature)
            ? Loc.GetString("health-analyzer-window-entity-unknown-value-text")
            : $"{temperature - Atmospherics.T0C:F1} °C ({temperature:F1} K)";
    }

    public static string FormatBloodLevel(float bloodLevel)
    {
        return float.IsNaN(bloodLevel)
            ? Loc.GetString("health-analyzer-window-entity-unknown-value-text")
            : $"{bloodLevel * 100:F1}%";
    }

    public static string FormatBloodLevelWithSeverity(float bloodLevel)
    {
        var severitySuffix = GetBloodLevelSeveritySuffix(bloodLevel);
        var formattedBloodLevel = FormatBloodLevel(bloodLevel);
        return string.IsNullOrEmpty(severitySuffix)
            ? formattedBloodLevel
            : $"{formattedBloodLevel} {severitySuffix}";
    }

    public static string FormatBloodLevelMarkup(float bloodLevel)
    {
        return WrapTextWithColorMarkup(
            FormatBloodLevelWithSeverity(bloodLevel),
            GetBloodLevelSeverityColor(bloodLevel));
    }

    public static string GetBloodLevelSeveritySuffix(float bloodLevel)
    {
        if (float.IsNaN(bloodLevel))
            return string.Empty;

        return bloodLevel switch
        {
            <= 0f => "!!!!",
            <= 0.25f => "!!!",
            <= 0.5f => "!!",
            <= 0.75f => "!",
            _ => string.Empty,
        };
    }

    public static Color? GetBloodLevelSeverityColor(float bloodLevel)
    {
        if (float.IsNaN(bloodLevel))
            return null;

        var clampedPercent = Math.Clamp(bloodLevel, 0.5f, 1f);
        var scaledPercent = (clampedPercent - 0.5f) / 0.5f;
        return Color.InterpolateBetween(SeverityDangerColor, SeveritySafeColor, scaledPercent);
    }

    public static string GetDamageSeveritySuffix(float damageAmount)
    {
        return damageAmount switch
        {
            > 200f => "!!!!",
            > 100f => "!!!",
            > 75f => "!!",
            > 50f => "!",
            _ => string.Empty,
        };
    }

    public static Color GetDamageSeverityColor(float damageAmount)
    {
        var clampedDamage = Math.Clamp(damageAmount, 0f, 100f);
        var damagePercent = clampedDamage / 100f;
        return Color.InterpolateBetween(SeveritySafeColor, SeverityDangerColor, damagePercent);
    }

    public static int GetDamageGroupSortKey(string groupId)
    {
        var index = Array.IndexOf(DamageGroupOrder, groupId);
        return index == -1 ? DamageGroupOrder.Length : index;
    }

    /// <summary>
    /// Returns the sort order index for a reagent group.
    /// Unknown groups are placed after all known groups by returning <see cref="ReagentGroupOrder"/> length.
    /// </summary>
    public static int GetReagentGroupSortKey(string groupId)
    {
        var index = Array.IndexOf(ReagentGroupOrder, groupId);
        return index == -1 ? ReagentGroupOrder.Length : index;
    }

    /// <summary>
    /// Returns the OD warning markup string colored with <see cref="SeverityDangerColor"/>.
    /// </summary>
    public static string FormatOdWarningMarkup()
    {
        return WrapMarkupWithColor(" !!", SeverityDangerColor);
    }

    public static string WrapMarkupWithColor(string markup, Color? color)
    {
        return color is { } value
            ? $"[color={value.ToHex()}]{markup}[/color]"
            : markup;
    }

    public static string WrapTextWithColorMarkup(string text, Color? color)
    {
        return WrapMarkupWithColor(FormattedMessage.EscapeText(text), color);
    }

    public static string GetStatusText(MobState mobState)
    {
        return mobState switch
        {
            MobState.Alive => Loc.GetString("health-analyzer-window-entity-alive-text"),
            MobState.Critical => Loc.GetString("health-analyzer-window-entity-critical-text"),
            MobState.Dead => Loc.GetString("health-analyzer-window-entity-dead-text"),
            _ => Loc.GetString("health-analyzer-window-entity-unknown-text"),
        };
    }
}

