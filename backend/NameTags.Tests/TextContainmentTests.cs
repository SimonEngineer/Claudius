using NameTags.Core.Geometry;
using NameTags.Core.Outlines;
using NameTags.Core.Pipeline;
using Xunit;

namespace NameTags.Tests;

public class TextContainmentTests
{
    [Theory]
    [InlineData(ShapeType.Star)]
    [InlineData(ShapeType.Heart)]
    [InlineData(ShapeType.Oval)]
    public void GenerateTagMesh_KeepsTextVerticesInsidePlateSilhouette(ShapeType shapeType)
    {
        var request = new TagGenerationRequest
        {
            Text = "Maximilian",
            ShapeType = shapeType,
            PlateWidthMm = 70f,
            PlateHeightMm = 30f,
            TextMarginLeftMm = 2f,
            TextMarginRightMm = 2f,
            TextMarginTopMm = 2f,
            TextMarginBottomMm = 2f,
        };

        var service = new ModelGenerationService();
        var mesh = service.GenerateTagMesh(request);

        var plateOutline = ShapeOutlineProviderFactory.Resolve(shapeType)
            .GetOutline(request.ShapeParams, request.PlateWidthMm, request.PlateHeightMm);

        // Text triangles sit above the plate's top face (z > PlateThicknessMm); checking
        // every such vertex's XY against the plate silhouette is exactly the guarantee
        // this app needs for 3D printing -- bounding-box-only containment would miss the
        // corners that poke outside a Star's points or a Heart's lobes.
        bool foundTextVertex = false;
        foreach (var (a, b, c) in mesh.Triangles())
        {
            foreach (var vertex in new[] { a, b, c })
            {
                if (vertex.Z <= request.PlateThicknessMm) continue;
                foundTextVertex = true;
                var xy = new System.Numerics.Vector2(vertex.X, vertex.Y);
                Assert.True(PointInPolygon.IsInside(plateOutline, xy),
                    $"Text vertex ({vertex.X},{vertex.Y}) on {shapeType} plate fell outside the plate silhouette.");
            }
        }

        Assert.True(foundTextVertex, "Expected at least one text vertex above the plate's top face.");
    }
}
