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

    /// <summary>Returns a new mesh with every vertex shifted by offset. Winding (and thus normals) is unaffected by a pure translation.</summary>
    public Mesh3D Translate(Vector3 offset)
    {
        var result = new Mesh3D();
        foreach (var (a, b, c) in Triangles())
        {
            result.AddTriangle(a + offset, b + offset, c + offset);
        }
        return result;
    }

    public (Vector3 Min, Vector3 Max) GetBounds()
    {
        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        foreach (var v in Vertices)
        {
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }
        return (min, max);
    }
}
