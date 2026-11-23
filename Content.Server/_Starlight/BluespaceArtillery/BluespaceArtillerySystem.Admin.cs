using Content.Server.Chat.Managers;
using Content.Server.Shuttles.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Map;

namespace Content.Server._Starlight.BluespaceArtillery;

public sealed partial class BluespaceArtillerySystem
{
    [Dependency] private IChatManager _chat = default!;

    /// <summary>
    /// True when the position is on protected grid, map or grid of protected station.
    /// </summary>
    public bool IsProtected(MapCoordinates coordinates)
    {
        if (coordinates.MapId == MapId.Nullspace)
            return false;

        if (_map.TryGetMap(coordinates.MapId, out var map) && IsMapProtected(map.Value))
            return true;

        return _map.TryFindGridAt(coordinates, out var grid, out _) && IsGridProtected(grid);
    }

    /// <summary>
    /// True when the entity stands on protected grid, map or station.
    /// </summary>
    public bool IsProtected(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.MapUid is { } map && IsMapProtected(map))
            return true;

        return xform.GridUid is { } grid && IsGridProtected(grid);
    }

    private bool IsMapProtected(EntityUid map)
    {
        if (HasComp<BluespaceArtilleryProtectedComponent>(map))
            return true;

        var centcomms = AllEntityQuery<StationCentcommComponent>();
        while (centcomms.MoveNext(out var centcomm))
        {
            if (centcomm.MapEntity == map)
                return true;
        }

        return false;
    }

    private bool IsGridProtected(EntityUid grid)
    {
        if (HasComp<BluespaceArtilleryProtectedComponent>(grid))
            return true;

        return TryComp<StationMemberComponent>(grid, out var member)
            && HasComp<BluespaceArtilleryProtectedComponent>(member.Station);
    }

    /// <summary>
    /// Warns admins once when a cannon is fully charged and has a target picked.
    /// </summary>
    private void CheckReadyAlert(Entity<BluespaceArtilleryConsoleComponent> console, BluespaceArtilleryComponent cannon)
    {
        if (!IsCharged(cannon) || console.Comp.Target is not { } target || console.Comp.AlertedTarget == target)
            return;

        if (!Exists(target))
        {
            console.Comp.Target = null;
            return;
        }

        console.Comp.AlertedTarget = target;
        _chat.SendAdminAlert(Loc.GetString("bsa-admin-alert-ready",
            ("operator", OperatorName(console.Comp.Operator)),
            ("target", ToPrettyString(target)),
            ("location", DescribeLocation(_transform.GetMapCoordinates(target)))));
        SendAdminLinks(console.Comp.Cannon, target, null, console.Comp.Operator);
    }

    private void SendFireAlert(EntityUid cannon, EntityUid target, MapCoordinates coordinates, EntityUid? user)
    {
        _chat.SendAdminAlert(Loc.GetString("bsa-admin-alert-fired",
            ("operator", OperatorName(user)),
            ("target", ToPrettyString(target)),
            ("location", DescribeLocation(coordinates))));
        SendAdminLinks(cannon, target, coordinates, user);
    }

    private void SendAdminLinks(EntityUid? cannon, EntityUid target, MapCoordinates? coordinates, EntityUid? user)
    {
        var links = new List<string>();

        if (cannon != null && Exists(cannon))
            links.Add(Link(Loc.GetString("bsa-admin-link-cannon"), $"tpto {GetNetEntity(cannon.Value)}"));

        if (Exists(target))
            links.Add(Link(Loc.GetString("bsa-admin-link-target"), $"tpto {GetNetEntity(target)}"));

        if (coordinates is { } coords && coords.MapId != MapId.Nullspace)
        {
            links.Add(Link(Loc.GetString("bsa-admin-link-impact"),
                FormattableString.Invariant($"tp {coords.X:0.##} {coords.Y:0.##} {coords.MapId}")));
        }

        if (user != null && Exists(user))
            links.Add(Link(Loc.GetString("bsa-admin-link-operator"), $"tpto {GetNetEntity(user.Value)}"));

        if (links.Count > 0)
            _chat.SendAdminAlertNoFormatOrEscape($"[color=#ff5555]{Loc.GetString("bsa-admin-links")}[/color] {string.Join(" | ", links)}");
    }

    private static string Link(string label, string command)
        => $"[cmdlink=\"{label}\" command=\"{command}\"/]";

    private string OperatorName(EntityUid? user)
        => user != null && Exists(user) ? ToPrettyString(user.Value).ToString() : Loc.GetString("bsa-admin-alert-unknown");

    private string DescribeLocation(MapCoordinates coordinates)
    {
        var place = _map.TryFindGridAt(coordinates, out var grid, out _)
            ? Name(grid)
            : _map.TryGetMap(coordinates.MapId, out var map) ? Name(map.Value) : coordinates.MapId.ToString();

        return Loc.GetString("bsa-admin-alert-location",
            ("place", place),
            ("x", MathF.Round(coordinates.X, 1)),
            ("y", MathF.Round(coordinates.Y, 1)),
            ("map", coordinates.MapId.ToString()));
    }
}
