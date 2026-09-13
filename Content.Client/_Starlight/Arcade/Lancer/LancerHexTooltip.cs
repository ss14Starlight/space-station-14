using System.Linq;
using Content.Shared._Starlight.Arcade.Lancer;

namespace Content.Client._Starlight.Arcade.Lancer;

/// <summary>
/// Builds localized hover text for hex cells on the Lancer board.
/// </summary>
public static class LancerHexTooltip
{
    public static string BuildIdle() => Loc.GetString("lancer-arcade-hex-hover-idle");

    public static string Build(LancerGameStateSnapshot snapshot, LancerGridCoord coord)
    {
        if (!LancerHex.InBounds(coord))
            return BuildIdle();

        var cell = snapshot.Cells[coord.Y][coord.X];
        var coordLabel = FormatCoord(coord);

        var (terrainName, terrainDesc) = cell.Terrain switch
        {
            LancerTerrainType.Relay => (
                Loc.GetString("lancer-arcade-terrain-relay-name"),
                Loc.GetString("lancer-arcade-terrain-relay-desc")),
            LancerTerrainType.RubbleSoft => (
                Loc.GetString("lancer-arcade-terrain-rubble-soft-name"),
                Loc.GetString("lancer-arcade-terrain-rubble-soft-desc")),
            LancerTerrainType.RubbleHard => (
                Loc.GetString("lancer-arcade-terrain-rubble-hard-name"),
                Loc.GetString("lancer-arcade-terrain-rubble-hard-desc")),
            _ => (
                Loc.GetString("lancer-arcade-terrain-open-name"),
                Loc.GetString("lancer-arcade-terrain-open-desc"))
        };

        var lines = new List<string>
        {
            Loc.GetString("lancer-arcade-hex-hover-header",
                ("coord", coordLabel),
                ("terrain", terrainName)),
            terrainDesc
        };

        var highlight = cell.Highlight switch
        {
            LancerCellHighlight.Reachable => Loc.GetString("lancer-arcade-highlight-reachable"),
            LancerCellHighlight.Target => Loc.GetString("lancer-arcade-highlight-target"),
            LancerCellHighlight.Blast => Loc.GetString("lancer-arcade-highlight-blast"),
            _ => null
        };

        if (highlight != null)
            lines.Add(highlight);

        var unit = snapshot.Units.FirstOrDefault(u =>
            u.Position.X == coord.X && u.Position.Y == coord.Y);

        if (unit != null)
        {
            lines.Add(Loc.GetString("lancer-arcade-hex-unit",
                ("unit", GetUnitName(unit.Kind)),
                ("hp", unit.Hp),
                ("maxHp", unit.MaxHp)));

            var statuses = new List<string>();
            if (unit.LockedOn)
                statuses.Add(Loc.GetString("lancer-arcade-status-locked-on"));
            if (unit.Shredded)
                statuses.Add(Loc.GetString("lancer-arcade-status-shredded"));
            if (unit.Impaired)
                statuses.Add(Loc.GetString("lancer-arcade-status-impaired"));
            if (statuses.Count > 0)
                lines.Add(string.Join(" · ", statuses));
        }

        return string.Join("\n", lines);
    }

    private static string GetUnitName(LancerUnitKind kind) => kind switch
    {
        LancerUnitKind.PlayerMech => Loc.GetString("lancer-arcade-unit-playermech"),
        LancerUnitKind.Grunt => Loc.GetString("lancer-arcade-unit-grunt"),
        LancerUnitKind.Urbie => Loc.GetString("lancer-arcade-unit-urbie"),
        LancerUnitKind.Assault => Loc.GetString("lancer-arcade-unit-assault"),
        LancerUnitKind.Cutlass => Loc.GetString("lancer-arcade-unit-cutlass"),
        LancerUnitKind.Sniper => Loc.GetString("lancer-arcade-unit-sniper"),
        LancerUnitKind.Bombard => Loc.GetString("lancer-arcade-unit-bombard"),
        LancerUnitKind.Relay => Loc.GetString("lancer-arcade-unit-relay"),
        _ => kind.ToString()
    };

    private static string FormatCoord(LancerGridCoord pos) =>
        $"{(char) ('A' + pos.X)}{pos.Y + 1}";
}
