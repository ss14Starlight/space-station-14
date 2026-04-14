using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Shared.Speech;

namespace Content.Server._Starlight.Speech.EntitySystems;

public sealed partial class BleatingAccentSystem : EntitySystem
{
    [GeneratedRegex("([mbdlpwhrkcnytfo])([aiu])", RegexOptions.IgnoreCase)]
    private static partial Regex BleatRegex();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BleatingAccentComponent, AccentGetEvent>(OnAccentGet);
    }

    private void OnAccentGet(Entity<BleatingAccentComponent> entity, ref AccentGetEvent args) =>
        // Only modify displayed text, TTS stays normal
        args.Message.Text = Accentuate(args.Message.Text);

    public static string Accentuate(string message) =>
         // Repeats the vowel in certain consonant-vowel pairs
         // So you taaaalk liiiike thiiiis
         BleatRegex().Replace(message, "$1$2$2$2$2");
}
