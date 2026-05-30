// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Text.Json;
using System.Text.Json.Serialization;
using Content.Server.Humanoid.Markings.Extensions;
using Content.Shared._Starlight.Body.Editor;
using Content.Shared._Starlight.Body.Preferences;

namespace Content.Server._Starlight.Body.Editor;

/// <summary>
/// Translates <see cref="BodyProfile"/> to/from a flat JSON blob for DB storage.
/// </summary>
public static class BodyProfileSerializer
{
    private static readonly JsonSerializerOptions _options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string? Serialize(BodyProfile? profile, ISawmill log)
    {
        if (profile == null)
            return null;
        try
        {
            var dto = ToDto(profile);
            return JsonSerializer.Serialize(dto, _options);
        }
        catch (Exception e)
        {
            log.Error($"Failed to serialize BodyProfile: {e}");
            return null;
        }
    }

    public static BodyProfile? Deserialize(string? serialized, ISawmill log)
    {
        if (string.IsNullOrWhiteSpace(serialized))
            return null;
        try
        {
            var dto = JsonSerializer.Deserialize<ProfileDto>(serialized, _options);
            return dto == null ? null : FromDto(dto, log);
        }
        catch (Exception e)
        {
            log.Error($"Failed to deserialize BodyProfile: {e}");
            return null;
        }
    }

    private sealed class ProfileDto
    {
        public PartDto Root { get; set; } = new();
        public Dictionary<string, string> Parameters { get; set; } = [];
    }

    private sealed class PartDto
    {
        public List<string> Markings { get; set; } = [];

        public string? BodyPartOverride { get; set; }

        public Dictionary<string, PartDto> Children { get; set; } = [];
    }

    private static ProfileDto ToDto(BodyProfile profile)
    {
        var dto = new ProfileDto { Root = ToDto(profile.Root) };
        foreach (var (key, value) in profile.Parameters)
            dto.Parameters[key.Id] = value.ToHex();
        return dto;
    }

    private static PartDto ToDto(BodyPartPreference part)
    {
        var dto = new PartDto
        {
            BodyPartOverride = part.BodyPartOverride?.Id,
        };
        foreach (var marking in part.Markings)
            dto.Markings.Add(marking.ToDBString());
        foreach (var (socket, child) in part.Children)
            dto.Children[socket] = ToDto(child);
        return dto;
    }

    private static BodyProfile FromDto(ProfileDto dto, ISawmill log)
    {
        var profile = new BodyProfile { Root = FromDto(dto.Root) };
        foreach (var (key, value) in dto.Parameters)
        {
            if (string.IsNullOrEmpty(value))
                continue;
            try
            {
                profile.Parameters[key] = Color.FromHex(value);
            }
            catch (Exception e)
            {
                log.Warning($"Failed to parse color '{value}' for parameter '{key}': {e.Message}");
            }
        }
        return profile;
    }

    private static BodyPartPreference FromDto(PartDto dto)
    {
        var part = new BodyPartPreference
        {
            BodyPartOverride = string.IsNullOrEmpty(dto.BodyPartOverride) ? null : dto.BodyPartOverride,
        };
        foreach (var markingStr in dto.Markings)
        {
            var parsed = MarkingExtensions.ParseFromDbString(markingStr);
            if (parsed != null)
                part.Markings.Add(parsed);
        }
        foreach (var (socket, childDto) in dto.Children)
            part.Children[socket] = FromDto(childDto);
        return part;
    }
}
