using System.Numerics;
using Microsoft.AspNetCore.Mvc;
using NameTags.Core.Export;
using NameTags.Core.Extrusion;
using NameTags.Core.Geometry;
using NameTags.Core.Outlines;

namespace NameTags.Api.Controllers;

[ApiController]
[Route("api/debug")]
public class DebugController : ControllerBase
{
    /// <summary>Returns a simple 20x20x10mm box as binary STL, to prove the export pipeline works end-to-end.</summary>
    [HttpGet("box.stl")]
    public IActionResult GetBox()
    {
        var mesh = BuildBox(new Vector3(20, 20, 10));
        var stream = new MemoryStream();
        StlWriter.WriteBinary(stream, mesh);
        stream.Position = 0;
        return File(stream, "model/stl", "box.stl");
    }

    /// <summary>Returns a bare extruded plate for a given preset shape, to verify outline+triangulation+extrusion end-to-end.</summary>
    [HttpGet("plate.stl")]
    public IActionResult GetPlate([FromQuery] ShapeType shape = ShapeType.RoundedRectangle)
    {
        var provider = ShapeOutlineProviderFactory.Resolve(shape);
        var outline = provider.GetOutline(new ShapeParams(), targetWidthMm: 60, targetHeightMm: 30);
        var mesh = MeshExtruder.Extrude(outline, zBottom: 0, zTop: 3);

        var stream = new MemoryStream();
        StlWriter.WriteBinary(stream, mesh);
        stream.Position = 0;
        return File(stream, "model/stl", $"plate-{shape}.stl");
    }

    private static Mesh3D BuildBox(Vector3 size)
    {
        var mesh = new Mesh3D();
        var h = size / 2f;

        var corners = new[]
        {
            new Vector3(-h.X, -h.Y, -h.Z), new Vector3(h.X, -h.Y, -h.Z),
            new Vector3(h.X, h.Y, -h.Z), new Vector3(-h.X, h.Y, -h.Z),
            new Vector3(-h.X, -h.Y, h.Z), new Vector3(h.X, -h.Y, h.Z),
            new Vector3(h.X, h.Y, h.Z), new Vector3(-h.X, h.Y, h.Z),
        };

        void Quad(int a, int b, int c, int d)
        {
            mesh.AddTriangle(corners[a], corners[b], corners[c]);
            mesh.AddTriangle(corners[a], corners[c], corners[d]);
        }

        Quad(0, 3, 2, 1); // bottom
        Quad(4, 5, 6, 7); // top
        Quad(0, 1, 5, 4); // front
        Quad(1, 2, 6, 5); // right
        Quad(2, 3, 7, 6); // back
        Quad(3, 0, 4, 7); // left

        return mesh;
    }
}
