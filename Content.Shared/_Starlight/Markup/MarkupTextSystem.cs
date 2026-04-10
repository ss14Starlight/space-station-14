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
        using (args.PushGroup("markupcomp", -1))
            foreach (var text in entity.Comp.Texts.Values)
                args.PushMarkup(text);
    }

    public void AddDescriptionText(Entity<MarkupDescriptionComponent> entity, string id, string text)
    {
        entity.Comp.Texts.Add(id, text);
        Dirty(entity);
   }

    public void EditDescriptionText(Entity<MarkupDescriptionComponent> entity, string id, string text)
    {
        if (!entity.Comp.Texts.ContainsKey(id))
            return;

        entity.Comp.Texts[id] = text;
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
