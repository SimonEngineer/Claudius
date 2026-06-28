using NameTags.Core.Geometry;

namespace NameTags.Core.Outlines;

/// <summary>Produces a 2D outline (one contour per glyph sub-path, e.g. letter bodies and their holes) for a line of text.</summary>
public interface IGlyphOutlineProvider
{
    Polygon2D GetTextOutline(string text, string fontFamilyOrPath, float fontSizeMm);
}
