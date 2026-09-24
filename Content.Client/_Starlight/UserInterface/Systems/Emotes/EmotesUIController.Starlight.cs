using System.Linq;
using Content.Shared._Starlight.CloudEmotes;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;

namespace Content.Client.UserInterface.Systems.Emotes;

public sealed partial class EmotesUIController
{
    // Emote category priority. Unspecified categories are sorted last.
    private static readonly Dictionary<EmoteCategory, int> CategoryPriority = new()
    {
        [EmoteCategory.Hands] = 0,
        [EmoteCategory.Vocal] = 1,
        [EmoteCategory.General] = 2,
        [EmoteCategory.Cloud] = 3,
    };

    private static int GetCategoryPriority(EmoteCategory category)
    {
        return CategoryPriority.TryGetValue(category, out var priority) ? priority : int.MaxValue;
    }

    private static IEnumerable<EmotePrototype> SortEmotePrototypes(IEnumerable<EmotePrototype> prototypes)
    {
        return prototypes
            .OrderBy(proto => GetCategoryPriority(proto.Category))
            .ThenBy(proto => proto.Priority)
            .ThenBy(proto => Loc.GetString(proto.Name), StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<CloudEmotePrototype> SortCloudEmotePrototypes(IEnumerable<CloudEmotePrototype> prototypes)
    {
        return prototypes
            .OrderBy(proto => proto.Priority)
            .ThenBy(proto => Loc.GetString(proto.Name), StringComparer.OrdinalIgnoreCase);
    }
}
