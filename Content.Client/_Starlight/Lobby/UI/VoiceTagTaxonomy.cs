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

    private readonly HashSet<VoiceTag> _excludedTags;
    private readonly HashSet<VoiceTag> _explicitlyIncludedTags;

    private readonly HashSet<VoiceTag> _presentingTags;
    private readonly List<VoiceTag> _cachedTags;

    private const int MaxRecursionDepth = 10;
    private static readonly ProtoId<VoiceTagFilterConfigPrototype> DefaultConfigId = "default";

    public VoiceTagTaxonomy(List<VoicePrototype> voices, IPrototypeManager prototypeManager)
    {
        _prototypeManager = prototypeManager;
        _config = prototypeManager.Index(DefaultConfigId);

        _excludedTags = _config.ExcludedTags.Select(t => new VoiceTag(t.Id)).ToHashSet();
        _explicitlyIncludedTags = _config.ExplicitlyIncludedTags.Select(t => new VoiceTag(t.Id)).ToHashSet();

        _presentingTags = ComputePresentingTags(voices);
        _cachedTags = voices.SelectMany(ResolvePresentingTags).Distinct().OrderBy(t => t.Value).ToList();
    }

    public List<VoiceTag> CachedTags => _cachedTags;

    public HashSet<VoiceTag> ResolvePresentingTags(VoicePrototype v)
    {
        var result = new HashSet<VoiceTag>();
        var visited = new HashSet<VoiceTag>();

        foreach (var protoId in v.Tags)
        {
            ResolveAncestors(new VoiceTag(protoId.Id), result, visited);
        }

        return result;
    }

    private void ResolveAncestors(VoiceTag tag, HashSet<VoiceTag> resolved, HashSet<VoiceTag> visited, int depth = 0)
    {
        // Safety guard against infinite recursion from malformed/cyclic tag prototype configurations
        if (depth > MaxRecursionDepth)
            return;

        if (!visited.Add(tag))
            return;

        if (_excludedTags.Contains(tag))
            return;

        if (_presentingTags.Contains(tag))
        {
            resolved.Add(tag);
        }

        if (_prototypeManager.TryIndex<VoiceTagPrototype>(tag, out var tagProto))
        {
            foreach (var parentProtoId in tagProto.Parents)
            {
                ResolveAncestors(new VoiceTag(parentProtoId.Id), resolved, visited, depth + 1);
            }
        }
    }

    private HashSet<VoiceTag> ComputePresentingTags(List<VoicePrototype> voices)
    {
        var rawCounts = new Dictionary<VoiceTag, int>();
        foreach (var v in voices)
        {
            var processed = new HashSet<VoiceTag>();
            foreach (var protoId in v.Tags)
            {
                var tag = new VoiceTag(protoId.Id);
                if (_excludedTags.Contains(tag))
                    continue;

                processed.Add(tag);
            }
            foreach (var tag in processed)
            {
                rawCounts[tag] = rawCounts.GetValueOrDefault(tag) + 1;
            }
        }

        var presenting = new HashSet<VoiceTag>();

        // Always include explicitly configured tags
        foreach (var protoId in _config.ExplicitlyIncludedTags)
        {
            presenting.Add(new VoiceTag(protoId.Id));
        }

        // Fill remaining slots with highest frequency tags
        var candidates = rawCounts.Keys
            .Where(t => !_explicitlyIncludedTags.Contains(t) && !_excludedTags.Contains(t))
            .OrderByDescending(t => rawCounts[t])
            .ToList();

        int slotsRemaining = Math.Max(0, _config.MaxPresentedTags - presenting.Count);
        for (int i = 0; i < Math.Min(slotsRemaining, candidates.Count); i++)
        {
            presenting.Add(candidates[i]);
        }

        return presenting;
    }
}
