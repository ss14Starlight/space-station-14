using System.IO;
using Content.Client._Starlight.Construction;
using Content.Client._Starlight.Construction.UI;
using Content.Client.Popups;
using Content.Shared.Popups;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;

// ReSharper disable CheckNamespace
// Partial of the upstream ConstructionMenuPresenter, so it has to share its namespace.
namespace Content.Client.Construction.UI;

internal sealed partial class ConstructionMenuPresenter
{
    [Dependency] private IFileDialogManager _fileDialogManager = default!;

    private static readonly FileDialogFilters TemplateFilters = new(new FileDialogFilters.Group("yml"));

    private PopupSystem _popupSystem = default!;
    private bool _templateBusy;

    private void InitializeTemplates()
    {
        if (_constructionView is not ConstructionMenu menu)
            return;

        _popupSystem = _entManager.System<PopupSystem>();

        menu.ExportTemplateButton.OnPressed += _ => ExportTemplate();
        menu.ImportTemplateButton.OnPressed += _ => ImportTemplate();
    }

    private async void ExportTemplate()
    {
        if (_templateBusy || _constructionSystem is null)
            return;

        var template = _constructionSystem.CreateTemplate(out var skipped);

        if (template is null)
        {
            _popupSystem.PopupCursor(
                Loc.GetString("construction-template-export-empty"),
                PopupType.MediumCaution);
            return;
        }

        if (skipped > 0)
        {
            _popupSystem.PopupCursor(
                Loc.GetString("construction-template-export-skipped", ("count", skipped)),
                PopupType.MediumCaution);
        }

        _templateBusy = true;

        try
        {
            var file = await _fileDialogManager.SaveFile(TemplateFilters);

            if (file is not { } save)
                return;

            await using var writer = new StreamWriter(save.fileStream);
            _constructionSystem.ToDataNode(template).Write(writer);
        }
        catch (Exception exc)
        {
            _sawmill.Error($"Error when exporting construction template\n{exc}");
            _popupSystem.PopupCursor(
                Loc.GetString("construction-template-export-failed"),
                PopupType.MediumCaution);
        }
        finally
        {
            _templateBusy = false;
        }
    }

    private async void ImportTemplate()
    {
        if (_templateBusy || _constructionSystem is null)
            return;

        _templateBusy = true;

        try
        {
            await using var file = await _fileDialogManager.OpenFile(TemplateFilters, FileAccess.Read);

            if (file is null)
                return;

            var system = _constructionSystem;
            var template = system.FromStream(file);

            if (template.Entries.Count == 0)
            {
                _popupSystem.PopupCursor(
                    Loc.GetString("construction-template-import-empty"),
                    PopupType.MediumCaution);
                return;
            }

            if (!system.TryGetTemplateOrigin(template, out var origin))
            {
                BeginTemplatePlacement(system, template);
                return;
            }

            var window = new ConstructionTemplateOriginWindow();
            window.Chosen += saved =>
            {
                if (saved)
                    system.SpawnTemplate(template, origin, Direction.South);
                else
                    BeginTemplatePlacement(system, template);
            };
        }
        catch (Exception exc)
        {
            _sawmill.Error($"Error when importing construction template\n{exc}");
            _popupSystem.PopupCursor(
                Loc.GetString("construction-template-import-failed"),
                PopupType.MediumCaution);
        }
        finally
        {
            _templateBusy = false;
        }
    }

    private void BeginTemplatePlacement(ConstructionSystem system, ConstructionTemplate template)
    {
        _placementManager.BeginPlacing(new PlacementInformation
            {
                IsTile = false,
                PlacementOption = "SnapgridCenter",
            },
            new ConstructionTemplatePlacementHijack(system, template));
    }
}
