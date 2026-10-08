using Content.Client._CD.Records.UI;
using Content.Shared._CD.Records;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private bool _allowFlavorText;
    private bool _allowCharacterSecrets;
    private bool _allowExploitables;
    private bool _allowRPNotes;

    private readonly RecordEditorGui _recordsTab;

    private void SetupTabs()
    {
        TabContainer.SetTabTitle(0, Loc.GetString("humanoid-profile-editor-appearance-tab"));
        TabContainer.SetTabTitle(1, Loc.GetString("humanoid-profile-editor-jobs-tab"));
        TabContainer.SetTabTitle(2, Loc.GetString("humanoid-profile-editor-antags-tab"));
        TabContainer.SetTabTitle(3, Loc.GetString("humanoid-profile-editor-traits-tab"));
        TabContainer.SetTabTitle(4, Loc.GetString("humanoid-profile-editor-markings-tab"));
        TabContainer.SetTabTitle(5, Loc.GetString("humanoid-profile-editor-cybernetics-tab"));
        TabContainer.SetTabTitle(6, Loc.GetString("humanoid-profile-editor-ic-info-tab"));
        TabContainer.SetTabTitle(7, Loc.GetString("humanoid-profile-editor-ooc-info-tab"));
    }

    private RecordEditorGui CreateRecordEditorTab()
    {
        var recordEditor = new RecordEditorGui(UpdateProfileRecords)
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        TabContainer.AddChild(recordEditor);
        TabContainer.SetTabTitle(
            TabContainer.ChildCount - 1,
            Loc.GetString("humanoid-profile-editor-cd-records-tab"));
        recordEditor.Update(Profile);
        return recordEditor;
    }

    private void SetupInfoEditors()
    {
        ICInfoEditor.PhysicalDescInput.OnTextChanged += OnPhysicalDescChanged;
        ICInfoEditor.PersonalityDescInput.OnTextChanged += OnPersonalityDescChanged;
        ICInfoEditor.ExploitableInput.OnTextChanged += OnExploitablesChanged;
        ICInfoEditor.SecretsInput.OnTextChanged += OnSecretsChanged;

        OOCInfoEditor.PersonalNotesInput.OnTextChanged += OnPersonalNotesChanged;
        OOCInfoEditor.OOCNotesInput.OnTextChanged += OnOOCNotesChanged;
    }

    public void RefreshCharacterInfo()
    {
        if (ICInfoEditor.VisibleInTree)
        {
            ICInfoEditor.Physical.Visible = _allowFlavorText;
            ICInfoEditor.Personality.Visible = _allowFlavorText;
            ICInfoEditor.Secrets.Visible = _allowCharacterSecrets;
            ICInfoEditor.Exploitable.Visible = _allowExploitables;
        }

        if (OOCInfoEditor.VisibleInTree)
        {
            OOCInfoEditor.OOCNotes.Visible = _allowRPNotes;
            OOCInfoEditor.PersonalNotes.Visible = _allowRPNotes;
        }
    }

    private void UpdateCharacterInfoEditorText()
    {
        ICInfoEditor.PhysicalDescInput.TextRope = new Rope.Leaf(Profile?.PhysicalDescription ?? "");
        ICInfoEditor.PersonalityDescInput.TextRope = new Rope.Leaf(Profile?.PersonalityDescription ?? "");
        ICInfoEditor.ExploitableInput.TextRope = new Rope.Leaf(Profile?.ExploitableInfo ?? "");
        ICInfoEditor.SecretsInput.TextRope = new Rope.Leaf(Profile?.Secrets ?? "");

        OOCInfoEditor.PersonalNotesInput.TextRope = new Rope.Leaf(Profile?.PersonalNotes ?? "");
        OOCInfoEditor.OOCNotesInput.TextRope = new Rope.Leaf(Profile?.OOCNotes ?? "");
    }

    private void OnPhysicalDescChanged(TextEdit.TextEditEventArgs args)
    {
        if (Profile is null)
            return;

        Profile = Profile.WithPhysicalDesc(Rope.Collapse(args.TextRope).Trim());
        IsDirty = true;
    }

    private void OnPersonalityDescChanged(TextEdit.TextEditEventArgs args)
    {
        if (Profile is null)
            return;

        Profile = Profile.WithPersonalityDesc(Rope.Collapse(args.TextRope).Trim());
        IsDirty = true;
    }

    private void OnExploitablesChanged(TextEdit.TextEditEventArgs args)
    {
        if (Profile is null)
            return;

        Profile = Profile.WithExploitable(Rope.Collapse(args.TextRope).Trim());
        IsDirty = true;
    }

    private void OnSecretsChanged(TextEdit.TextEditEventArgs args)
    {
        if (Profile is null)
            return;

        Profile = Profile.WithSecrets(Rope.Collapse(args.TextRope).Trim());
        IsDirty = true;
    }

    private void OnPersonalNotesChanged(TextEdit.TextEditEventArgs args)
    {
        if (Profile is null)
            return;

        Profile = Profile.WithPersonalNotes(Rope.Collapse(args.TextRope).Trim());
        IsDirty = true;
    }

    private void OnOOCNotesChanged(TextEdit.TextEditEventArgs args)
    {
        if (Profile is null)
            return;

        Profile = Profile.WithOOCNotes(Rope.Collapse(args.TextRope).Trim());
        IsDirty = true;
    }

    private void UpdateProfileRecords(PlayerProvidedCharacterRecords records)
    {
        if (Profile is null)
            return;

        Profile = Profile.WithCDCharacterRecords(records);
        SetDirty();
    }
}
