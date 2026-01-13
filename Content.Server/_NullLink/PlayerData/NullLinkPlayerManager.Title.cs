using System.Linq;
using Content.Shared._NullLink;

namespace Content.Server._NullLink.PlayerData;

public sealed partial class NullLinkPlayerManager : INullLinkPlayerManager
{
    private void UpdateTitleBuilder(string obj)
    {
        if (_builder?.ID == obj)
            return;
        if (!_proto.TryIndex<TitleBuilderPrototype>(obj, out var builder))
            return;
        _builder = builder;

        foreach (var player in _playerById)
            RebuildTitle(player.Key, player.Value);
    }

    private void RebuildTitle(Guid player, PlayerData playerData)
    {
        if (_builder == null)
            return;

        var result = new List<string>(_builder.Segments.Count);
        foreach (var segment in _builder.Segments)
        {
            foreach (var title in segment.Titles)
            {
                if (!title.Roles.Any(playerData.Roles.Contains))
                    continue;
                if (title.Color != null)
                    result.Add($"[color={title.Color.Value.ToHex()}]{title.Text}[/color]");
                else
                    result.Add(title.Text);
                break;
            }
        }

        playerData.Title = result.Count > 0 ? string.Join(_builder.Separator, result) : null;
    }
}
