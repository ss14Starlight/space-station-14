using System.Text;
using System.Text.RegularExpressions;
using Content.Client.Administration.UI.CustomControls;
using Content.Client.Verbs.UI;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Administration.Logs;

/// <summary>
/// An admin log line that highlights the entities its message refers to and opens their context menu.
/// </summary>
public sealed class AdminLogEntityLabel : AdminLogLabel
{
    private static readonly Regex EntityPattern = new(
        @"\(\d+/n(?<net>\d+)(?:,[^)\r\n]*)?\)D?|\b\d+/n(?<net>\d+)D",
        RegexOptions.Compiled);

    private static readonly string EntityColorTag = $"[color={Color.LightSkyBlue.ToHexNoAlpha()}]";

    private const string TimestampColorTag = "[color=#7A7F92]";

    private readonly List<(NetEntity Entity, string Text)> _entities = new();

    private Popup? _popup;

    public AdminLogEntityLabel(ref SharedAdminLog log, HSeparator separator) : base(ref log, separator)
    {
        var markup = new StringBuilder($"{TimestampColorTag}{log.Date:HH:mm:ss}:[/color] ");
        var read = 0;

        foreach (Match match in EntityPattern.Matches(log.Message))
        {
            if (!int.TryParse(match.Groups["net"].ValueSpan, out var id))
                continue;

            var entity = new NetEntity(id);
            if (!entity.IsValid())
                continue;

            markup.Append(log.Message[read..match.Index])
                .Append(EntityColorTag)
                .Append(match.Value)
                .Append("[/color]");
            read = match.Index + match.Length;

            if (!_entities.Exists(reference => reference.Entity == entity))
                _entities.Add((entity, match.Value));
        }

        markup.Append(log.Message[read..]);
        SetMessage(FormattedMessage.FromMarkupPermissive(markup.ToString()), ImpactColor(log.Impact));

        if (_entities.Count == 0)
            return;

        MouseFilter = MouseFilterMode.Pass;
        ToolTip = Loc.GetString("admin-logs-entity-context-tooltip");
        OnKeyBindDown += EntityKeyBindDown;
    }

    private static Color ImpactColor(LogImpact impact) => impact switch
    {
        LogImpact.Extreme => Color.FromHex("#E88F8F"),
        LogImpact.High => Color.FromHex("#E8B87F"),
        LogImpact.Low => Color.FromHex("#8F94A3"),
        _ => Color.FromHex("#C8C8C8"),
    };

    private void EntityKeyBindDown(GUIBoundKeyEventArgs args)
    {
        if (args.Function != EngineKeyFunctions.UIRightClick)
            return;

        if (_entities.Count == 1)
            OpenVerbMenu(_entities[0].Entity);
        else
            OpenEntityPopup();

        args.Handle();
    }

    private void OpenVerbMenu(NetEntity entity)
        => UserInterfaceManager.GetUIController<VerbMenuUIController>().OpenVerbMenu(entity, true);

    private void OpenEntityPopup()
    {
        _popup?.Close();

        var entities = IoCManager.Resolve<IEntityManager>();
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };

        foreach (var (entity, text) in _entities)
        {
            var button = new Button
            {
                Text = entities.TryGetEntity(entity, out var uid) && entities.TryGetComponent(uid, out MetaDataComponent? meta)
                    ? $"{meta.EntityName} {text}"
                    : text,
                StyleClasses = { "ButtonSquare" },
                HorizontalAlignment = HAlignment.Stretch,
            };

            button.OnPressed += _ =>
            {
                _popup?.Close();
                OpenVerbMenu(entity);
            };
            box.AddChild(button);
        }

        _popup = new Popup();
        _popup.AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = new Color(32, 32, 40) },
            Children = { box },
        });

        _popup.OpenAtMouse();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();

        _popup?.Close();
    }
}
