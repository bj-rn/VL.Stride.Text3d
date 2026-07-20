// Collider point extraction for the async mesh nodes: distinct vertex positions are
// the input for convex hull baking (the async hull nodes in VL.Stride.BepuPhysics).
// Runs inside the nodes' background tasks; callers own the scratch collections and may
// reuse them across bakes because at most one bake runs at a time (latest-wins).
// The returned spreads are immutable snapshots: SpreadBuilder.ToSpread() never aliases
// the builder's buffer, so Clear() and refill cannot corrupt earlier results.

using Stride.Core.Mathematics;
using Stride.Graphics;
using VL.Lib.Collections;
using GlyphList = System.Collections.Generic.List<(Stride.Graphics.VertexPositionNormalTexture[] Vertices, Stride.Core.Mathematics.Vector2 Position)>;

namespace VL.Stride.Text3d.Core;

public static class ColliderPoints
{
    /// <summary>Distinct vertex positions of a mesh, in mesh-local space.</summary>
    public static Spread<Vector3> DistinctPositions(VertexPositionNormalTexture[] vertices,
        HashSet<Vector3> scratchSeen, SpreadBuilder<Vector3> scratchPoints)
    {
        scratchSeen.Clear();
        scratchPoints.Clear();
        foreach (var vertex in vertices)
        {
            if (scratchSeen.Add(vertex.Position))
                scratchPoints.Add(vertex.Position);
        }
        return scratchPoints.ToSpread();
    }

    /// <summary>
    /// Per glyph: distinct vertex positions with the glyph translation added, so all
    /// groups live in text-local space (the space of the composed Meshes plus
    /// Transformations outputs).
    /// </summary>
    public static Spread<Spread<Vector3>> DistinctPositionsPerGlyph(GlyphList glyphs,
        HashSet<Vector3> scratchSeen, SpreadBuilder<Vector3> scratchPoints,
        SpreadBuilder<Spread<Vector3>> scratchGroups)
    {
        scratchGroups.Clear();
        foreach (var (vertices, position) in glyphs)
        {
            scratchSeen.Clear();
            scratchPoints.Clear();
            var translation = new Vector3(position.X, position.Y, 0f);
            foreach (var vertex in vertices)
            {
                // Dedup on the glyph-local position, output the translated one.
                if (scratchSeen.Add(vertex.Position))
                    scratchPoints.Add(vertex.Position + translation);
            }
            scratchGroups.Add(scratchPoints.ToSpread());
        }
        return scratchGroups.ToSpread();
    }
}
