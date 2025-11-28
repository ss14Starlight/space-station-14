using System.Text;
using System.Text.RegularExpressions;

namespace Content.Shared._Starlight.Administration;

/// <summary>
/// Shared utility for AutoMod note formatting and coloring
/// </summary>
public static class AutoModFormatting
{
    private static readonly Regex DecayLevelRegex = new(@"\[Decays by: (\d+) on", RegexOptions.Compiled);
    
    private static readonly Dictionary<int, string> LevelColors = new()
    {
        { 1, "#00ff00" }, { 2, "#ffff00" }, { 3, "#ff8800" }
    };

    private static readonly Dictionary<string, string> ActionColors = new(StringComparer.OrdinalIgnoreCase)
    {
        { "none", "#00ff00" }, { "warn", "#ffff00" }, { "kick", "#ff8800" }, { "ban", "#ff0000" }
    };

    /// <summary>
    /// Checks if a note message is an AutoMod violation
    /// </summary>
    public static bool IsAutoModNote(string message)
    {
        return message.Contains("Metadata:") && message.Contains("\u2554\u2550\u2550 AUTOMOD VIOLATION");
    }

    /// <summary>
    /// Checks if a note contains AutoMod ID metadata
    /// </summary>
    public static bool HasAutoModId(string message)
    {
        return message.Contains("AUTOMOD_ID:");
    }

    /// <summary>
    /// Extracts the decay level from an AutoMod note message
    /// </summary>
    public static int GetDecayLevel(string message)
    {
        var match = DecayLevelRegex.Match(message);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var level))
            return level;
        return 1;
    }

    /// <summary>
    /// Formats a time span for display
    /// </summary>
    public static string FormatTimeRemaining(TimeSpan timeRemaining)
    {
        if (timeRemaining.TotalHours >= 1)
            return $"{timeRemaining.TotalHours:F1}h";
        if (timeRemaining.TotalMinutes >= 1)
            return $"{timeRemaining.TotalMinutes:F0}m";
        return $"{timeRemaining.TotalSeconds:F0}s";
    }

    /// <summary>
    /// Applies BBCode coloring to AutoMod violation notes
    /// </summary>
    public static string ApplyColors(string plainText)
    {
        var result = new StringBuilder();
        var lines = plainText.Split('\n');
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();
            
            if (trimmed.StartsWith("╔══ AUTOMOD VIOLATION ══╗"))
                result.AppendLine($"[bold][color=#ff4444]{line}[/color][/bold]");
            else if (trimmed.StartsWith("Rule:"))
            {
                var parts = line.Split(':', 2);
                result.AppendLine(parts.Length == 2 
                    ? $"[color=#00ddff]Rule:[/color] [bold][color=#ffaa00]{parts[1].Trim()}[/color][/bold]"
                    : line);
            }
            else if (trimmed.StartsWith("Offense Level:"))
            {
                var parts = trimmed.Split(':', 2);
                if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out var level))
                {
                    var color = LevelColors.TryGetValue(level, out var c) ? c : "#ff0000";
                    result.AppendLine($"[color=#00ddff]Offense Level:[/color] [bold][color={color}]{level}[/color][/bold]");
                }
                else
                    result.AppendLine(line);
            }
            else if (trimmed.StartsWith("Action Taken:"))
            {
                var parts = trimmed.Split(':', 2);
                if (parts.Length == 2)
                {
                    var action = parts[1].Trim();
                    var color = ActionColors.TryGetValue(action, out var c) ? c : "#88aaff";
                    result.AppendLine($"[color=#00ddff]Action Taken:[/color] [bold][color={color}]{action}[/color][/bold]");
                }
                else
                    result.AppendLine(line);
            }
            else if (trimmed.StartsWith("Channel:") || trimmed.StartsWith("Category:"))
            {
                var parts = trimmed.Split(':', 2);
                result.AppendLine(parts.Length == 2 
                    ? $"[color=#00ddff]{parts[0]}:[/color] [color=#ffffff]{parts[1].Trim()}[/color]"
                    : line);
            }
            else if (trimmed.StartsWith("Violating Message:"))
            {
                var parts = trimmed.Split(':', 2);
                result.AppendLine(parts.Length == 2 
                    ? $"[bold][color=#00ddff]Violating Message:[/color][/bold] [color=#ffcccc]{parts[1].Trim()}[/color]"
                    : line);
            }
            else if (trimmed.StartsWith("──"))
                result.AppendLine($"[color=#88aaff]{line}[/color]");
            else if (trimmed.StartsWith("#"))
            {
                var isDecayed = trimmed.Contains("[DECAYED]");
                var baseColor = isDecayed ? "#555555" : "#ffffff";
                var parts = trimmed.Split('|');
                
                for (int j = 0; j < parts.Length; j++)
                {
                    var part = parts[j].Trim();
                    
                    if (j == 0 && part.StartsWith("#"))
                        result.Append($"[color=#88aaff]{part}[/color]");
                    else if (part.StartsWith("Level ") && int.TryParse(part.Replace("Level ", ""), out var level))
                    {
                        var color = isDecayed ? "#555555" : (LevelColors.TryGetValue(level, out var c) ? c : "#ff0000");
                        result.Append($"[color={color}]Level {level}[/color]");
                    }
                    else if (ActionColors.ContainsKey(part))
                    {
                        var color = isDecayed ? "#555555" : ActionColors[part];
                        result.Append($"[bold][color={color}]{part}[/color][/bold]");
                    }
                    else if (part.StartsWith("Decay:"))
                        result.Append($"[color=#aaaaaa]{part}[/color]");
                    else if (part == "[ACTIVE]")
                        result.Append("[bold][color=#00ff00][ACTIVE][/color][/bold]");
                    else if (part == "[DECAYED]")
                        result.Append("[bold][color=#ff4444][DECAYED][/color][/bold]");
                    else
                        result.Append($"[color={baseColor}]{part}[/color]");
                    
                    if (j < parts.Length - 1)
                        result.Append(" [color=#666666]|[/color] ");
                }
                result.AppendLine();
            }
            else if (line.StartsWith("  Message:") || trimmed.StartsWith("Message:"))
            {
                var indent = new string(' ', line.Length - trimmed.Length);
                var parts = trimmed.Split(':', 2);
                result.AppendLine(parts.Length == 2 
                    ? $"{indent}[color=#cccccc]Message:[/color] [color=#ffcccc]{parts[1].Trim()}[/color]"
                    : line);
            }
            else if (trimmed.StartsWith("╚"))
                result.AppendLine($"[color=#ff4444]{line}[/color]");
            else if (trimmed.StartsWith("Metadata:"))
                result.AppendLine($"[color=#555555][size=8]{line}[/size][/color]");
            else
                result.AppendLine(line);
        }
        
        return result.ToString();
    }

    /// <summary>
    /// Gets the color for a specific offense level
    /// </summary>
    public static string GetLevelColor(int level)
    {
        return LevelColors.TryGetValue(level, out var color) ? color : "#ff0000";
    }

    /// <summary>
    /// Gets the color for a specific action type
    /// </summary>
    public static string GetActionColor(string action)
    {
        return ActionColors.TryGetValue(action, out var color) ? color : "#88aaff";
    }
}
