using Robust.Shared.Audio;

namespace Content.Shared.VendingMachines;

public sealed partial class VendingMachineComponent
{
    // Moffstation - keypad sounds from the diegetic vending UI.
    [DataField]
    public SoundSpecifier BeepSound = new SoundPathSpecifier("/Audio/Machines/Nuke/general_beep.ogg", new AudioParams().WithVolume(-4));

    [DataField]
    public SoundSpecifier SuccessSound = new SoundPathSpecifier("/Audio/Machines/vending_jingle.ogg", new AudioParams().WithVolume(-4));

    [DataField]
    public SoundSpecifier ErrorSound = new SoundPathSpecifier("/Audio/Machines/buzz-two.ogg", new AudioParams().WithVolume(-4));

    [DataField]
    public SoundSpecifier TimeoutSound = new SoundPathSpecifier("/Audio/Machines/button.ogg", new AudioParams().WithVolume(-6));
}
