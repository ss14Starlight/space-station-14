using System.Text;
using Content.Shared._Moffstation.Speech.Components;
using Content.Shared.Speech;

namespace Content.Shared._Moffstation.Speech.Systems;

public sealed partial class NarSieDemonAccentSystem : EntitySystem
{
    private static readonly Dictionary<char, string> Replacements = new()
    {
        ['a'] = "a̷̛͖̪̾",
        ['b'] = "b̶̤́",
        ['c'] = "c̷̦̾",
        ['d'] = "d̸̥̠͗̉",
        ['e'] = "ê̴̝̩͂",
        ['f'] = "f̴̂̏ͅ",
        ['g'] = "g̴̨͖̒̓",
        ['h'] = "h̸͆͝ͅ",
        ['i'] = "i̴̢̲̅͊",
        ['j'] = "j̶̥̈́",
        ['k'] = "k̵̥̫͋",
        ['l'] = "l̴̫͐͘",
        ['m'] = "m̷͔̕",
        ['n'] = "n̷̢͝",
        ['o'] = "o̵͕̎ͅ",
        ['p'] = "p̵̣̍̒",
        ['q'] = "q̸̞̰̈́",
        ['r'] = "r̴̼̍",
        ['s'] = "s̶̳̣͊͆",
        ['t'] = "t̸͍̔",
        ['u'] = "ṷ̶͌",
        ['v'] = "v̷̰̞̕",
        ['w'] = "w̶̹̲͝",
        ['x'] = "x̶̯̄",
        ['y'] = "ẏ̴̻̞̚",
        ['z'] = "ź̵̠͎͝",

        ['A'] = "A̴̡̦̪̦̅͛̍̀",
        ['B'] = "B̴͕̜̌͑͗͊̏̐",
        ['C'] = "C̷͙̀̽͐͆͘͝",
        ['D'] = "D̴̲̮̈́͂͠",
        ['E'] = "E̶͎̋̊",
        ['F'] = "F̵̻̱̳̅̇̅̄͝",
        ['G'] = "G̷͕͕̱̓̔̀̈́͗͜͜",
        ['H'] = "H̶̘̙̄͐͝",
        ['I'] = "Ì̵̧̨̙͕̲",
        ['J'] = "J̵̹̮̙̫͎̥̾̇",
        ['K'] = "K̴̟͍͑̈́̒͐",
        ['L'] = "L̸̪͉̙̣̼̺͐͌̀̕",
        ['M'] = "M̷̙̯̞̘͑̋͆",
        ['N'] = "Ň̶̨̨̼̠͓̌̇̌͘̚",
        ['O'] = "Ọ̵̘́͑͝",
        ['P'] = "P̵̰͆̓̈́̃̕͠",
        ['Q'] = "Q̶̧̻͎̺͎̩͘",
        ['R'] = "R̷̻̆̊̓̂͠",
        ['S'] = "S̵̬̙̦̑̎̽ͅ",
        ['T'] = "T̶̰͌͌̀͛͝",
        ['U'] = "Ṷ̸̡̙͋",
        ['V'] = "V̷̯͈̯̿̀̕",
        ['W'] = "Ẅ̷͎͖̻̼͇́̈́",
        ['X'] = "X̷̦͎̽",
        ['Y'] = "Y̴̫̗̍̋͑̾͜",
        ['Z'] = "Z̸̮͍͙̺͋̀̽̈́̚",
    };

    [SubscribeLocalEvent]
    private static void OnAccent(Entity<NarSieDemonAccentComponent> ent, ref AccentGetEvent args)
    {
        var sb = new StringBuilder();
        foreach (var c in args.Message)
        {
            if (Replacements.TryGetValue(c, out var replacement))
            {
                sb.Append(replacement);
            }
            else
            {
                sb.Append(c);
            }
        }

        args.Message = sb.ToString();
    }
}
