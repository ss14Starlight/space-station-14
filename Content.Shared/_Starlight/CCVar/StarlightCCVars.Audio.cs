using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;
public sealed partial class StarlightCCVars
{
    public static readonly CVarDef<float> StationRadioVolume =
        CVarDef.Create("audio.station_radio_volume", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);
}
