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

    private string _pathOrRoot => string.IsNullOrEmpty(Path) ? "/" : Path;

    public BodyPartAddress(string? path, string? markingSet = null)
    {
        Path = string.IsNullOrEmpty(path) ? "/" : path;
        MarkingSet = string.IsNullOrEmpty(markingSet) ? null : markingSet;
    }

    public bool IsRoot => _pathOrRoot == "/" && MarkingSet == null;

    public bool HasMarkingSet => MarkingSet != null;

    public IEnumerable<string> Segments
    {
        get
        {
            var path = _pathOrRoot;
            if (path == "/")
                yield break;

            // skip leading '/'.
            var start = 1;
            for (var i = 1; i <= path.Length; i++)
            {
                if (i == path.Length || path[i] == '/')
                {
                    if (i > start)
                        yield return path[start..i];
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

        var path = _pathOrRoot;
        var basePath = path == "/" ? string.Empty : path;
        return new BodyPartAddress(basePath + "/" + socketId);
    }

    public BodyPartAddress WithMarkingSet(string? markingSet) => new(_pathOrRoot, markingSet);

    public BodyPartAddress PartOnly() => MarkingSet == null ? this : new BodyPartAddress(_pathOrRoot);

    public BodyPartAddress? Parent
    {
        get
        {
            // A marking-set-scoped address falls back to the bare part.
            if (MarkingSet != null)
                return new BodyPartAddress(_pathOrRoot);

            var path = _pathOrRoot;
            if (path == "/")
                return null;

            var lastSlash = path.LastIndexOf('/');
            return lastSlash <= 0 ? Root : new BodyPartAddress(path[..lastSlash]);
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
        ? (_pathOrRoot == "/" ? "/" : _pathOrRoot + "/")
        : (_pathOrRoot == "/" ? "/" : _pathOrRoot + "/") + MarkingSet;
}
