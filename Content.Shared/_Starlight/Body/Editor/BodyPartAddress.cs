// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Body.Editor;

/// <summary>
/// Address of a body part inside the socket hierarchy, optionally scoped to a marking set on that part.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct BodyPartAddress
{
    public static readonly BodyPartAddress Root = default;

    public string Path { get; }

    public string? MarkingSet { get; }

    public BodyPartAddress(string? path, string? markingSet = null)
    {
        Path = string.IsNullOrEmpty(path) ? "/" : path;
        MarkingSet = string.IsNullOrEmpty(markingSet) ? null : markingSet;
    }

    public bool IsRoot => Path == "/" && MarkingSet == null;

    public bool HasMarkingSet => MarkingSet != null;

    public IEnumerable<string> Segments
    {
        get
        {
            if (Path == "/")
                yield break;

            // skip leading '/'.
            var start = 1;
            for (var i = 1; i <= Path.Length; i++)
            {
                if (i == Path.Length || Path[i] == '/')
                {
                    if (i > start)
                        yield return Path[start..i];
                    start = i + 1;
                }
            }
        }
    }

    public BodyPartAddress Append(string socketId)
    {
        if (string.IsNullOrEmpty(socketId) || socketId.Contains('/'))
            throw new ArgumentException("Socket id must be non-empty and must not contain '/'.", nameof(socketId));

        if (MarkingSet != null)
            throw new InvalidOperationException("Cannot append a socket to a marking-set-scoped address.");

        var basePath = Path == "/" ? string.Empty : Path;
        return new BodyPartAddress(basePath + "/" + socketId);
    }

    public BodyPartAddress WithMarkingSet(string? markingSet) => new(Path, markingSet);

    public BodyPartAddress PartOnly() => MarkingSet == null ? this : new BodyPartAddress(Path);

    public BodyPartAddress? Parent
    {
        get
        {
            // A marking-set-scoped address falls back to the bare part.
            if (MarkingSet != null)
                return new BodyPartAddress(Path);

            if (Path == "/")
                return null;

            var lastSlash = Path.LastIndexOf('/');
            return lastSlash <= 0 ? Root : new BodyPartAddress(Path[..lastSlash]);
        }
    }

    public static BodyPartAddress Parse(string? value)
    {
        if (string.IsNullOrEmpty(value) || value == "/")
            return Root;

        var isPart = value.Length > 1 && value[^1] == '/';
        if (isPart)
            value = value[..^1];

        var trimmed = value.StartsWith('/') ? value[1..] : value;
        var parts = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return Root;

        if (isPart)
            return new BodyPartAddress("/" + string.Join('/', parts));

        var markingSet = parts[^1];
        var path = parts.Length == 1 ? "/" : "/" + string.Join('/', parts, 0, parts.Length - 1);
        return new BodyPartAddress(path, markingSet);
    }

    public override string ToString() => MarkingSet == null
        ? (Path == "/" ? "/" : Path + "/")
        : (Path == "/" ? "/" : Path + "/") + MarkingSet;
}
