namespace NameTags.Api.Validation;

public static class TagTextValidator
{
    public const int MaxLength = 40;

    /// <summary>Returns an error message if the text isn't usable for generation, or null if it's fine.</summary>
    public static string? Validate(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "Name text cannot be empty.";
        }
        if (text.Length > MaxLength)
        {
            return $"Name text cannot exceed {MaxLength} characters (got {text.Length}).";
        }
        return null;
    }
}
