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
            AddWalls(mesh, contour, zBottom, zTop);
        }

        return mesh;
    }

    /// <summary>
    /// Extrudes with a tapered chamfer cut into the top edge: straight walls from zBottom up
    /// to zBevelStart using fullOutline's full footprint, then a tapered ring up to zTop that
    /// slopes inward to insetOutline's smaller footprint, capped there. insetOutline's outer
    /// contour must have the same point count/order as fullOutline's (callers get this for
    /// free by re-invoking the same shape provider with a smaller target size). Any further
    /// contours (mounting holes) are walled straight through the full height, unaffected by
    /// the bevel, and must appear in both outlines with identical geometry.
    /// </summary>
    public static Mesh3D ExtrudeWithTopBevel(Polygon2D fullOutline, Polygon2D insetOutline, float zBottom, float zBevelStart, float zTop)
    {
        var mesh = new Mesh3D();

        var (bottomVerts, bottomIndices) = PolygonTriangulator.Triangulate(fullOutline);
        for (int i = 0; i < bottomIndices.Count; i += 3)
        {
            var v0 = bottomVerts[bottomIndices[i]];
            var v1 = bottomVerts[bottomIndices[i + 1]];
            var v2 = bottomVerts[bottomIndices[i + 2]];
            mesh.AddTriangle(new Vector3(v0, zBottom), new Vector3(v2, zBottom), new Vector3(v1, zBottom));
        }

        var (topVerts, topIndices) = PolygonTriangulator.Triangulate(insetOutline);
        for (int i = 0; i < topIndices.Count; i += 3)
        {
            var v0 = topVerts[topIndices[i]];
            var v1 = topVerts[topIndices[i + 1]];
            var v2 = topVerts[topIndices[i + 2]];
            mesh.AddTriangle(new Vector3(v0, zTop), new Vector3(v1, zTop), new Vector3(v2, zTop));
        }

        AddWalls(mesh, fullOutline.Contours[0], zBottom, zBevelStart);
        AddTaperedRing(mesh, fullOutline.Contours[0], insetOutline.Contours[0], zBevelStart, zTop);

        for (int i = 1; i < fullOutline.Contours.Count; i++)
        {
            AddWalls(mesh, fullOutline.Contours[i], zBottom, zTop);
        }

        return mesh;
    }

    private static void AddWalls(Mesh3D mesh, Contour contour, float zBottom, float zTop)
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

    private static void AddTaperedRing(Mesh3D mesh, Contour outerContour, Contour innerContour, float zOuter, float zInner)
    {
        var outerPoints = outerContour.Points;
        var innerPoints = innerContour.Points;
        int n = outerPoints.Count;
        for (int i = 0; i < n; i++)
        {
            var a = new Vector3(outerPoints[i], zOuter);
            var b = new Vector3(outerPoints[(i + 1) % n], zOuter);
            var c = new Vector3(innerPoints[(i + 1) % n], zInner);
            var d = new Vector3(innerPoints[i], zInner);

            mesh.AddTriangle(a, b, c);
            mesh.AddTriangle(a, c, d);
        }
    }
}
