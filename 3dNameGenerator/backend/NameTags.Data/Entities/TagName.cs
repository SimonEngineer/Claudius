namespace NameTags.Data.Entities;

public sealed class TagName
{
    public int Id { get; set; }
    public int TagProjectId { get; set; }
    public TagProject TagProject { get; set; } = null!;
    public string Text { get; set; } = "";
    public int SortOrder { get; set; }
    public float? TextDepthMmOverride { get; set; }
}
