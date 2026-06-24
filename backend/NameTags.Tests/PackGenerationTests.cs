using NameTags.Core.Outlines;
using NameTags.Core.Pipeline;
using Xunit;

namespace NameTags.Tests;

public class PackGenerationTests
{
    private static TagGenerationRequest StandingRequest(string text) => new()
    {
        Text = text,
        ShapeType = ShapeType.RoundedRectangle,
        PlateWidthMm = 70f,
        PlateHeightMm = 30f,
        PlateThicknessMm = 3f,
    };

    [Fact]
    public void HookOutline_IsCounterClockwise()
    {
        var outline = HookOutlineProvider.BuildOpenHookOutline(6f, 1.8f);
        Assert.True(outline.Contours[0].IsCounterClockwise);
    }

    [Fact]
    public void ClipOutline_IsCounterClockwise()
    {
        var outline = ClipOutlineProvider.BuildAlligatorClipOutline(20f, 2.4f, 1.2f);
        Assert.True(outline.Contours[0].IsCounterClockwise);
    }

    [Fact]
    public void WineGlassCharm_ProducesNonEmptyWatertightLookingMesh()
    {
        var mesh = WineGlassCharmBuilder.Build(new ModelGenerationService(), "Al", "DejaVuSans-Bold.ttf");
        Assert.True(mesh.TriangleCount > 0);
        // Hook must actually extend above the plate's own top edge -- otherwise it didn't fuse on, it floats separately.
        var (min, max) = mesh.GetBounds();
        Assert.True(max.Y > WineGlassCharmBuilder.PlateHeightMm / 2f);
        Assert.True(min.Z >= -0.001f);
    }

    [Fact]
    public void ClothesClip_ExtendsBelowPlateBottomEdge()
    {
        var mesh = ClothesClipBuilder.Build(new ModelGenerationService(), "Al", "DejaVuSans-Bold.ttf");
        var (min, max) = mesh.GetBounds();
        Assert.True(min.Y < -ClothesClipBuilder.PlateHeightMm / 2f);
    }

    [Fact]
    public void BuildPackMesh_LaysOutThreeVariantsWithoutXOverlap()
    {
        var mesh = PackGenerationService.BuildPackMesh(new ModelGenerationService(), StandingRequest("Alice"));
        Assert.True(mesh.TriangleCount > 0);

        var (min, max) = mesh.GetBounds();
        // Three tags plus 2 layout gaps side by side must span more than any single tag's width.
        Assert.True(max.X - min.X > ClothesClipBuilder.PlateWidthMm + WineGlassCharmBuilder.PlateWidthMm);
        // Everything must sit flat on the bed.
        Assert.True(min.Z >= -0.001f);
    }
}
