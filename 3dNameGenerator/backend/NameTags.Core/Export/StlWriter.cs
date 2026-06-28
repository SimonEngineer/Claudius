using System.Numerics;
using System.Text;
using NameTags.Core.Geometry;

namespace NameTags.Core.Export;

/// <summary>Writes a Mesh3D as a binary STL stream.</summary>
public static class StlWriter
{
    public static void WriteBinary(Stream stream, Mesh3D mesh)
    {
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

        var header = new byte[80];
        writer.Write(header);
        writer.Write((uint)mesh.TriangleCount);

        foreach (var (a, b, c) in mesh.Triangles())
        {
            var normal = Vector3.Cross(b - a, c - a);
            normal = normal.LengthSquared() > 0 ? Vector3.Normalize(normal) : Vector3.Zero;

            WriteVector3(writer, normal);
            WriteVector3(writer, a);
            WriteVector3(writer, b);
            WriteVector3(writer, c);
            writer.Write((ushort)0);
        }
    }

    private static void WriteVector3(BinaryWriter writer, Vector3 v)
    {
        writer.Write(v.X);
        writer.Write(v.Y);
        writer.Write(v.Z);
    }
}
