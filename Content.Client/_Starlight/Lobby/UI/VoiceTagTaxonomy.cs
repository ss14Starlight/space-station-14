using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared._Starlight.TextToSpeech;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Lobby.UI;

public sealed class VoiceTagTaxonomy
{
    private readonly IPrototypeManager _prototypeManager;
    private readonly VoiceTagFilterConfigPrototype _config;

    private readonly HashSet<string> _presentingTags;
    private readonly List<string> _cachedTags;

    private static readonly ProtoId<VoiceTagFilterConfigPrototype> DefaultConfigId = "default";

    public VoiceTagTaxonomy(List<VoicePrototype> voices, IPrototypeManager prototypeManager)
    {
        _prototypeManager = prototypeManager;
        _config = prototypeManager.Index(DefaultConfigId);

        _presentingTags = ComputePresentingTags(voices);
        _cachedTags = voices.SelectMany(ResolvePresentingTags).Distinct().OrderBy(t => t).ToList();
    }

    public List<string> CachedTags => _cachedTags;

    public HashSet<string> ResolvePresentingTags(VoicePrototype v)
    {
        var result = new HashSet<string>();
        var visited = new HashSet<string>();

        foreach (var protoId in v.Tags)
        {
            ResolveAncestors(protoId.Id.Trim().ToLowerInvariant(), result, visited);
        }

        return result;
    }

    private void ResolveAncestors(string tag, HashSet<string> resolved, HashSet<string> visited, int depth = 0)
    {
        // Safety guard against infinite recursion from malformed/cyclic tag prototype configurations
        if (depth > 10)
            return;

        if (!visited.Add(tag))
            return;

        if (_config.ExcludedTags.Contains(tag))
            return;

        if (_presentingTags.Contains(tag))
        {
            resolved.Add(tag);
        }

        if (_prototypeManager.TryIndex<VoiceTagPrototype>(tag, out var tagProto))
        {
            foreach (var parentProtoId in tagProto.Parents)
            {
                ResolveAncestors(parentProtoId.Id.Trim().ToLowerInvariant(), resolved, visited, depth + 1);
            }
        }
    }

    private HashSet<string> ComputePresentingTags(List<VoicePrototype> voices)
    {
        var rawCounts = new Dictionary<string, int>();
        foreach (var v in voices)
        {
            var processed = new HashSet<string>();
            foreach (var protoId in v.Tags)
            {
                var tag = protoId.Id.Trim().ToLowerInvariant();
                if (_config.ExcludedTags.Contains(tag))
                    continue;

                processed.Add(tag);
            }
            foreach (var tag in processed)
            {
                rawCounts[tag] = rawCounts.GetValueOrDefault(tag) + 1;
            }
        }

        var presenting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Always include explicitly configured tags
        foreach (var protoId in _config.ExplicitlyIncludedTags)
        {
            presenting.Add(protoId.Id);
        }

        // Fill remaining slots with highest frequency tags
        var candidates = rawCounts.Keys
            .Where(t => !_config.ExplicitlyIncludedTags.Contains(t) && !_config.ExcludedTags.Contains(t))
            .OrderByDescending(t => rawCounts[t])
            .ToList();

        int slotsRemaining = Math.Max(0, _config.MaxPresentedTags - presenting.Count);
        for (int i = 0; i < Math.Min(slotsRemaining, candidates.Count); i++)
        {
            presenting.Add(candidates[i]);
        }

        return presenting;
    }

    public string FormatTag(string tag)
    {
        // Check for custom localization key first
        var locKey = $"tts-tag-{tag.Replace(" ", "-")}";
        if (Loc.TryGetString(locKey, out var localized))
            return localized;

        // Fallback to title casing
        if (string.IsNullOrEmpty(tag))
            return tag;

        var chars = tag.ToCharArray();
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
}
