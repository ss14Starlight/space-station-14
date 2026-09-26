using System.Linq;
using Content.Server.Chat.Managers;
using Content.Shared._NullLink;
using Content.Shared.CCVar;
using Robust.Shared.Network;
using Robust.Shared.Player;

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
            RebuildTitle(_playerManager.GetSessionById(new NetUserId(player.Key)), player.Value);
    }

    private void RebuildTitle(ICommonSession player, PlayerData playerData)
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
                else if (_netConfigManager.GetClientCVar(player.Channel, CCVars.ShowOocPatronColor) && player.Channel.UserData.PatronTier is { } patron && ChatManager.PatronOocColors.TryGetValue(patron, out var patronColor))
                    result.Add($"[color={patronColor}]{title.Text}[/color]");
                else
                    result.Add(title.Text);
                break;
            }
        }

        playerData.Title = result.Count > 0 ? string.Join(_builder.Separator, result) : null;
    }
}
