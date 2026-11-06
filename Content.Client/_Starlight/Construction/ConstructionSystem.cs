using System.Diagnostics.CodeAnalysis;
using Content.Shared.Construction.Prototypes;
using Robust.Shared.Configuration;
using Robust.Shared.Map;

// ReSharper disable once CheckNamespace
namespace Content.Client.Construction;

public sealed partial class ConstructionSystem
{
    [Dependency] private IConfigurationManager _configurationManager = default!;

    /// <summary>
    ///     Attempts to create a construction ghost for the specified user.
    /// </summary>
    /// <param name="prototype">The construction prototype for the ghost.</param>
    /// <param name="loc">The location where the ghost should be created.</param>
    /// <param name="dir">The direction the ghost should face.</param>
    /// <param name="user">The entity whose range and construction conditions are validated.</param>
    /// <param name="ghost">The created ghost, or <see langword="null"/> if creation failed.</param>
    /// <param name="showPopup">Whether to show a popup explaining a failed construction condition.</param>
    /// <returns>True if a construction ghost was created; otherwise, false.</returns>
    public bool TrySpawnGhost(
        ConstructionPrototype prototype,
        EntityCoordinates loc,
        Direction dir,
        EntityUid user,
        [NotNullWhen(true)] out EntityUid? ghost,
        bool showPopup = true)
    {
        ghost = null;
        if (!user.IsValid())
            return false;

        // Prevent multiple identical ghosts in the same spot and orientation.
        foreach (var (id, existingGhost) in _ghosts)
        {
            var ghostComp = Comp<ConstructionGhostComponent>(existingGhost);
            var xform = Transform(existingGhost);
            if (ghostComp.Prototype == prototype
                && xform.Coordinates.Equals(loc)
                && xform.LocalRotation.Equals(dir.ToAngle()))
                return false;
        }

        return TrySpawnGhostForUser(prototype, loc, dir, user, out ghost, showPopup);
    }

}
