using System.Numerics;
using LibTessDotNet;
using NameTags.Core.Geometry;

namespace NameTags.Core.Triangulation;

/// <summary>Triangulates a Polygon2D (outer boundaries plus holes) into a flat triangle index list.</summary>
public static class PolygonTriangulator
{
    /// <summary>Returns a flat list of vertex indices into <paramref name="combinedVertices"/>, 3 per triangle.</summary>
    public static (List<Vector2> CombinedVertices, List<int> TriangleIndices) Triangulate(Polygon2D polygon)
    {
        var tess = new Tess();

        foreach (var contour in polygon.Contours)
        {
            var contourVertices = new ContourVertex[contour.Points.Count];
            for (int i = 0; i < contour.Points.Count; i++)
            {
                var p = contour.Points[i];
                contourVertices[i].Position = new Vec3(p.X, p.Y, 0);
            }
            tess.AddContour(contourVertices, ContourOrientation.Original);
        }

        tess.Tessellate(WindingRule.NonZero, ElementType.Polygons, polySize: 3);

        var vertices = new List<Vector2>(tess.VertexCount);
        for (int i = 0; i < tess.VertexCount; i++)
        {
            var v = tess.Vertices[i].Position;
            vertices.Add(new Vector2(v.X, v.Y));
        }

        var indices = new List<int>(tess.ElementCount * 3);
        for (int i = 0; i < tess.ElementCount; i++)
        {
            indices.Add(tess.Elements[i * 3]);
            indices.Add(tess.Elements[i * 3 + 1]);
            indices.Add(tess.Elements[i * 3 + 2]);
        }

        return (vertices, indices);
    }
}
