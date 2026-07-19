// Welds exactly identical vertices (position + normal + UV) of the generated triangle
// soup into an indexed mesh. This is visually lossless: vertices smoothed across edges
// and cap-interior vertices merge, while hard corners and the cap/side-wall seam stay
// split because their normals differ. Guarded by tests asserting that expanding the
// welded mesh through its index buffer reproduces the original soup bit-exactly.

using Stride.Graphics;

namespace VL.Stride.Text3d.Core;

public static class MeshWelder
{
    /// <summary>Deduplicates identical vertices, producing a compact vertex array plus a real index buffer.</summary>
    public static (VertexPositionNormalTexture[] Vertices, int[] Indices) Weld(
        IReadOnlyList<VertexPositionNormalTexture> triangleSoup)
    {
        var indexOf = new Dictionary<VertexPositionNormalTexture, int>(triangleSoup.Count);
        var vertices = new List<VertexPositionNormalTexture>(triangleSoup.Count / 2);
        var indices = new int[triangleSoup.Count];

        for (int i = 0; i < triangleSoup.Count; i++)
        {
            var vertex = triangleSoup[i];
            if (!indexOf.TryGetValue(vertex, out int index))
            {
                index = vertices.Count;
                vertices.Add(vertex);
                indexOf.Add(vertex, index);
            }
            indices[i] = index;
        }

        return (vertices.ToArray(), indices);
    }
}
