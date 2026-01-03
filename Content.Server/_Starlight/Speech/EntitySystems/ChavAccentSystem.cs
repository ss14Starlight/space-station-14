using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Robust.Shared.Random;
using Robust.Shared.Reflection;
using System.Text.RegularExpressions;

namespace Content.Server.Speech.EntitySystems
{
    public sealed class ChavAccentSystem : EntitySystem
    {
        [Dependency] private readonly IRobustRandom _random = default!;

        // Regex for splitting on words:
        private static readonly Regex s_wordSplit = new(pattern: "([\\p{P}\\p{Z}])",
            options: RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly IReadOnlyDictionary<string, string> SpecialWords = new Dictionary<string, string>()
        {
            // RESTRICTION: words with apostrophes in them (won't, don't, ma'am, etc. are processed as two tokens (e.g. won + t))

            // selected Cockney Rhyming Slang (https://en.wikipedia.org/wiki/Rhyming_slang)

            { "believe", "Adam and Eve" },
            { "arse", "bottle" }, // 'bottle and glass'
            { "neck", "Gregory" }, // 'Gregory Peck'
            { "necks", "Gregories" }, // 'Gregory Peck'
            { "fart", "raspberry" }, // 'raspberry tart'
            // { "head", "loaf" }, // 'loaf of bread'
            // { "heads", "loaves" }, // 'loaf of bread'
            // { "eye", "mince" }, // 'mince pie'
            // { "eyes", "minces" }, // 'mince pie'
            // { "feet", "plates" }, // 'plates of meat'
            { "hat", "titfer" }, // 'tit for tat'
            { "wife", "trouble" }, // 'trouble and strife'
            { "sweetheart", "treacle" }, // 'treacle tart'
            { "dear", "treacle" }, // 'treacle tart' (sweetheart)
            { "honey", "treacle" }, // 'treacle tart' (sweetheart)
            { "hun", "treacle" }, // 'treacle tart' (sweetheart)
            { "hon", "treacle" }, // 'treacle tart' (sweetheart)
            { "sweetie", "treacle" }, // 'treacle tart' (sweetheart)
            { "suit", "whistle" }, // 'whistle and flute'
            { "cunt", "berk" }, // 'berkshire hunt'
            { "rotten", "bales" }, // 'bales of cotton'
            { "stairs", "apples and pears" },
            { "stair", "apples and pears" },
            { "pizza", "Mona Lisa" },
            { "balls", "cobblers" }, // 'cobbler\'s awls'
            { "crap", "pony and trap" },

            // general replacements:

            { "bruh", "bruv" },
            { "bro", "bruv" },
            { "brother", "bruvva" },
            { "bud", "bruv" },
            { "friend", "wanka" },
            { "friends", "mates" },
            { "sister", "sista" },
            { "sir", "guv" },
            { "security", "po-po" },
            { "shitsec", "filth" },
            { "secoff", "bobby" },
            { "secoffs", "bobbies" },
            { "officer", "guv" },
            /* { "hop", "form fairy" },
            { "qm", "jobsworth" },
            { "doctor", "quack" },
            { "doctors", "quacks" },
            { "psych", "pill pusher" },*/
            { "psychologist", "pill pusher" },
            { "surgeon", "sawbones" },
            { "surgeons", "sawbones" },
            { "medbay", "A&E" },
            // { "station", "madhouse" },
            { "speso", "quid" },
            { "spesos", "quid" },
            { "credit", "quid" },
            { "credits", "quid" },
            { "crazy", "daft" },
            { "drunk", "plastered" },
            { "questionable", "dodgy" },
            { "beverage", "bevvy" },
            { "beverages", "bevvies" },
            { "beer", "bevvy" },
            { "beers", "bevvies" },
            { "booze", "bevvy" },
            { "excited", "chuffed" },
            { "thrilled", "chuffed" },
            { "great", "cracking" },
            { "very", "propa" },
            { "steal", "nick" },
            { "stole", "nicked" },
            { "stolen", "nicked" },
            { "steals", "nicks" },
            { "stealing", "nicking" },
            /*{ "cc", "Council" },
            { "centcom", "Council" },
            { "centcomm", "Council" },*/
            { "maam", "lady" },
            { "this", "dis" },
            { "that", "dat" },
            { "these", "deez" },
            { "those", "doze" },
            { "though", "al'o" },
            { "although", "al'o" },
            { "alright", "awlrite" },
            { "they", "dey" },
            { "the", "da" },
            { "their", "ez" },
            { "there", "dere" },
            { "them", "em" },
            { "than", "dan" },
            { "then", "den" },
            { "sixth", "sicth" },
            { "what", "wot" },
            { "food", "chow" },
            { "mommy", "nan" },
            { "mom", "mum" },
            { "mother", "mum" },
            { "daddy", "paw" },
            { "dad", "da" },
            { "father", "paw" },
            { "little", "lil" },
            { "ass", "arse" },
            { "asses", "arses" },
            { "fuck", "fack" },
            { "fucks", "facks" },
            { "fucking", "facking" },
            { "fucked", "facked" },
            { "fucker", "facker" },
            { "hi", "oi" },
            { "hey", "oi" },
            { "hello", "oi" },
            { "cig", "ciggy" },
            { "cigs", "ciggies" },
            { "cigarette", "ciggy" },
            { "cigarettes", "ciggies" },
            { "bartender", "barkeep" },
            { "bartenders", "barkeeps" },
            { "assistant", "drudge" },
            { "assistants", "drudges" },
            { "jerk", "wanka" },
            { "asshole", "berk" },
            { "idiot", "twit" },
            { "guy", "bloke" },
            { "man", "bloke" },
            { "guys", "blokes" },
            { "men", "blokes" },
            { "scientist", "boffin" },
            { "scientists", "boffins" },
            { "buddy", "mate" },
            { "buddies", "mates" },
            { "pal", "mate" },
            { "pals", "mates" },
            { "lady", "dame" },
            { "woman", "dame" },
            { "stinky", "rancid" },
            { "smelly", "foul" },
            { "moff", "buggy wanka" },
            { "birb", "feavvery wanka" },
            { "moffs", "buggy wankas" },
            { "birbs", "feavvery wankas" },
            { "feather", "feavver" },
            { "feathery", "feavvery" },
            { "feathers", "feavvers" },
            { "feathered", "feavvered" },
            { "feathering", "feavvering" },
            { "bird", "feavvery wanka" },
            { "girl", "bird" },
            { "girls", "birds" },
            { "vox", "gassy wanka" },
            { "cat", "hissy wanka" },
            { "cyclorite", "one-eyed wanka" },
            { "resomi", "wanka" },
            { "avali", "wanka" },
            { "vulp", "drooling wanka" },
            { "voxes", "gassy wankas" },
            { "cats", "hissy wankas" },
            { "cyclorites", "one-eyed wankas" },
            { "resomis", "wankas" },
            { "avalis", "wankas" },
            { "vulps", "drooling wankas" },
            { "dorf", "manlet" },
            { "dorfs", "manlets" },
            { "dwarf", "manlet" },
            { "dwarfs", "manlets" },
            { "dwarves", "manlets" },
            { "borg", "toaster" },
            { "borgs", "toasters" },
            /*{ "bso", "filth" },
            { "cap", "boss" },
            { "captain", "boss" },
            { "iaa", "shark" },
            { "iaas", "sharks" },
            { "ntr", "judge" },*/
            { "magi", "fancy nob" },
            { "right", "roight" },
            { "fine", "foine" },
            { "good", "roight propa" },
            { "lighter", "loighta" },
            { "nice", "noice" },
            { "cool", "cor" },
            { "my", "me" },
            { "you", "yew" },
            { "your", "yer" },
            { "some", "sum" },
        };

        public override void Initialize()
        {
            SubscribeLocalEvent<ChavAccentComponent, AccentGetEvent>(OnAccent);
        }

        public string Accentuate(string message)
        {
            // proper word replacement, considering word boundaries:
            var tokens = s_wordSplit.Split(message);
            
            for (int ti = 0; ti < tokens.Length; ++ti) {
                var token = tokens[ti];

                // token contains no letters, ignore:
                if(token.ToUpper() == token.ToLower())
                    continue;
                
                var token_lower = token.ToLower();
                
                if (token.Length > 1) {
                    if (SpecialWords.ContainsKey(token_lower)) {
                        var word = token_lower;
                        var repl = SpecialWords[token_lower];

                        // replace each pair:
                        if (token == word) {
                            token = repl;
                        } else {
                            // case didn't match -- maybe it was capitalized?
                            string word_cap = word[0].ToString().ToUpper() + word.Substring(1);
                            if (token == word_cap) {
                                token = repl[0].ToString().ToUpper() + repl.Substring(1);
                            } else {
                                // the word matches but case is uncertain -- assume all-caps
                                token = repl.ToUpper();
                            }
                        }
                    }
                }

                tokens[ti] = token;
            }
            message = string.Concat(tokens);

            // final phonetic processing:
            return message
                .Replace("th", "ff").Replace("Th", "F")
                .Replace("TH", "FF").Replace(" ff", " f").Replace(" FF", " F");
        }

        private void OnAccent(EntityUid uid, ChavAccentComponent component, AccentGetEvent args)
        {
            args.Message = Accentuate(args.Message);
        }
    }
}
