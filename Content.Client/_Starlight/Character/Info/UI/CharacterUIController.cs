using Content.Client._Starlight.Character.Info.UI;

// ReSharper disable once CheckNamespace
namespace Content.Client.UserInterface.Systems.Character;

public sealed partial class CharacterUIController
{
    private readonly Dictionary<EntityUid, CharacterInspectWindow> _openInspectionWindows = new();

    public void OpenInspectCharacterWindow(EntityUid target, EntityUid viewer)
    {
        if (!target.Valid)
            return;

        if (target == viewer)
        {
            //If attempting to inspect own character, redirect to character window
            if (_window == null || _window.IsOpen)
            {
                return;
            }

            _characterInfo.RequestCharacterInfo();
            SLSetSelfCharacterInfo();
            _window.Open();
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
    }

    /// <summary>
    /// Opens the local player's character window on its overview tab.
    /// </summary>
    public void OpenCharacterOverview()
    {
        if (_window == null)
            return;

        _window.CharacterInfoTabs.CurrentTab = 0;
        if (_window.IsOpen)
            return;

        CharacterButton?.SetClickPressed(true);
        _characterInfo.RequestCharacterInfo();
        SLSetSelfCharacterInfo();
        _window.Open();
    }

    private void SLClearSelfCharacterInfo()
    {
        if (_window == null)
            return;
        _window.InfoIC.ClearCharacter();
        _window.InfoOOC.ClearCharacter();
        _window.InfoBackground.ClearCharacter();
    }

    private void SLSetSelfCharacterInfo()
    {
        if (_window == null)
            return;
        var ent = _window.CharacterInfo.CharacterPreview.CharacterSpriteView.Entity;
        if (!ent.HasValue)
        {
            _window.InfoIC.ClearCharacter();
            _window.InfoOOC.ClearCharacter();
            _window.InfoBackground.ClearCharacter();
            return;
        }

        _window.InfoIC.SetCharacter(ent, EntityManager, ent.Value);
        _window.InfoOOC.SetCharacter(ent, EntityManager, ent);
        _window.InfoBackground.SetCharacter(ent, EntityManager, ent);
    }
}
