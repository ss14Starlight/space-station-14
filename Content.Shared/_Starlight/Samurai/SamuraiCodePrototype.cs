using Content.Shared.Dataset;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
﻿using System.Linq;

namespace Content.Shared._Starlight.Samurai;

[Virtual, DataDefinition]
[Serializable, NetSerializable]
public partial class SamuraiCode
{
    /// <summary>
    /// The prototype this code was created from.
    /// Used for managing conflicts, this does not apply to admin-made codes.
    /// </summary>
    [DataField]
    public ProtoId<SamuraiCodePrototype>? ProtoId;

    /// <summary>
    /// A locale string of the code name. Gets passed to
    /// <see cref="Loc.GetString"/> with <see cref="CodeVars"/>.
    /// </summary>
    [DataField(required: true)]
    public LocId CodeName;

    /// <summary>
    /// A locale string of the code description. Gets passed to
    /// <see cref="Loc.GetString"/> with <see cref="CodeVars"/>.
    /// </summary>
    [DataField(required: true)]
    public LocId CodeDesc;

    /// <summary>
    /// A list of code IDs that this code will conflict with.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<SamuraiCodePrototype>> Conflicts = new();

    /// <summary>
    /// Additional localized words for the <see cref="CodeDesc"/>, for things like random
    /// verbs and nouns.
    /// Gets randomly picked from datasets in <see cref="CodeVarDatasets"/>.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public Dictionary<string, string> CodeVars = new();

    public (string, object)[] GetLocArgs()
    {
        return CodeVars.Select(v => (v.Key, (object)v.Value)).ToArray();
    }

    public string GetLocName()
    {
        return Loc.GetString(CodeName, GetLocArgs());
    }

    public string GetLocDesc()
    {
        return Loc.GetString(CodeDesc, GetLocArgs());
    }

    /// <summary>
    /// Create a shallow clone of this code.
    /// Used to prevent modifying prototypes.
    /// </summary>
    public SamuraiCode ShallowClone()
    {
        return new SamuraiCode()
        {
            ProtoId = ProtoId,
            CodeName = CodeName,
            CodeDesc = CodeDesc,
            Conflicts = Conflicts,
            CodeVars = CodeVars
        };
    }
}

[Prototype]
public sealed partial class SamuraiCodePrototype : SamuraiCode, IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Extra code variables that will be randomly chosen and provided
    /// for localizing <see cref="SamuraiCode.CodeName"/> and <see cref="SamuraiCode.CodeDesc"/>.
    /// </summary>
    [DataField("codeVars")]
    public Dictionary<string, ProtoId<DatasetPrototype>> CodeVarDatasets = new();

    /// <summary>
    /// If false, prevents the same variable from being rolled twice when rolling
    /// code variables for this code. Does not prevent the same code variable
    /// from being present in other codes.
    /// </summary>
    [DataField]
    public bool AllowDuplicateCodeVars = false;

    public SamuraiCodePrototype()
    {
        ProtoId = ID;
    }
}
