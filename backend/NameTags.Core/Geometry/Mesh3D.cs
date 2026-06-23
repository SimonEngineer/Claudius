using System.Numerics;

namespace NameTags.Core.Geometry;

/// <summary>Indexed triangle mesh. Triangle i uses Indices[3*i..3*i+2] into Vertices.</summary>
public sealed class Mesh3D
{
    public List<Vector3> Vertices { get; } = new();
    public List<int> Indices { get; } = new();

    public int TriangleCount => Indices.Count / 3;

    public void AddTriangle(Vector3 a, Vector3 b, Vector3 c)
    {
        int baseIndex = Vertices.Count;
        Vertices.Add(a);
        Vertices.Add(b);
        Vertices.Add(c);
        Indices.Add(baseIndex);
        Indices.Add(baseIndex + 1);
        Indices.Add(baseIndex + 2);
    }

    public IEnumerable<(Vector3 A, Vector3 B, Vector3 C)> Triangles()
    {
        for (int i = 0; i < Indices.Count; i += 3)
        {
            yield return (Vertices[Indices[i]], Vertices[Indices[i + 1]], Vertices[Indices[i + 2]]);
        }
    }

    public static Mesh3D Combine(params Mesh3D[] meshes)
    {
        var result = new Mesh3D();
        foreach (var mesh in meshes)
        {
            foreach (var (a, b, c) in mesh.Triangles())
            {
                result.AddTriangle(a, b, c);
            }
        }
        return result;
    }
}
