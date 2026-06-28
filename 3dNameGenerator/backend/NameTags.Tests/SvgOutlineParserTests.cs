using NameTags.Core.Svg;
using Xunit;

namespace NameTags.Tests;

public class SvgOutlineParserTests
{
    [Fact]
    public void Parse_AppliesGroupTransform_ToRectGeometry()
    {
        // <rect> local bounds (0,0)-(10,5), wrapped in a <g> that scales by 2 then
        // translates by (10,10) -- per SVG semantics the rightmost-listed function
        // (scale) is applied to the point first, then the leftmost (translate).
        var svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <g transform="translate(10,10) scale(2)">
                <rect x="0" y="0" width="10" height="5" />
              </g>
            </svg>
            """;
        var bytes = System.Text.Encoding.UTF8.GetBytes(svg);

        // Use a target size equal to the expected pre-fit bounding box so FitToSize
        // is a no-op (scale factor 1), letting us assert on the transform math alone.
        var polygon = SvgOutlineParser.Parse(bytes, targetWidthMm: 20f, targetHeightMm: 10f);

        var (min, max) = polygon.GetBounds();
        var size = max - min;

        Assert.InRange(size.X, 19.5f, 20.5f);
        Assert.InRange(size.Y, 9.5f, 10.5f);
    }

    [Fact]
    public void Parse_Throws_WhenSvgHasNoUsableGeometry()
    {
        var svg = """<svg xmlns="http://www.w3.org/2000/svg" width="10" height="10"></svg>""";
        var bytes = System.Text.Encoding.UTF8.GetBytes(svg);

        Assert.Throws<InvalidOperationException>(() => SvgOutlineParser.Parse(bytes, 10f, 10f));
    }
}
