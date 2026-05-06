using System.Linq;
using Content.Shared.Examine;

namespace Content.Shared._Starlight.Markup.Components;

public sealed class MarkupTextSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MarkupDescriptionComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<MarkupDescriptionComponent> entity, ref ExaminedEvent args)
    {
        foreach (var group in entity.Comp.Texts.Values.GroupBy(x => x.Item1).OrderBy(x => x.Key))
        {
            using (args.PushGroup($"markupcomp-{group.Key}", group.Key))
            {
                foreach (var entry in group)
                    args.PushMarkup(entry.Item2);
            }
        }
    }

    public void AddDescriptionText(Entity<MarkupDescriptionComponent> entity, string id, string text, int priority)
    {
        entity.Comp.Texts.Add(id, (priority, text));
        Dirty(entity);
   }

    public void EditDescriptionText(Entity<MarkupDescriptionComponent> entity, string id, string text, int priority)
    {
        if (!entity.Comp.Texts.ContainsKey(id))
            return;

        entity.Comp.Texts[id] = (priority, text);
        Dirty(entity);
    }

    public void RemoveDescriptionText(Entity<MarkupDescriptionComponent> entity, string id)
    {
        entity.Comp.Texts.Remove(id);
        Dirty(entity);
    }

    public void ClearDescriptionText(Entity<MarkupDescriptionComponent> entity)
    {
        entity.Comp.Texts.Clear();
        Dirty(entity);
    }
}
