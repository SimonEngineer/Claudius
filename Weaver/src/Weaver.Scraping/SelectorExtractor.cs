using AngleSharp.Dom;
using Weaver.Domain;

namespace Weaver.Scraping;

public static class SelectorExtractor
{
    /// <summary>Extracts one record of field values from <paramref name="scope"/> (an item container, or the document).</summary>
    public static Dictionary<string, string?> ExtractFields(IParentNode scope, IReadOnlyList<FieldSelector> fields, Uri pageUrl)
    {
        var data = new Dictionary<string, string?>();

        foreach (var field in fields.OrderBy(f => f.Order))
        {
            var element = string.IsNullOrWhiteSpace(field.Selector)
                ? scope as IElement
                : scope.QuerySelector(field.Selector);

            var value = ExtractValue(element, field);

            if (field.ResolveUrl && !string.IsNullOrWhiteSpace(value) && Uri.TryCreate(pageUrl, value, out var resolved))
            {
                value = resolved.ToString();
            }

            value = FieldTransforms.Apply(value, FieldTransforms.Parse(field.TransformsJson));

            if (field.Required && string.IsNullOrWhiteSpace(value))
            {
                throw new FieldExtractionException(field.Name, field.Selector);
            }

            data[field.Name] = value;
        }

        return data;
    }

    private static string? ExtractValue(IElement? element, FieldSelector field)
    {
        if (element is null)
        {
            return null;
        }

        return field.Attribute switch
        {
            FieldAttribute.Text => element.TextContent.Trim(),
            FieldAttribute.Html => element.InnerHtml.Trim(),
            FieldAttribute.Href => element.GetAttribute("href"),
            FieldAttribute.Src => element.GetAttribute("src"),
            FieldAttribute.Attribute => string.IsNullOrEmpty(field.AttributeName) ? null : element.GetAttribute(field.AttributeName),
            _ => element.TextContent.Trim()
        };
    }
}

public class FieldExtractionException : Exception
{
    public FieldExtractionException(string fieldName, string selector)
        : base($"Required field '{fieldName}' (selector '{selector}') was not found.")
    {
    }
}
