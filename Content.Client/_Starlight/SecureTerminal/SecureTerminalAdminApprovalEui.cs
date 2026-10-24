using Content.Client.Eui;
using Content.Shared._Starlight.SecureTerminal;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.SecureTerminal;

[UsedImplicitly]
public sealed class SecureTerminalAdminApprovalEui : BaseEui
{
    private readonly SecureTerminalAdminApprovalWindow _window = new();
    private bool _responded;

    public SecureTerminalAdminApprovalEui()
    {
        _window.ApproveButton.OnPressed += _ => Respond(true);
        _window.DenyButton.OnPressed += _ => Respond(false);
    }

    /// <inheritdoc/>
    public override void Opened()
    {
        base.Opened();
        _window.OpenCentered();
    }

    /// <inheritdoc/>
    public override void Closed()
    {
        base.Closed();
        _window.Close();
    }

    /// <inheritdoc/>
    public override void HandleState(EuiStateBase state)
    {
        base.HandleState(state);
        if (state is not SecureTerminalAdminApprovalEuiState approvalState)
            return;

        _window.InformationContainer.RemoveAllChildren();
        AddWrappedText("secure-terminal-admin-approval-request", approvalState.RequestName,
            _window.InformationContainer);
        AddWrappedText("secure-terminal-admin-approval-description", approvalState.RequestDescription,
            _window.InformationContainer);
        if (approvalState.Reason is { } reason)
            AddWrappedText("secure-terminal-admin-approval-reason", reason, _window.InformationContainer);

        _window.AuthorizedByContainer.RemoveAllChildren();
        _window.AuthorizedByContainer.AddChild(new Label
        {
            Text = Loc.GetString("secure-terminal-admin-approval-authorized-by")
        });
        var authorizedBy = new RichTextLabel
        {
            HorizontalExpand = true
        };
        authorizedBy.SetMessage(FormattedMessage.FromUnformatted(
            string.Join(", ", approvalState.AuthorizedBy)));
        _window.AuthorizedByContainer.AddChild(authorizedBy);
    }

    private static void AddWrappedText(string localizationKey, string value, BoxContainer container)
        => AddWrappedPlainText(Loc.GetString(localizationKey, ("request", value), ("description", value), ("reason", value)), container);

    private static void AddWrappedPlainText(string value, BoxContainer container)
    {
        const int CharactersPerLine = 120;
        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line = string.Empty;
        foreach (var word in words)
        {
            if (line.Length > 0 && line.Length + word.Length + 1 > CharactersPerLine)
            {
                container.AddChild(new Label { Text = line });
                line = word;
            }
            else
            {
                line = line.Length == 0 ? word : $"{line} {word}";
            }
        }

        if (line.Length > 0)
            container.AddChild(new Label { Text = line });
    }

    private void Respond(bool approved)
    {
        if (_responded)
            return;

        _responded = true;
        SendMessage(new SecureTerminalAdminApprovalMessage(approved));
        _window.Close();
    }
}
