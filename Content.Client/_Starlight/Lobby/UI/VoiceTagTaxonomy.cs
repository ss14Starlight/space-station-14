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
        _cachedTags = voices.SelectMany(GetMappedTags).Distinct().OrderBy(t => t).ToList();
    }

    public List<string> CachedTags => _cachedTags;

    public List<string> GetMappedTags(VoicePrototype v)
    {
        var list = new List<string>();
        foreach (var protoId in v.Tags)
        {
            var tag = protoId.Id.Trim().ToLowerInvariant();
            if (_config.ExcludedTags.Contains(tag))
                continue;

            if (_presentingTags.Contains(tag))
            {
                if (!list.Contains(tag))
                    list.Add(tag);
                continue;
            }

            // Look up parent hierarchies
            if (_prototypeManager.TryIndex<VoiceTagPrototype>(tag, out var tagProto))
            {
                foreach (var parentProtoId in tagProto.Parents)
                {
                    var parent = parentProtoId.Id;
                    if (_presentingTags.Contains(parent))
                    {
                        if (!list.Contains(parent))
                            list.Add(parent);
                    }
                }
            }
        }
        return list;
    }

    private HashSet<string> ComputePresentingTags(List<VoicePrototype> voices)
    {
        var rawCounts = new Dictionary<string, int>();
        foreach (var v in voices)
        {
            var processed = new List<string>();
            foreach (var protoId in v.Tags)
            {
                var tag = protoId.Id.Trim().ToLowerInvariant();
                if (_config.ExcludedTags.Contains(tag))
                    continue;

                if (!processed.Contains(tag))
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
