using Content.Shared.Dataset;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
﻿using System.Linq;

namespace Content.Shared._Starlight.Thaven;

[Virtual, DataDefinition]
[Serializable, NetSerializable]
public partial class ThavenMood
{
    /// <summary>
    /// The prototype this mood was created from.
    /// Used for managing conflicts, this does not apply to admin-made moods.
    /// </summary>
    [DataField]
    public ProtoId<ThavenMoodPrototype>? ProtoId;

    /// <summary>
    /// A locale string of the mood name. Gets passed to
    /// <see cref="Loc.GetString"/> with <see cref="MoodVars"/>.
    /// </summary>
    [DataField(required: true)]
    public LocId MoodName;

    /// <summary>
    /// A locale string of the mood description. Gets passed to
    /// <see cref="Loc.GetString"/> with <see cref="MoodVars"/>.
    /// </summary>
    [DataField(required: true)]
    public LocId MoodDesc;

    /// <summary>
    /// A list of mood IDs that this mood will conflict with.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<ThavenMoodPrototype>> Conflicts = new();

    /// <summary>
    /// Additional localized words for the <see cref="MoodDesc"/>, for things like random
    /// verbs and nouns.
    /// Gets randomly picked from datasets in <see cref="MoodVarDatasets"/>.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public Dictionary<string, string> MoodVars = new();

    public (string, object)[] GetLocArgs()
    {
        IoCManager.Resolve<IPrototypeManager>().TryIndex(ProtoId, out var proto);
        var loc = IoCManager.Resolve<ILocalizationManager>();
        return MoodVars.Select(v => (v.Key, (object)LocalizeMoodVar(loc, proto, v.Key, v.Value))).ToArray();
    }

    private static string LocalizeMoodVar(ILocalizationManager loc, ThavenMoodPrototype? proto, string name, string value)
    {
        if (proto is null || !proto.MoodVarDatasets.TryGetValue(name, out var dataset))
            return value;

        return loc.TryGetString($"thaven-mood-var-{ToKebab(dataset.Id)}-{ToKebab(value)}", out var localized)
            ? localized
            : value;
    }

    private static string ToKebab(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsUpper(c) && i > 0 && char.IsLower(text[i - 1]))
                sb.Append('-');
            var next = char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-';
            if (next == '-' && sb.Length > 0 && sb[^1] == '-')
                continue;
            sb.Append(next);
        }
        return sb.ToString().Trim('-');
    }

    public string GetLocName()
    {
        return Loc.GetString(MoodName, GetLocArgs());
    }

    public string GetLocDesc()
    {
        return Loc.GetString(MoodDesc, GetLocArgs());
    }

    /// <summary>
    /// Create a shallow clone of this mood.
    /// Used to prevent modifying prototypes.
    /// </summary>
    public ThavenMood ShallowClone()
    {
        return new ThavenMood()
        {
            ProtoId = ProtoId,
            MoodName = MoodName,
            MoodDesc = MoodDesc,
            Conflicts = Conflicts,
            MoodVars = MoodVars
        };
    }
}

[Prototype]
public sealed partial class ThavenMoodPrototype : ThavenMood, IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Extra mood variables that will be randomly chosen and provided
    /// for localizing <see cref="ThavenMood.MoodName"/> and <see cref="ThavenMood.MoodDesc"/>.
    /// </summary>
    [DataField("moodVars")]
    public Dictionary<string, ProtoId<DatasetPrototype>> MoodVarDatasets = new();

    /// <summary>
    /// If false, prevents the same variable from being rolled twice when rolling
    /// mood variables for this mood. Does not prevent the same mood variable
    /// from being present in other moods.
    /// </summary>
    [DataField]
    public bool AllowDuplicateMoodVars = false;

    public ThavenMoodPrototype()
    {
        ProtoId = ID;
    }
}
