using System.Numerics;
using NameTags.Core.Geometry;
using NameTags.Core.Triangulation;

namespace NameTags.Core.Extrusion;

/// <summary>
/// Extrudes a triangulated 2D polygon into a closed 3D solid: a top cap, a bottom cap,
/// and side walls along the polygon's original boundary contours (not the triangulator's
/// internal diagonals). Outline providers must wind outer boundaries CCW and holes CW
/// (so solid material is always to the left when walking a contour forward) -- this is
/// also what makes the NonZero triangulation winding rule punch holes correctly, and it
/// lets the wall-normal formula below stay uniform for outer and hole contours alike.
/// </summary>
public static class MeshExtruder
{
    public static Mesh3D Extrude(Polygon2D polygon, float zBottom, float zTop)
    {
        var (vertices2D, triangleIndices) = PolygonTriangulator.Triangulate(polygon);
        var mesh = new Mesh3D();

        for (int i = 0; i < triangleIndices.Count; i += 3)
        {
            var v0 = vertices2D[triangleIndices[i]];
            var v1 = vertices2D[triangleIndices[i + 1]];
            var v2 = vertices2D[triangleIndices[i + 2]];

            // Top cap: keep winding as-is (CCW input -> +Z-facing normal).
            mesh.AddTriangle(new Vector3(v0, zTop), new Vector3(v1, zTop), new Vector3(v2, zTop));

            // Bottom cap: reverse winding so the normal faces -Z.
            mesh.AddTriangle(new Vector3(v0, zBottom), new Vector3(v2, zBottom), new Vector3(v1, zBottom));
        }

        foreach (var contour in polygon.Contours)
        {
            var points = contour.Points;
            int n = points.Count;
            for (int i = 0; i < n; i++)
            {
                var p0 = points[i];
                var p1 = points[(i + 1) % n];

                var a = new Vector3(p0, zBottom);
                var b = new Vector3(p1, zBottom);
                var c = new Vector3(p1, zTop);
                var d = new Vector3(p0, zTop);

                mesh.AddTriangle(a, b, c);
                mesh.AddTriangle(a, c, d);
            }
        }

        return mesh;
    }
}
