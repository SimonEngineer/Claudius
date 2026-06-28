using System.Numerics;
using NameTags.Core.Outlines;
using NameTags.Core.Pipeline;
using Xunit;

namespace NameTags.Tests;

public class BevelTests
{
    [Fact]
    public void GenerateTagMesh_WithBevel_TopFaceFootprintIsSmallerThanBottom()
    {
        var request = new TagGenerationRequest
        {
            Text = "Hi",
            ShapeType = ShapeType.RoundedRectangle,
            PlateWidthMm = 70f,
            PlateHeightMm = 30f,
            PlateThicknessMm = 3f,
            BevelMm = 1f,
        };

        var mesh = new ModelGenerationService().GenerateTagMesh(request);

        float bottomMaxX = float.MinValue, topMaxX = float.MinValue;
        foreach (var (a, b, c) in mesh.Triangles())
        {
            foreach (var v in new[] { a, b, c })
            {
                if (v.Z <= 0.001f) bottomMaxX = MathF.Max(bottomMaxX, v.X);
                else if (MathF.Abs(v.Z - request.PlateThicknessMm) < 0.001f) topMaxX = MathF.Max(topMaxX, v.X);
            }
        }

        Assert.True(bottomMaxX > 0f, "Expected bottom-face vertices at z=0.");
        Assert.True(topMaxX > 0f, "Expected top-face vertices at z=PlateThicknessMm.");
        Assert.True(topMaxX < bottomMaxX, $"Bevel should inset the top face (top {topMaxX} should be < bottom {bottomMaxX}).");
    }

    [Fact]
    public void GenerateTagMesh_BevelIgnored_ForCustomSvgShape()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100\" height=\"40\"><rect x=\"0\" y=\"0\" width=\"100\" height=\"40\"/></svg>";
        var request = new TagGenerationRequest
        {
            Text = "Hi",
            ShapeType = ShapeType.CustomSvg,
            CustomSvgBytes = System.Text.Encoding.UTF8.GetBytes(svg),
            PlateWidthMm = 70f,
            PlateHeightMm = 30f,
            BevelMm = 1f,
        };

        // Should not throw despite BevelMm being set -- bevel is silently skipped for CustomSvg.
        var mesh = new ModelGenerationService().GenerateTagMesh(request);
        Assert.True(mesh.TriangleCount > 0);
    }
}
