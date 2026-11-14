// ReSharper disable once CheckNamespace once
namespace Content.Client.MassMedia.Ui;

public sealed partial class ArticleEditorPanel
{
    public void SetPhotoState(bool hasPhoto)
    {
        PhotoStatusLabel.Text = Loc.GetString(hasPhoto ? "news-write-ui-photo-attached" : "news-write-ui-photo-empty");
        PhotoEjectButton.Visible = hasPhoto;
    }
}
