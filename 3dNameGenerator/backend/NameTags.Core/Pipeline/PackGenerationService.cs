using System.Numerics;
using NameTags.Core.Geometry;

namespace NameTags.Core.Pipeline;

/// <summary>
/// Builds one "pack" per name: the project's normal standing table tag plus a wine-glass
/// charm and a clothes clip, laid out side by side (non-overlapping, all sitting flat at
/// z=0) and combined into a single Mesh3D -- so a saved project with ~100 names exports as
/// ~100 self-contained, slicer-ready files with nothing to rearrange by hand.
/// </summary>
public static class PackGenerationService
{
    private const float LayoutGapMm = 6f;

    public static Mesh3D BuildPackMesh(ModelGenerationService modelService, TagGenerationRequest standingRequest)
    {
        var standingMesh = modelService.GenerateTagMesh(standingRequest);
        var charmMesh = WineGlassCharmBuilder.Build(modelService, standingRequest.Text, standingRequest.FontFamilyOrPath);
        var clipMesh = ClothesClipBuilder.Build(modelService, standingRequest.Text, standingRequest.FontFamilyOrPath);

        var meshes = new[] { standingMesh, charmMesh, clipMesh };
        var positioned = new Mesh3D[meshes.Length];

        float cursorX = 0f;
        for (int i = 0; i < meshes.Length; i++)
        {
            var (min, max) = meshes[i].GetBounds();
            float width = max.X - min.X;

            // Shift so this mesh's own min.X lands at cursorX, and drop its z-min to 0 (every
            // builder already extrudes from z=0, but this keeps the pack robust even if that changes).
            var offset = new Vector3(cursorX - min.X, -min.Y - (max.Y - min.Y) / 2f, -min.Z);
            positioned[i] = meshes[i].Translate(offset);

            cursorX += width + LayoutGapMm;
        }

        return Mesh3D.Combine(positioned);
    }
}
