using System.Linq;
using Content.Server.Administration;
using Content.Shared._Functional.TutorialServer;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Host command: crop tutorial section templates from station maps into Resources.
/// Usage: tutorialcropsections [all|CropMedbay|...]
/// </summary>
[AdminCommand(AdminFlags.Host)]
public sealed partial class TutorialCropSectionsCommand : LocalizedCommands
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPrototypeManager _protos = default!;

    public override string Command => "tutorialcropsections";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var cropper = _entities.System<TutorialSectionCropSystem>();
        var root = TutorialSectionCropSystem.FindResourcesDirectory();
        if (root == null)
        {
            shell.WriteError("Could not find Resources/ directory (run from repo root).");
            return;
        }

        if (args.Length == 0 || args[0].Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var failures = new List<string>();
            var ok = cropper.TryCropAndSaveAll(root, failures);
            shell.WriteLine(ok
                ? $"Cropped all tutorial sections into {root}"
                : $"Crop finished with failures: {string.Join(", ", failures)}");
            return;
        }

        var id = args[0];
        if (!_protos.HasIndex<TutorialSectionCropPrototype>(id))
        {
            shell.WriteError($"Unknown tutorialSectionCrop '{id}'. Known: {string.Join(", ", _protos.EnumeratePrototypes<TutorialSectionCropPrototype>().Select(p => p.ID))}");
            return;
        }

        if (cropper.TryCropAndSave(id, root))
            shell.WriteLine($"Cropped {id}");
        else
            shell.WriteError($"Failed to crop {id}");
    }
}
