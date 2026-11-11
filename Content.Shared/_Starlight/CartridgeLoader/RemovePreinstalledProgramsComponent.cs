using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.CartridgeLoader;

/// <summary>
/// Drops programs from an inherited <see cref="Content.Shared.CartridgeLoader.CartridgeLoaderComponent.PreinstalledPrograms"/>
/// before they get installed, e.g. NanoChat on dog collars, whichever PDA a collar is based on.
/// </summary>
[RegisterComponent]
public sealed partial class RemovePreinstalledProgramsComponent : Component
{
    [DataField(required: true)]
    public List<EntProtoId> Programs = new();
}
