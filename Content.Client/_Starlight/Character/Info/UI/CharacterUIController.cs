using Content.Client._Starlight.Character.Info.UI;

// ReSharper disable once CheckNamespace
namespace Content.Client._Moffstation.CharacterMenu;

public sealed partial class CharacterUIController
{
    private readonly Dictionary<EntityUid, CharacterInspectWindow> _openInspectionWindows = new();

    public void OpenInspectCharacterWindow(EntityUid target, EntityUid viewer)
    {
        if (!target.Valid)
            return;

        if (target == viewer)
        {
            OpenWindow();
            return;
        }

        if (_openInspectionWindows.TryGetValue(target, out var window))
        {
            window.SetCharacter(target, EntityManager, viewer);
            window.OpenCentered();
            return;
        }

        window = new CharacterInspectWindow();
        window.SetCharacter(target, EntityManager, viewer.Valid ? viewer : target);

        _openInspectionWindows[target] = window;

        window.OnClose += () => _openInspectionWindows.Remove(target);
        window.Title = Loc.GetString("character-info-window-title", ("player", target));
        window.OpenCentered();
    }

    /// <summary>
    /// Opens the local player's character window on its overview tab.
    /// </summary>
    public void OpenCharacterOverview()
    {
        if (_window == null)
            return;

        _window.CharacterInfoTabs.CurrentTab = 0;
        OpenWindow();
    }

    private void ClearSelfCharacterInfo()
    {
        if (_window == null)
            return;
        _window.InfoIC.ClearCharacter();
        _window.InfoOOC.ClearCharacter();
        _window.InfoBackground.ClearCharacter();
    }

    private void SetSelfCharacterInfo(EntityUid? ent)
    {
        if (_window == null)
            return;

        if (!ent.HasValue || !_window.IsOpen)
        {
            ClearSelfCharacterInfo();
            return;
        }

        _window.InfoIC.SetCharacter(ent, EntityManager, ent.Value);
        _window.InfoOOC.SetCharacter(ent, EntityManager, ent);
        _window.InfoBackground.SetCharacter(ent, EntityManager, ent);
    }
}
