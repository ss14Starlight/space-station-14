using Robust.Shared.Audio;

namespace Content.Shared.Starlight.Medical.Surgery;
// Based on the RMC14.
// https://github.com/RMC-14/RMC-14
public interface ISurgeryToolComponent
{
    public string ToolName { get; }
    // FarHorizons Start
    public string ToolType { get; }
    public float Speed { get; }
    public float SuccessRate { get; }
    public SoundSpecifier? StartSound { get; }
    public SoundSpecifier? EndSound { get; }
    // FarHorizons End
}
